using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WaterbusSystem.Application;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.SightseeingAudio;
using WaterbusSystem.Application.Features.Incidents;
using WaterbusSystem.Application.Features.SightseeingAudio;
using WaterbusSystem.Application.Features.SightseeingAudio.Interfaces;
using WaterbusSystem.Application.Features.SightseeingAudio.Models;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.Infrastructure.Services.SightseeingAudio;
using WaterbusSystem.Infrastructure.Services.Tracking;
using WaterbusSystem.UnitTests.Domain;
using WaterbusSystem.UnitTests.TestHelpers;
using WaterbusSystem.WebApi.Controllers;
using WaterbusSystem.WebApi.Filters;
using WaterbusSystem.WebApi.Middlewares;
using WaterbusSystem.WebApi.Realtime;
using Xunit;

namespace WaterbusSystem.UnitTests.SightseeingAudio;

/// <summary>
/// Task 5 — luồng audio Sightseeing thật: GPS (REST) → geofence → sổ cái SQL (chống trùng) → SignalR tới Onboard Player của
/// đúng Boat → ACK → tải MP3 có kiểm soát. SQL Server thật, host HTTP/SignalR thật; chỉ kho Blob và Redis được thay bằng fake.
/// Không khẳng định audio phát ra loa (BR-EXP-03): player ở đây là SignalR client giả lập.
/// </summary>
[Collection(SqlCollections.Serial)]
public sealed class OnboardAudioPlaybackSqlTests : IAsyncLifetime
{
    private const decimal PoiLat = 10.7750m, PoiLng = 106.7050m;
    private static readonly decimal FarLat = 10.7850m;   // ~1.1 km, ngoài bán kính 150 m

    private string? _connection;
    private WebApplication _app = null!;
    private readonly string _jwtSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private readonly FakeStorage _storage = new();
    private readonly List<HubConnection> _connections = [];
    private TestDbContextFactory.TripSeedData _seed = null!;
    private readonly Guid _captain = Guid.NewGuid(), _otherCaptain = Guid.NewGuid();
    private Guid _routePoiId, _poiId;
    private ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(_connection!).Options);

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("WATERBUS_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(configured)) return;
        _connection = new SqlConnectionStringBuilder(configured)
        { InitialCatalog = "WaterbusDomainTests_Audio_" + Guid.NewGuid().ToString("N") }.ConnectionString;
        await using (var c = Context()) await c.Database.MigrateAsync();

        using var key = RSA.Create(2048);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = _connection,
            ["JwtSettings:Secret"] = _jwtSecret,
            ["VnPay:TmnCode"] = "AUDTEST01",
            ["VnPay:HashSecret"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            ["Qr:PrivateKeyPem"] = key.ExportPkcs8PrivateKeyPem(),
            ["SightseeingAudio:EnableGpsTriggerEvaluation"] = "true",
            ["SightseeingAudio:AckTimeoutSeconds"] = "1",
            ["SightseeingAudio:MaxDispatchAttempts"] = "2",
            ["SightseeingAudio:ReservationLeaseSeconds"] = "30",
            ["SightseeingAudio:MaxPositionAgeSeconds"] = "30"
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddApplicationServices();
        builder.Services.AddInfrastructureServices(builder.Configuration);
        builder.Services.RemoveAll<IHostedService>();
        builder.Services.AddSingleton<IDistributedLockService, FakeDistributedLockService>();
        builder.Services.AddSingleton<ICheckoutSessionStore, CommercialHandlerFactory.MemoryStore>();
        builder.Services.RemoveAll<IBoatPositionCache>();
        builder.Services.AddSingleton<IBoatPositionCache, InMemoryBoatPositionCache>();
        builder.Services.RemoveAll<IAudioStorageService>();
        builder.Services.AddSingleton<IAudioStorageService>(_storage);
        builder.Services.AddScoped<ValidateRequestFilter>();
        builder.Services.AddRealtime();
        builder.Services.AddControllers().AddApplicationPart(typeof(OnboardAudioController).Assembly);
        _app = builder.Build();
        _app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapControllers();
        _app.MapRealtimeHubs();
        await _app.StartAsync();

        await using var seed = Context();
        _seed = TestDbContextFactory.SeedTrip(seed);
        var boat = await seed.Boats.SingleAsync(b => b.Id == _seed.Boat.Id);
        boat.CaptainUserId = _captain;
        (await seed.Boats.SingleAsync(b => b.Id == _seed.OtherBoat.Id)).CaptainUserId = _otherCaptain;
        var trip = await seed.Trips.SingleAsync(t => t.Id == _seed.Trip.Id);
        trip.TripType = TripType.Sightseeing;
        trip.Status = TripStatus.EnRoute;
        var poi = new PointOfInterest { Name = "Bến Bạch Đằng", Latitude = PoiLat, Longitude = PoiLng, IsActive = true };
        var routePoi = new RoutePoi { RouteId = _seed.Route.Id, PoiId = poi.Id, DisplayOrder = 1, TriggerRadiusM = 150, IsActive = true };
        seed.PointsOfInterest.Add(poi);
        seed.RoutePois.Add(routePoi);
        seed.AudioGuides.Add(new AudioGuide
        {
            PoiId = poi.Id, LanguageCode = "vi", IsPublished = true, DurationSeconds = 42,
            AudioAssetUri = "https://example.blob.core.windows.net/audio/audio-assets/tts/20261108/poi.mp3"
        });
        await seed.SaveChangesAsync();
        _routePoiId = routePoi.Id;
        _poiId = poi.Id;
        _storage.Files["audio-assets/tts/20261108/poi.mp3"] = [1, 2, 3, 4, 5];
    }

    public async Task DisposeAsync()
    {
        foreach (var c in _connections) await c.DisposeAsync();
        if (_app is not null) await _app.DisposeAsync();
        if (_connection is null) return;
        if (!new SqlConnectionStringBuilder(_connection).InitialCatalog.StartsWith("WaterbusDomainTests_Audio_"))
            throw new InvalidOperationException("Unsafe test database.");
        await using var c2 = Context();
        await c2.Database.EnsureDeletedAsync();
    }

    // ───────────────────────── helpers ─────────────────────────

    private string Jwt(Guid id, string role) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
        "WaterbusSystemApi", "WaterbusClients", [new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Role, role)],
        expires: DateTime.UtcNow.AddMinutes(10),
        signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret)), SecurityAlgorithms.HmacSha256)));

    private HttpClient Http(Guid? id = null, string role = "Captain")
    {
        var client = new HttpClient { BaseAddress = new Uri(_app.Urls.Single()), Timeout = TimeSpan.FromSeconds(30) };
        if (id is { } v) client.DefaultRequestHeaders.Authorization = new("Bearer", Jwt(v, role));
        return client;
    }

    private sealed class Player
    {
        public HubConnection Hub = null!;
        public readonly ConcurrentQueue<JsonElement> Triggers = new();
        public readonly ConcurrentQueue<JsonElement> Controls = new();
        public readonly ConcurrentQueue<JsonElement> States = new();
        public bool AutoAccept = true;
        public bool StartImmediately;
        public readonly ConcurrentQueue<bool> StartResults = new();
    }

    private async Task<Player> ConnectPlayerAsync(Guid captain, string role = "Captain", bool autoAccept = true)
    {
        var player = new Player { AutoAccept = autoAccept };
        player.Hub = new HubConnectionBuilder().WithUrl(_app.Urls.Single() + "/hubs/onboard-audio",
            o => o.AccessTokenProvider = () => Task.FromResult<string?>(Jwt(captain, role))).Build();
        player.Hub.On<JsonElement>("audioTrigger", async m =>
        {
            player.Triggers.Enqueue(m);
            if (!player.AutoAccept) return;
            var id = m.GetProperty("requestId").GetGuid();
            await player.Hub.InvokeAsync<bool>("AcceptPlayback", id);
            // Finding A: báo Started ngay sau ACK, trước khi dispatcher kịp ghi gì thêm
            if (player.StartImmediately) player.StartResults.Enqueue(await player.Hub.InvokeAsync<bool>("ReportPlaybackState", id, "Started", null));
        });
        player.Hub.On<JsonElement>("audioControl", m => player.Controls.Enqueue(m));
        player.Hub.On<JsonElement>("audioState", m => player.States.Enqueue(m));
        _connections.Add(player.Hub);
        await player.Hub.StartAsync();
        await WaitAsync(() => !player.States.IsEmpty);
        return player;
    }

    private static async Task WaitAsync(Func<bool> condition, int ms = 5000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(ms);
        while (!condition() && DateTime.UtcNow < until) await Task.Delay(25);
    }

    /// <summary>Gửi GPS qua REST như Captain Portal; ObservedAt tăng dần để không bị coi là sai thứ tự.</summary>
    private int _tick;
    private async Task<HttpStatusCode> GpsAsync(decimal lat, DateTimeOffset? observedAt = null)
    {
        using var http = Http(_captain);
        var response = await http.PostAsJsonAsync($"/api/v1/tracking/boats/{_seed.Boat.Id}/positions",
            new { latitude = lat, longitude = PoiLng, speedKnots = 5m, observedAt = observedAt ?? DateTimeOffset.UtcNow.AddMilliseconds(++_tick) });
        return response.StatusCode;
    }

    private async Task<AudioPlaybackRequest?> LedgerAsync()
    {
        await using var c = Context();
        return await c.AudioPlaybackRequests.AsNoTracking().SingleOrDefaultAsync();
    }

    // ───────────────────────── happy path & dedup ─────────────────────────

    [SqlServerDomainFact]
    public async Task GpsInsideGeofence_DeliversOneTriggerToTheBoatsPlayer_WithMetadata_AndNeverRepeats()
    {
        var player = await ConnectPlayerAsync(_captain);
        Assert.Equal("Allowed", player.States.Single().GetProperty("reason").GetString());

        Assert.Equal(HttpStatusCode.OK, await GpsAsync(FarLat));
        Assert.Empty(player.Triggers);

        Assert.Equal(HttpStatusCode.OK, await GpsAsync(PoiLat));
        Assert.Single(player.Triggers);
        var t = player.Triggers.Single();
        Assert.Equal(_seed.Boat.Id, t.GetProperty("boatId").GetGuid());
        Assert.Equal(_seed.Trip.Id, t.GetProperty("tripId").GetGuid());
        Assert.Equal(_poiId, t.GetProperty("poiId").GetGuid());
        Assert.Equal("Bến Bạch Đằng", t.GetProperty("poiName").GetString());
        Assert.Equal("audio/mpeg", t.GetProperty("contentType").GetString());
        Assert.Equal(42, t.GetProperty("durationSeconds").GetInt32());
        var requestId = t.GetProperty("requestId").GetGuid();
        Assert.Equal(AudioRequestIds.For(_seed.Trip.Id, _routePoiId, null), requestId);
        var path = t.GetProperty("playbackPath").GetString()!;
        Assert.DoesNotContain("?", path);   // không có SAS/token trong URL
        Assert.DoesNotContain("blob.core", path);

        var ledger = (await LedgerAsync())!;
        Assert.Equal(AudioPlaybackStatus.Accepted, ledger.Status);
        Assert.Equal(1, ledger.Attempts);
        Assert.Equal(_seed.Boat.Id, ledger.BoatId);

        // GPS lặp trong vùng, ra rồi vào lại: không phát thêm lần nào (BR-EXP-05)
        await GpsAsync(PoiLat); await GpsAsync(PoiLat);
        await GpsAsync(FarLat); await GpsAsync(PoiLat);
        await Task.Delay(300);
        Assert.Single(player.Triggers);

        // tải MP3 bằng đường dẫn trong event (JWT Captain đúng tàu)
        using var http = Http(_captain);
        var audio = await http.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, audio.StatusCode);
        Assert.Equal("audio/mpeg", audio.Content.Headers.ContentType!.MediaType);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, await audio.Content.ReadAsByteArrayAsync());

        // player báo trạng thái: Started → Completed (idempotent)
        Assert.True(await player.Hub.InvokeAsync<bool>("ReportPlaybackState", requestId, "Started", null));
        Assert.False(await player.Hub.InvokeAsync<bool>("ReportPlaybackState", requestId, "Started", null));
        Assert.True(await player.Hub.InvokeAsync<bool>("ReportPlaybackState", requestId, "Completed", null));
        Assert.Equal(AudioPlaybackStatus.Completed, (await LedgerAsync())!.Status);
    }

    [SqlServerDomainFact]
    public async Task PlayerOfflineAtTrigger_DoesNotBurnAttempts_AndDeliversAfterReconnectOnNextFix()
    {
        await GpsAsync(PoiLat);
        await GpsAsync(PoiLat);
        await GpsAsync(PoiLat);
        var offline = (await LedgerAsync())!;
        Assert.Equal(AudioPlaybackStatus.Released, offline.Status);
        Assert.Equal(0, offline.Attempts);       // chưa gửi lần nào nên không tính lượt thử

        var player = await ConnectPlayerAsync(_captain);
        await GpsAsync(PoiLat);
        Assert.Single(player.Triggers);
        Assert.Equal(AudioPlaybackStatus.Accepted, (await LedgerAsync())!.Status);
    }

    [SqlServerDomainFact]
    public async Task PlayerThatNeverAcks_IsRetriedWithTheSameRequestId_ThenStopsAfterMaxAttempts()
    {
        var player = await ConnectPlayerAsync(_captain, autoAccept: false);
        await GpsAsync(PoiLat);                       // lần 1: không ACK sau 1s
        Assert.Equal(AudioPlaybackStatus.Released, (await LedgerAsync())!.Status);
        await GpsAsync(PoiLat);                       // lần 2: vẫn không ACK → hết lượt (MaxDispatchAttempts=2)
        Assert.Equal(AudioPlaybackStatus.Failed, (await LedgerAsync())!.Status);
        await GpsAsync(PoiLat);                       // không gửi thêm
        Assert.Equal(2, player.Triggers.Count);
        Assert.Single(player.Triggers.Select(x => x.GetProperty("requestId").GetGuid()).Distinct());
    }

    [SqlServerDomainFact]
    public async Task PlayerAckingTheRetry_RecoversTheLostAck()
    {
        var player = await ConnectPlayerAsync(_captain, autoAccept: false);
        await GpsAsync(PoiLat);
        player.AutoAccept = true;
        await GpsAsync(PoiLat);
        Assert.Equal(AudioPlaybackStatus.Accepted, (await LedgerAsync())!.Status);
        Assert.Equal(2, player.Triggers.Count);
    }

    // ───────────────────────── isolation ─────────────────────────

    [SqlServerDomainFact]
    public async Task OtherBoatsPlayer_NeverReceivesNorFetchesAudio_AndNonCaptainsAreRejected()
    {
        var mine = await ConnectPlayerAsync(_captain);
        var other = await ConnectPlayerAsync(_otherCaptain);
        await GpsAsync(PoiLat);
        Assert.Single(mine.Triggers);
        await Task.Delay(300);
        Assert.Empty(other.Triggers);

        var requestId = mine.Triggers.Single().GetProperty("requestId").GetGuid();
        var path = $"/api/v1/onboard-audio/requests/{requestId}/audio";
        Assert.Equal(HttpStatusCode.NotFound, (await Http(_otherCaptain).GetAsync(path)).StatusCode);   // không lộ sự tồn tại
        Assert.Equal(HttpStatusCode.Forbidden, (await Http(Guid.NewGuid(), "Passenger").GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Http(Guid.NewGuid(), "Staff").GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Http().GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Http(_otherCaptain).PostAsync($"/api/v1/onboard-audio/requests/{requestId}/accept", null)).StatusCode);
        await Assert.ThrowsAsync<HubException>(() => other.Hub.InvokeAsync<bool>("AcceptPlayback", requestId));

        // Passenger/Guest không kết nối được hub onboard
        var passenger = new HubConnectionBuilder().WithUrl(_app.Urls.Single() + "/hubs/onboard-audio",
            o => o.AccessTokenProvider = () => Task.FromResult<string?>(Jwt(Guid.NewGuid(), "Passenger"))).Build();
        _connections.Add(passenger);
        await Assert.ThrowsAnyAsync<Exception>(() => passenger.StartAsync());
        var anonymous = new HubConnectionBuilder().WithUrl(_app.Urls.Single() + "/hubs/onboard-audio").Build();
        _connections.Add(anonymous);
        await Assert.ThrowsAnyAsync<Exception>(() => anonymous.StartAsync());
    }

    [SqlServerDomainFact]
    public async Task CaptainReassignedAfterConnecting_NoLongerReceivesTriggers_AndCannotFetchAudio()
    {
        var oldPlayer = await ConnectPlayerAsync(_captain);
        await GpsAsync(PoiLat);
        var requestId = oldPlayer.Triggers.Single().GetProperty("requestId").GetGuid();

        // Admin đổi Captain của tàu: kết nối cũ không còn quyền dù vẫn đang mở
        var newCaptain = Guid.NewGuid();
        await using (var c = Context())
        {
            (await c.Boats.SingleAsync(b => b.Id == _seed.Boat.Id)).CaptainUserId = newCaptain;
            await c.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await Http(_captain).GetAsync($"/api/v1/onboard-audio/requests/{requestId}/audio")).StatusCode);

        // lệnh mới ở POI khác không tới kết nối cũ (bị thu hồi khỏi group khi dispatch)
        await using (var c = Context())
        {
            var poi2 = new PointOfInterest { Name = "POI 2", Latitude = PoiLat + 0.01m, Longitude = PoiLng, IsActive = true };
            c.PointsOfInterest.Add(poi2);
            c.RoutePois.Add(new RoutePoi { RouteId = _seed.Route.Id, PoiId = poi2.Id, DisplayOrder = 2, TriggerRadiusM = 150, IsActive = true });
            c.AudioGuides.Add(new AudioGuide { PoiId = poi2.Id, LanguageCode = "vi", IsPublished = true,
                AudioAssetUri = "https://example.blob.core.windows.net/audio/audio-assets/tts/20261108/poi.mp3" });
            await c.SaveChangesAsync();
        }
        // GPS do Captain MỚI gửi (hợp lệ) để chắc chắn dispatch thật sự chạy; kết nối cũ không được nhận gì.
        using var http = Http(newCaptain);
        var gps = await http.PostAsJsonAsync($"/api/v1/tracking/boats/{_seed.Boat.Id}/positions",
            new { latitude = PoiLat + 0.01m, longitude = PoiLng, speedKnots = 5m, observedAt = DateTimeOffset.UtcNow.AddSeconds(1) });
        Assert.Equal(HttpStatusCode.OK, gps.StatusCode);
        await Task.Delay(300);
        Assert.Single(oldPlayer.Triggers);
    }

    [SqlServerDomainFact]
    public async Task StartedReportedImmediatelyAfterAck_IsNeverRejectedByAReservedLedger()
    {
        var player = await ConnectPlayerAsync(_captain);
        player.StartImmediately = true;
        await GpsAsync(PoiLat);
        await WaitAsync(() => !player.StartResults.IsEmpty);
        Assert.True(player.StartResults.Single());
        Assert.Equal(AudioPlaybackStatus.Started, (await LedgerAsync())!.Status);
    }

    [SqlServerDomainFact]
    public async Task AckPersistsAcceptedBeforeSignalling_SoImmediateStartedIsApplied_Deterministic()
    {
        // Ép đúng chuỗi xen kẽ: sổ cái đang Reserved (dispatcher chưa kịp MarkAccepted), player ACK rồi Started ngay.
        var requestId = Guid.NewGuid();
        await using (var c = Context())
        {
            c.AudioPlaybackRequests.Add(new AudioPlaybackRequest { Id = requestId, TripId = _seed.Trip.Id, BoatId = _seed.Boat.Id,
                RoutePoiKey = Guid.NewGuid(), VisitKey = "", Status = AudioPlaybackStatus.Reserved, Attempts = 1, ReservedAt = DateTimeOffset.UtcNow });
            await c.SaveChangesAsync();
        }
        var player = await ConnectPlayerAsync(_captain, autoAccept: false);
        Assert.True(await player.Hub.InvokeAsync<bool>("AcceptPlayback", requestId));
        Assert.True(await player.Hub.InvokeAsync<bool>("ReportPlaybackState", requestId, "Started", null));
        await using var check = Context();
        Assert.Equal(AudioPlaybackStatus.Started, (await check.AudioPlaybackRequests.AsNoTracking().SingleAsync(x => x.Id == requestId)).Status);
    }

    [SqlServerDomainFact]
    public async Task RepeatedAck_IsIdempotent_DoesNotRegressOrAffectOtherRequests()
    {
        var player = await ConnectPlayerAsync(_captain);
        await GpsAsync(PoiLat);
        var requestId = player.Triggers.Single().GetProperty("requestId").GetGuid();
        Assert.True(await player.Hub.InvokeAsync<bool>("AcceptPlayback", requestId));
        Assert.True(await player.Hub.InvokeAsync<bool>("ReportPlaybackState", requestId, "Started", null));
        // ACK lặp sau khi đã Started: vẫn true, không lùi trạng thái, không gửi lại lệnh
        Assert.True(await player.Hub.InvokeAsync<bool>("AcceptPlayback", requestId));
        Assert.Equal(AudioPlaybackStatus.Started, (await LedgerAsync())!.Status);
        Assert.Single(player.Triggers);
        // ACK cho request không tồn tại: không ảnh hưởng gì
        await Assert.ThrowsAsync<HubException>(() => player.Hub.InvokeAsync<bool>("AcceptPlayback", Guid.NewGuid()));
        Assert.Equal(AudioPlaybackStatus.Started, (await LedgerAsync())!.Status);
    }

    [SqlServerDomainFact]
    public async Task TwoPlayersOnOneBoat_OnlyTheNewestActivePlayerGetsTheTrigger()
    {
        var first = await ConnectPlayerAsync(_captain);
        var second = await ConnectPlayerAsync(_captain);
        await GpsAsync(PoiLat);
        Assert.Empty(first.Triggers);
        Assert.Single(second.Triggers);
        var requestId = second.Triggers.Single().GetProperty("requestId").GetGuid();
        // kết nối chờ không thể ACK thay cho player hoạt động
        Assert.False(await first.Hub.InvokeAsync<bool>("AcceptPlayback", requestId));
        Assert.Equal(AudioPlaybackStatus.Accepted, (await LedgerAsync())!.Status);
    }

    [SqlServerDomainFact]
    public async Task StaleWorkerAfterLeaseTakeover_CannotDispatch_Accept_OrRelease_TheNewerOwnersReservation()
    {
        // Cùng MỘT RequestId, hai DbContext độc lập, thời điểm điều khiển được.
        var id = AudioRequestIds.For(_seed.Trip.Id, _routePoiId, null);
        var t0 = DateTimeOffset.UtcNow; var lease = TimeSpan.FromSeconds(30);
        await using var a = Context(); await using var b = Context();
        var repoA = new AudioPlaybackRepository(a); var repoB = new AudioPlaybackRepository(b);
        var epochA = await repoA.TryReserveAsync(id, _seed.Trip.Id, _routePoiId, "", t0, lease, 3, default);
        Assert.Equal(1, epochA);
        Assert.Null(await repoB.TryReserveAsync(id, _seed.Trip.Id, _routePoiId, "", t0.AddSeconds(10), lease, 3, default)); // lease còn
        var epochB = await repoB.TryReserveAsync(id, _seed.Trip.Id, _routePoiId, "", t0.AddSeconds(31), lease, 3, default);   // A đứng im quá lease → B giành lại
        Assert.Equal(2, epochB);

        var req = new AudioTriggerRequest { RequestId = id, TripId = _seed.Trip.Id, BoatId = _seed.Boat.Id, AudioAssetUri = "x.mp3" };
        // A tỉnh dậy ngay sau khi B làm mới ReservedAt: dòng vẫn Reserved và "mới" nhưng A không còn là chủ
        Assert.False(await repoA.RecordDispatchAsync(req, epochA!.Value, t0.AddSeconds(32), default));
        Assert.False(await repoA.MarkAcceptedAsync(id, epochA, t0.AddSeconds(32), default));
        await repoA.ReleaseAsync(id, epochA.Value, "stale", 3, default);
        await using (var c = Context())
        {
            var row = await c.AudioPlaybackRequests.AsNoTracking().SingleAsync(x => x.Id == id);
            Assert.Equal(AudioPlaybackStatus.Reserved, row.Status);   // reservation của B còn nguyên
            Assert.Equal(2, row.ReservationEpoch);
            Assert.Equal(0, row.Attempts);                            // A không ghi được lượt gửi
        }
        // B (chủ thật) dispatch / accept bình thường
        Assert.True(await repoB.RecordDispatchAsync(req, epochB!.Value, t0.AddSeconds(33), default));
        Assert.True(await repoB.MarkAcceptedAsync(id, epochB, t0.AddSeconds(33), default));
        await using var d = Context();
        var final = await d.AudioPlaybackRequests.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal(AudioPlaybackStatus.Accepted, final.Status);
        Assert.Equal(1, final.Attempts);
    }

    [SqlServerDomainFact]
    public async Task RecordedDispatch_RenewsTheLease_SoTakeoverCannotRaceTheSend()
    {
        var id = AudioRequestIds.For(_seed.Trip.Id, _routePoiId, null);
        var t0 = DateTimeOffset.UtcNow; var lease = TimeSpan.FromSeconds(30);
        await using var a = Context(); await using var b = Context();
        var repoA = new AudioPlaybackRepository(a); var repoB = new AudioPlaybackRepository(b);
        var epoch = await repoA.TryReserveAsync(id, _seed.Trip.Id, _routePoiId, "", t0, lease, 3, default);
        var req = new AudioTriggerRequest { RequestId = id, TripId = _seed.Trip.Id, BoatId = _seed.Boat.Id, AudioAssetUri = "x.mp3" };
        Assert.True(await repoA.RecordDispatchAsync(req, epoch!.Value, t0.AddSeconds(25), default));   // ghi nhận gần cuối lease cũ
        Assert.Null(await repoB.TryReserveAsync(id, _seed.Trip.Id, _routePoiId, "", t0.AddSeconds(31), lease, 3, default)); // lease đã gia hạn
        Assert.NotNull(await repoB.TryReserveAsync(id, _seed.Trip.Id, _routePoiId, "", t0.AddSeconds(56), lease, 3, default));
    }

    [SqlServerDomainFact]
    public async Task LateAckOfAnEarlierDelivery_AcceptsTheRequest_AndTheNewOwnerCannotDispatchAgain()
    {
        // ACK là mức request: player đã nhận RequestId này (dù từ lần gửi cũ). Chủ mới không được gửi lần nữa.
        var id = AudioRequestIds.For(_seed.Trip.Id, _routePoiId, null);
        var t0 = DateTimeOffset.UtcNow; var lease = TimeSpan.FromSeconds(30);
        await using var a = Context(); await using var b = Context();
        var repoA = new AudioPlaybackRepository(a); var repoB = new AudioPlaybackRepository(b);
        var epochA = await repoA.TryReserveAsync(id, _seed.Trip.Id, _routePoiId, "", t0, lease, 3, default);
        var epochB = await repoB.TryReserveAsync(id, _seed.Trip.Id, _routePoiId, "", t0.AddSeconds(31), lease, 3, default);
        Assert.True(await repoB.MarkAcceptedAsync(id, null, t0.AddSeconds(32), default));        // ACK muộn của player
        var req = new AudioTriggerRequest { RequestId = id, TripId = _seed.Trip.Id, BoatId = _seed.Boat.Id, AudioAssetUri = "x.mp3" };
        Assert.False(await repoB.RecordDispatchAsync(req, epochB!.Value, t0.AddSeconds(33), default)); // không gửi trùng
        Assert.False(await repoA.RecordDispatchAsync(req, epochA!.Value, t0.AddSeconds(33), default));
    }

    [SqlServerDomainFact]
    public async Task ConcurrentTakeovers_OfOneExpiredReservation_HaveExactlyOneWinnerWithAUniqueEpoch()
    {
        var id = AudioRequestIds.For(_seed.Trip.Id, _routePoiId, null);
        var t0 = DateTimeOffset.UtcNow.AddMinutes(-5); var lease = TimeSpan.FromSeconds(30);
        await using (var c = Context()) Assert.Equal(1, await new AudioPlaybackRepository(c).TryReserveAsync(id, _seed.Trip.Id, _routePoiId, "", t0, lease, 3, default));
        async Task<int?> Take() { await using var c = Context(); return await new AudioPlaybackRepository(c).TryReserveAsync(id, _seed.Trip.Id, _routePoiId, "", DateTimeOffset.UtcNow, lease, 3, default); }
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(Take)));
        Assert.Single(results, x => x is not null);
        Assert.Equal(2, results.Single(x => x is not null));
    }

    [SqlServerDomainFact]
    public async Task StaleEvaluationThroughTheDedupService_CannotReleaseOrMarkTheNewerOwner()
    {
        var tripId = _seed.Trip.Id; var id = AudioRequestIds.For(tripId, _routePoiId, null);
        await using var ca = Context(); await using var cb = Context();
        var opts = Options.Create(new SightseeingAudioOptions());
        var evalA = new SqlPoiTriggerDeduplicationService(new AudioPlaybackRepository(ca), opts);
        var evalB = new SqlPoiTriggerDeduplicationService(new AudioPlaybackRepository(cb), opts);
        Assert.True(await evalA.TryReserveAsync(tripId, _routePoiId, null));
        await using (var c = Context())   // A treo quá lease (dịch ReservedAt lùi 2 phút)
        {
            var row = await c.AudioPlaybackRequests.SingleAsync(x => x.Id == id);
            row.ReservedAt = DateTimeOffset.UtcNow.AddMinutes(-2); await c.SaveChangesAsync();
        }
        Assert.True(await evalB.TryReserveAsync(tripId, _routePoiId, null));
        Assert.Equal(2, evalB.GetReservationEpoch(tripId, _routePoiId, null));

        await evalA.ReleaseReservationAsync(tripId, _routePoiId, null);   // A đường lỗi: không được nhả của B
        await evalA.MarkAsTriggeredAsync(tripId, _routePoiId, null);      // A không được đánh dấu Accepted thay B
        await using (var c = Context())
            Assert.Equal(AudioPlaybackStatus.Reserved, (await c.AudioPlaybackRequests.AsNoTracking().SingleAsync(x => x.Id == id)).Status);
        await evalB.MarkAsTriggeredAsync(tripId, _routePoiId, null);
        await using (var c = Context())
            Assert.Equal(AudioPlaybackStatus.Accepted, (await c.AudioPlaybackRequests.AsNoTracking().SingleAsync(x => x.Id == id)).Status);
    }

    [SqlServerDomainFact]
    public async Task CaptainWithoutABoat_CannotUseThePlayerContract()
    {
        var stranger = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Forbidden, (await Http(stranger).GetAsync("/api/v1/onboard-audio/state")).StatusCode);
    }

    // ───────────────────────── operational state (BR-EXP-05/06) ─────────────────────────

    [SqlServerDomainFact]
    public async Task SuspendedTrip_BlocksTriggerAndAudioFetch_AndResumeWorks()
    {
        var player = await ConnectPlayerAsync(_captain);
        await GpsAsync(PoiLat);
        var requestId = player.Triggers.Single().GetProperty("requestId").GetGuid();

        await using (var c = Context())
        {
            (await c.Trips.SingleAsync(t => t.Id == _seed.Trip.Id)).Status = TripStatus.Suspended;
            await c.SaveChangesAsync();
        }
        var state = await Http(_captain).GetFromJsonAsync<JsonElement>("/api/v1/onboard-audio/state");
        Assert.False(state.GetProperty("playbackAllowed").GetBoolean());
        Assert.Equal("TripNotSuspended", state.GetProperty("reason").GetString());
        Assert.Equal(HttpStatusCode.Conflict,
            (await Http(_captain).GetAsync($"/api/v1/onboard-audio/requests/{requestId}/audio")).StatusCode);

        // tín hiệu pause tới player qua publisher vận hành (đường đổi trạng thái chuyến thật dùng publisher này)
        using (var scope = _app.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IOperationsAlertPublisher>().PublishTripStatusChangedAsync(
                new TripStatusChangedMessage(_seed.Trip.Id, _seed.Boat.Id, "Suspended", "EnRoute", "Suspend", DateTimeOffset.UtcNow));
        await WaitAsync(() => !player.Controls.IsEmpty);
        Assert.Equal("pause", player.Controls.Single().GetProperty("action").GetString());
        using (var scope = _app.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IOperationsAlertPublisher>().PublishTripStatusChangedAsync(
                new TripStatusChangedMessage(_seed.Trip.Id, _seed.Boat.Id, "EnRoute", "Suspended", "Resume", DateTimeOffset.UtcNow));
        await WaitAsync(() => player.Controls.Count == 2);
        Assert.Equal("recheck", player.Controls.Last().GetProperty("action").GetString());   // không bao giờ tự "resume"
    }

    [SqlServerDomainFact]
    public async Task SuspendedTrip_NeverDispatches_AndOpenSafetyIncidentBlocks()
    {
        var player = await ConnectPlayerAsync(_captain);
        await using (var c = Context())
        {
            (await c.Trips.SingleAsync(t => t.Id == _seed.Trip.Id)).Status = TripStatus.Suspended;
            await c.SaveChangesAsync();
        }
        await GpsAsync(PoiLat);
        Assert.Empty(player.Triggers);
        Assert.Null(await LedgerAsync());

        await using (var c = Context())
        {
            (await c.Trips.SingleAsync(t => t.Id == _seed.Trip.Id)).Status = TripStatus.EnRoute;
            var incident = new Incident
            {
                IncidentType = IncidentConstants.Types.Safety, Status = IncidentConstants.Statuses.Open,
                Source = "Captain", ScopeType = "Trip", Description = "SOS", OccurredAt = DateTimeOffset.UtcNow
            };
            c.Incidents.Add(incident);
            c.IncidentTrips.Add(new IncidentTrip { IncidentId = incident.Id, TripId = _seed.Trip.Id });
            await c.SaveChangesAsync();
        }
        await GpsAsync(PoiLat);
        Assert.Empty(player.Triggers);
        var state = await Http(_captain).GetFromJsonAsync<JsonElement>("/api/v1/onboard-audio/state");
        Assert.Equal("SafetyAnnouncementActive", state.GetProperty("reason").GetString());
    }

    [SqlServerDomainFact]
    public async Task StaleGps_DoesNotTrigger_ButFreshFixDoes()
    {
        var player = await ConnectPlayerAsync(_captain);
        await GpsAsync(PoiLat, DateTimeOffset.UtcNow.AddMinutes(-10));
        Assert.Empty(player.Triggers);
        Assert.Null(await LedgerAsync());
        await GpsAsync(PoiLat);
        Assert.Single(player.Triggers);
    }

    // ───────────────────────── content failures ─────────────────────────

    [SqlServerDomainFact]
    public async Task MissingOrUnavailableAudio_IsNotDispatched_AndRecoversWithoutBurningAttempts()
    {
        var player = await ConnectPlayerAsync(_captain);
        _storage.Files.Remove("audio-assets/tts/20261108/poi.mp3");
        await GpsAsync(PoiLat);
        _storage.Unavailable = true;
        await GpsAsync(PoiLat);
        Assert.Empty(player.Triggers);
        Assert.Equal(0, (await LedgerAsync())!.Attempts);

        _storage.Unavailable = false;
        _storage.Files["audio-assets/tts/20261108/poi.mp3"] = [9];
        await GpsAsync(PoiLat);
        Assert.Single(player.Triggers);
    }

    [SqlServerDomainFact]
    public async Task UnpublishedAudio_IsNeverTriggered()
    {
        await using (var c = Context())
        {
            (await c.AudioGuides.SingleAsync()).IsPublished = false;
            await c.SaveChangesAsync();
        }
        var player = await ConnectPlayerAsync(_captain);
        await GpsAsync(PoiLat);
        Assert.Empty(player.Triggers);
        Assert.Null(await LedgerAsync());
    }

    // ───────────────────────── SQL dedup & ledger ─────────────────────────

    [SqlServerDomainFact]
    public async Task ConcurrentReservations_HaveExactlyOneWinner_AndSurviveANewServiceInstance()
    {
        var tripId = _seed.Trip.Id;
        async Task<bool> Reserve()
        {
            await using var c = Context();
            var svc = new SqlPoiTriggerDeduplicationService(new AudioPlaybackRepository(c), Options.Create(new SightseeingAudioOptions()));
            var won = await svc.TryReserveAsync(tripId, _routePoiId, null);
            if (won) await svc.MarkAsTriggeredAsync(tripId, _routePoiId, null); // chỉ chủ reservation (đúng epoch) mới đánh dấu được
            return won;
        }
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(Reserve)));
        Assert.Equal(1, results.Count(x => x));

        await using var c1 = Context();
        var fresh = new SqlPoiTriggerDeduplicationService(new AudioPlaybackRepository(c1), Options.Create(new SightseeingAudioOptions()));
        Assert.False(await fresh.TryReserveAsync(tripId, _routePoiId, null));          // lượt ghé đã có chủ
        await using var c2 = Context();                                                  // "sau restart": instance mới, DB cũ
        var restarted = new SqlPoiTriggerDeduplicationService(new AudioPlaybackRepository(c2), Options.Create(new SightseeingAudioOptions()));
        Assert.True(await restarted.HasBeenTriggeredAsync(tripId, _routePoiId, null));
        Assert.False(await restarted.TryReserveAsync(tripId, _routePoiId, null));
        // lượt ghé khác (hành trình vòng) vẫn được phép
        Assert.True(await restarted.TryReserveAsync(tripId, _routePoiId, "lap-2"));
    }

    [SqlServerDomainFact]
    public async Task AbandonedReservation_IsTakenOverOnlyAfterTheLeaseExpires()
    {
        await using var c = Context();
        var repo = new AudioPlaybackRepository(c);
        var id = AudioRequestIds.For(_seed.Trip.Id, _routePoiId, null);
        var t0 = DateTimeOffset.UtcNow;
        Assert.NotNull(await repo.TryReserveAsync(id, _seed.Trip.Id, _routePoiId, "", t0, TimeSpan.FromSeconds(30), 3, default));
        Assert.Null(await repo.TryReserveAsync(id, _seed.Trip.Id, _routePoiId, "", t0.AddSeconds(10), TimeSpan.FromSeconds(30), 3, default));
        Assert.NotNull(await repo.TryReserveAsync(id, _seed.Trip.Id, _routePoiId, "", t0.AddSeconds(31), TimeSpan.FromSeconds(30), 3, default));
    }

    // ───────────────────────── fakes ─────────────────────────

    private sealed class FakeStorage : IAudioStorageService
    {
        public readonly Dictionary<string, byte[]> Files = new();
        public bool Unavailable;
        private static string Key(string id) => id.Contains("/audio-assets/") ? id[(id.IndexOf("/audio-assets/", StringComparison.Ordinal) + 1)..] : id;
        public Task<AudioStorageResult> StoreAudioAsync(StoreAudioRequest command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(string assetIdentifier, CancellationToken cancellationToken = default) =>
            Unavailable ? throw new AudioStorageException("fake storage down") : Task.FromResult(Files.ContainsKey(Key(assetIdentifier)));
        public Task<StoredAudio?> ReadAudioAsync(string assetKey, CancellationToken cancellationToken = default) =>
            Unavailable ? throw new AudioStorageException("fake storage down")
                : Task.FromResult(Files.TryGetValue(assetKey, out var data) ? new StoredAudio(data, "audio/mpeg") : null);
    }
}

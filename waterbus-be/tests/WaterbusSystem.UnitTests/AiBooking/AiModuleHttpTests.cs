using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Moq;
using WaterbusSystem.Application;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.AiBooking;
using WaterbusSystem.Application.DTOs.Requests.SightseeingAudio;
using WaterbusSystem.Application.DTOs.Responses.AiBooking;
using WaterbusSystem.Application.DTOs.Responses.SightseeingAudio;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Infrastructure;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.UnitTests.Services.AudioStorage.Fakes;
using WaterbusSystem.UnitTests.Services.Tts;
using WaterbusSystem.WebApi.Controllers;
using WaterbusSystem.WebApi.Filters;
using WaterbusSystem.WebApi.Middlewares;
using Xunit;

namespace WaterbusSystem.UnitTests.AiBooking;

/// <summary>Kiểm tra contract HTTP và wiring service; provider giả chỉ tồn tại trong test.</summary>
public sealed class AiModuleHttpTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private readonly string _jwtSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private readonly Mock<IAiBookingAssistantService> _assistant = new();
    private Guid _poiId;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=127.0.0.1;Database=NotUsed;Integrated Security=True",
            ["JwtSettings:Secret"] = _jwtSecret
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddApplicationServices();
        builder.Services.AddInfrastructureServices(builder.Configuration);
        builder.Services.RemoveAll<IHostedService>();
        builder.Services.RemoveAll<ApplicationDbContext>();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        builder.Services.AddScoped(_ => new ApplicationDbContext(options, null));
        builder.Services.AddSingleton<ITextToSpeechService, FakeTextToSpeechService>();
        builder.Services.AddSingleton<IAudioStorageService>(new FakeAudioStorageService("https://example.blob.core.windows.net/private"));
        builder.Services.AddSingleton(_assistant.Object);
        builder.Services.AddScoped<ValidateRequestFilter>();
        builder.Services.AddControllers().AddApplicationPart(typeof(AudioGuidesController).Assembly);
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddRateLimiter(o => o.AddPolicy("AiAssistantPolicy", _ => RateLimitPartition.GetNoLimiter("ai-tests")));
        _app = builder.Build();
        _app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
        _app.UseSwagger();
        _app.UseRateLimiter();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapControllers();
        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var poi = new PointOfInterest { Name = "Bến Nhà Rồng" };
            db.PointsOfInterest.Add(poi);
            await db.SaveChangesAsync();
            _poiId = poi.Id;
        }
        await _app.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_app.Urls.Single()) };
    }

    private void SignIn(string role)
    {
        var token = new JwtSecurityToken("WaterbusSystemApi", "WaterbusClients",
            [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, role)],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret)), SecurityAlgorithms.HmacSha256));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
    }

    [Fact]
    public async Task AudioRoutesPreserveAuthorizationValidationAndPublishedMetadata()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/audioguides/pois")).StatusCode);
        SignIn("Passenger");
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync("/api/v1/audioguides/pois")).StatusCode);
        SignIn("Admin");
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync("/api/v1/audioguides/pois?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync("/api/v1/texttospeech/audio?assetKey=../bad.mp3")).StatusCode);
        var invalid = await _client.PostAsJsonAsync("/api/v1/texttospeech/generate", new SynthesizeAudioRequest(""));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var problem = JsonDocument.Parse(await invalid.Content.ReadAsStringAsync());
        Assert.True(problem.RootElement.TryGetProperty("errors", out _));
        Assert.True(problem.RootElement.TryGetProperty("traceId", out _));
        var created = await _client.PostAsJsonAsync("/api/v1/texttospeech/generate", new SynthesizeAudioRequest("Đây là Bến Nhà Rồng."));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var audio = (await created.Content.ReadFromJsonAsync<SynthesizeAudioResponse>())!;
        var download = await _client.GetAsync($"/api/v1/texttospeech/audio?assetKey={Uri.EscapeDataString(audio.AssetKey)}");
        Assert.Equal("audio/mpeg", download.Content.Headers.ContentType!.MediaType);
        Assert.Equal(audio.ByteLength, (await download.Content.ReadAsByteArrayAsync()).LongLength);
        var publishRequest = new PublishAudioGuideRequest(_poiId, audio.AudioUrl, audio.DurationSeconds);
        var published = await _client.PostAsJsonAsync("/api/v1/audioguides/publish", publishRequest);
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        var first = (await published.Content.ReadFromJsonAsync<PublishedAudioGuideResponse>())!;
        var repeated = await _client.PostAsJsonAsync("/api/v1/audioguides/publish", publishRequest);
        Assert.Equal(first.AudioGuideId, (await repeated.Content.ReadFromJsonAsync<PublishedAudioGuideResponse>())!.AudioGuideId);
        var page = (await _client.GetFromJsonAsync<AudioGuidePoisPageResponse>("/api/v1/audioguides/pois"))!;
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
        Assert.Equal(audio.AudioUrl, Assert.Single(page.Items).AudioAssetUri);
        Assert.True(page.Items[0].IsPublished);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/v1/texttospeech/audio?assetKey=audio-assets/missing.mp3")).StatusCode);
    }

    [Fact]
    public async Task ChatPreservesPublicContractAndRejectsInvalidInputBeforeCallingService()
    {
        var invalid = await _client.PostAsJsonAsync("/api/v1/aiassistant/chat", new AiBookingChatRequest { Message = "" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        _assistant.Verify(s => s.ProcessChatAsync(It.IsAny<AiBookingChatRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _assistant.Setup(s => s.ProcessChatAsync(It.IsAny<AiBookingChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiBookingChatResponse("No matching trips.", [], []));
        var response = await _client.PostAsJsonAsync("/api/v1/aiassistant/chat", new AiBookingChatRequest { Message = "Any trips?", LanguageCode = "en" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("No matching trips.", json.RootElement.GetProperty("textAnswer").GetString());
        Assert.Equal(JsonValueKind.Array, json.RootElement.GetProperty("cards").ValueKind);
        _assistant.Verify(s => s.ProcessChatAsync(It.Is<AiBookingChatRequest>(r => r.LanguageCode == "en" && r.Message == "Any trips?"), It.IsAny<CancellationToken>()), Times.Once);
        using var swagger = JsonDocument.Parse(await _client.GetStringAsync("/swagger/v1/swagger.json"));
        foreach (var route in new[] { "/api/v1/AudioGuides/pois", "/api/v1/AudioGuides/publish", "/api/v1/TextToSpeech/generate", "/api/v1/TextToSpeech/audio", "/api/v1/AiAssistant/chat" })
            Assert.True(swagger.RootElement.GetProperty("paths").TryGetProperty(route, out _));
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_app is not null) await _app.DisposeAsync();
    }
}

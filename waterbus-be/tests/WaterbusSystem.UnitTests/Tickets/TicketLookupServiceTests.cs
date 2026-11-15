using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.Tickets;
using WaterbusSystem.Application.DTOs.Responses.Tickets;
using WaterbusSystem.Application.Features.Tickets;
using WaterbusSystem.Application.Features.Tickets.Services;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Services.TicketLookup;
using Xunit;

namespace WaterbusSystem.UnitTests.Tickets;

/// <summary>Tra cứu vé bằng mã đặt chỗ + email + OTP: gửi OTP, xác minh OTP, chống dò và chống thử bừa.</summary>
public class TicketLookupServiceTests
{
    private const string Email = "buyer@example.com";
    private const string Code = "WB20261114101530ABCDEF";

    /// <summary>Kho OTP trong bộ nhớ, giữ đúng ngữ nghĩa của bản Redis (nguyên tử, dùng một lần, giới hạn lần thử/gửi).</summary>
    private sealed class FakeStore : ITicketLookupOtpStore
    {
        public readonly Dictionary<string, (string Hash, int Attempts)> Otps = new();
        public readonly HashSet<string> Cooling = new();
        public bool Unavailable;
        public bool DenySend;

        public Task<bool> TryReserveSendAsync(string identity, TimeSpan cooldown, int maxPerHour, CancellationToken ct)
        {
            if (Unavailable) throw new InvalidOperationException("down");
            if (DenySend || !Cooling.Add(identity)) return Task.FromResult(false);
            return Task.FromResult(true);
        }

        public Task SaveAsync(string identity, string otpHash, TimeSpan ttl, CancellationToken ct)
        {
            if (Unavailable) throw new InvalidOperationException("down");
            Otps[identity] = (otpHash, 0);
            return Task.CompletedTask;
        }

        public Task<OtpAttempt> RegisterAttemptAsync(string identity, int maxAttempts, CancellationToken ct)
        {
            if (Unavailable) throw new InvalidOperationException("down");
            if (!Otps.TryGetValue(identity, out var entry)) return Task.FromResult(new OtpAttempt(null, 0));
            var attempts = entry.Attempts + 1;
            if (attempts > maxAttempts) { Otps.Remove(identity); return Task.FromResult(new OtpAttempt(null, attempts)); }
            Otps[identity] = (entry.Hash, attempts);
            return Task.FromResult(new OtpAttempt(entry.Hash, attempts));
        }

        public Task<bool> RemoveAsync(string identity, CancellationToken ct) => Task.FromResult(Otps.Remove(identity));
    }

    private sealed class CapturingMailer : ITicketLookupOtpMailer
    {
        public readonly List<(string Email, string Name, string Otp)> Sent = new();
        public void Enqueue(string toEmail, string toName, string otp, TimeSpan validFor) => Sent.Add((toEmail, toName, otp));
    }

    private static IConfiguration Config() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["JwtSettings:Secret"] = new string('k', 48) }).Build();

    private static TicketLookupBookingData Booking(BookingStatus status, int ticketCount = 1) => new(
        Code, "PUB-1", Guid.NewGuid(), 1, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, status,
        "Bach Dang - Linh Dong", DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddHours(1), "Scheduled",
        "Bach Dang", "Linh Dong",
        Enumerable.Range(1, ticketCount)
            .Select(i => new TicketLookupTicketDto($"TK{i}", "Nguyen Van A", $"A{i}", "Standard", "Valid", "NotCheckedIn", true)).ToList());

    private sealed class Harness
    {
        public readonly FakeStore Store = new();
        public readonly CapturingMailer Mailer = new();
        public readonly Mock<ITicketLookupRepository> Repo = new();
        public readonly Mock<IBookingQrSigner> Signer = new();
        public readonly ITicketLookupHasher Hasher = new HmacTicketLookupHasher(Config());
        public readonly TicketLookupOptions Options = new();
        public TicketLookupService Sut { get; private set; } = null!;

        public Harness(bool matches = true, params TicketLookupBookingData[] bookings)
        {
            var orderId = Guid.NewGuid();
            Repo.Setup(r => r.FindMatchAsync(Code, Email, It.IsAny<CancellationToken>()))
                .ReturnsAsync(matches ? new TicketLookupMatch(orderId, "Nguyen Van A") : null);
            Repo.Setup(r => r.GetBookingsAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(bookings);
            Signer.Setup(s => s.Sign(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<DateTimeOffset>()))
                .Returns(new BookingQrCredential("payload", "RS256", "kid", 1));
            Sut = new TicketLookupService(Repo.Object, Store, Hasher, Mailer, Signer.Object,
                Microsoft.Extensions.Options.Options.Create(Options), NullLogger<TicketLookupService>.Instance);
        }
    }

    private static RequestTicketLookupOtpRequest Ask(string email = Email, string code = Code) => new(email, code);

    // ---------- Xin OTP ----------

    [Fact]
    public async Task RequestOtp_WhenMatching_ShouldSendSixDigitOtpAndStoreOnlyItsHash()
    {
        var h = new Harness();

        await h.Sut.RequestOtpAsync(Ask());

        var sent = h.Mailer.Sent.Should().ContainSingle().Subject;
        sent.Otp.Should().MatchRegex("^[0-9]{6}$");
        sent.Email.Should().Be(Email);
        h.Store.Otps.Should().ContainSingle();
        h.Store.Otps.Single().Value.Hash.Should().NotContain(sent.Otp).And.HaveLength(64);
        h.Store.Otps.Keys.Single().Should().NotContain("example").And.NotContain(Code);
    }

    [Fact]
    public async Task RequestOtp_ShouldReturnTheSameResponseWhetherOrNotTheDetailsMatch()
    {
        var matching = await new Harness(matches: true).Sut.RequestOtpAsync(Ask());
        var unmatched = new Harness(matches: false);
        var notMatching = await unmatched.Sut.RequestOtpAsync(Ask());

        notMatching.Should().Be(matching);
        unmatched.Mailer.Sent.Should().BeEmpty();
        unmatched.Store.Otps.Should().BeEmpty();
    }

    [Fact]
    public async Task RequestOtp_NormalizesEmailCaseAndBookingCodeCase()
    {
        var h = new Harness();

        await h.Sut.RequestOtpAsync(Ask("  Buyer@Example.COM ", " wb20261114101530abcdef "));

        h.Mailer.Sent.Should().ContainSingle();
    }

    [Fact]
    public async Task RequestOtp_WhenResendCooldownOrHourlyLimitDenies_ShouldNotSendAgainButStillRespondTheSame()
    {
        var h = new Harness();
        var first = await h.Sut.RequestOtpAsync(Ask());
        var second = await h.Sut.RequestOtpAsync(Ask()); // cùng cặp trong thời gian chờ

        second.Should().Be(first);
        h.Mailer.Sent.Should().HaveCount(1);
    }

    [Fact]
    public async Task RequestOtp_WhenStoreIsUnavailable_ShouldNotSendAndNotThrow()
    {
        var h = new Harness();
        h.Store.Unavailable = true;

        var act = () => h.Sut.RequestOtpAsync(Ask());

        await act.Should().NotThrowAsync();
        h.Mailer.Sent.Should().BeEmpty();
    }

    // ---------- Xác minh OTP ----------

    private static async Task<string> IssueAsync(Harness h)
    {
        await h.Sut.RequestOtpAsync(Ask());
        return h.Mailer.Sent.Last().Otp;
    }

    private static VerifyTicketLookupOtpRequest Verify(string otp) => new(Email, Code, otp);

    [Fact]
    public async Task VerifyOtp_WithCorrectOtp_ShouldReturnTicketsAndQrOfPaidBooking()
    {
        var h = new Harness(true, Booking(BookingStatus.Confirmed, ticketCount: 2));
        var otp = await IssueAsync(h);

        var response = await h.Sut.VerifyOtpAsync(Verify(otp));

        var booking = response.Bookings.Should().ContainSingle().Subject;
        booking.Tickets.Should().HaveCount(2);
        booking.Qr.Should().NotBeNull();
        h.Store.Otps.Should().BeEmpty();
    }

    [Fact]
    public async Task VerifyOtp_WhenBookingIsNotPaid_ShouldNotIssueQr()
    {
        var h = new Harness(true, Booking(BookingStatus.Pending, ticketCount: 0));
        var otp = await IssueAsync(h);

        var response = await h.Sut.VerifyOtpAsync(Verify(otp));

        response.Bookings.Single().Qr.Should().BeNull();
        h.Signer.Verify(s => s.Sign(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<DateTimeOffset>()), Times.Never);
    }

    [Fact]
    public async Task VerifyOtp_ShouldBeSingleUse()
    {
        var h = new Harness(true, Booking(BookingStatus.Confirmed));
        var otp = await IssueAsync(h);
        await h.Sut.VerifyOtpAsync(Verify(otp));

        var again = () => h.Sut.VerifyOtpAsync(Verify(otp));

        await again.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task VerifyOtp_WithWrongOtp_ShouldThrowTheSameMessageAsExpiredOrMissing()
    {
        var h = new Harness(true, Booking(BookingStatus.Confirmed));
        var otp = await IssueAsync(h);
        var wrong = otp == "000000" ? "111111" : "000000";

        var wrongAct = () => h.Sut.VerifyOtpAsync(Verify(wrong));
        var missingAct = () => new Harness(true).Sut.VerifyOtpAsync(Verify("123456"));

        var wrongMessage = (await wrongAct.Should().ThrowAsync<ForbiddenAccessException>()).Which.Message;
        var missingMessage = (await missingAct.Should().ThrowAsync<ForbiddenAccessException>()).Which.Message;
        wrongMessage.Should().Be(missingMessage);
    }

    [Fact]
    public async Task VerifyOtp_AfterTooManyWrongAttempts_ShouldLockEvenTheCorrectOtp()
    {
        var h = new Harness(true, Booking(BookingStatus.Confirmed));
        var otp = await IssueAsync(h);
        var wrong = otp == "000000" ? "111111" : "000000";

        for (var i = 0; i < h.Options.MaxVerifyAttempts; i++)
        {
            await ((Func<Task>)(() => h.Sut.VerifyOtpAsync(Verify(wrong)))).Should().ThrowAsync<ForbiddenAccessException>();
        }
        var correct = () => h.Sut.VerifyOtpAsync(Verify(otp));

        await correct.Should().ThrowAsync<ForbiddenAccessException>();
        h.Store.Otps.Should().BeEmpty();
    }

    [Fact]
    public async Task VerifyOtp_OtpOfAnotherPair_ShouldNotWork()
    {
        var h = new Harness(true, Booking(BookingStatus.Confirmed));
        var otp = await IssueAsync(h);

        var act = () => h.Sut.VerifyOtpAsync(new VerifyTicketLookupOtpRequest("someone.else@example.com", Code, otp));

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        h.Store.Otps.Should().ContainSingle(); // OTP của cặp thật vẫn còn, chưa bị đốt
    }

    [Fact]
    public async Task VerifyOtp_WhenStoreIsUnavailable_ShouldDenyInsteadOfAccepting()
    {
        var h = new Harness(true, Booking(BookingStatus.Confirmed));
        h.Store.Unavailable = true;

        var act = () => h.Sut.VerifyOtpAsync(Verify("123456"));

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task VerifyOtp_WhenOrderNoLongerMatches_ShouldDeny()
    {
        var h = new Harness(true, Booking(BookingStatus.Confirmed));
        var otp = await IssueAsync(h);
        h.Repo.Setup(r => r.FindMatchAsync(Code, Email, It.IsAny<CancellationToken>())).ReturnsAsync((TicketLookupMatch?)null);

        var act = () => h.Sut.VerifyOtpAsync(Verify(otp));

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }
}

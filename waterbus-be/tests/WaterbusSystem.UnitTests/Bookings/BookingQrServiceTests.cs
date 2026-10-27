using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Moq;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Bookings.Services;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Repositories;
using WaterbusSystem.Infrastructure.Services.Checkout;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.Bookings;

public sealed class BookingQrServiceTests
{
    private static byte[] Decode(string text) => Convert.FromBase64String(
        text.Replace('-', '+').Replace('_', '/').PadRight((text.Length + 3) / 4 * 4, '='));

    [Fact]
    public async Task PaidBookingProducesCompatibleStableRsaCredential()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);
        var created = await BookingApplicationServiceTests.Service(context).CreateAsync(new(seed.Trip.Id, [seed.Seat1.Id],
            seed.StationA.Id, seed.StationC.Id, "Buyer", "buyer@example.com", null));
        var payment = CommercialHandlerFactory.Payment(context, new CommercialCheckoutTests.Gateway());
        await payment.CreateVnPayUrlAsync(created.OrderId, "127.0.0.1", created.AccessToken);
        await payment.ConfirmIpnAsync(CommercialCheckoutTests.Ipn(context.FinancialTransactions.Single()).Query);
        using var key = RSA.Create(2048);
        var signer = new RsaBookingQrSigner(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Qr:PrivateKeyPem"] = key.ExportPkcs8PrivateKeyPem(), ["Qr:KeyId"] = "service-test-key"
        }).Build());
        var service = new BookingQrService(new BookingRepository(context),
            new CheckoutAccessService(new AccessGrantRepository(context), new CheckoutOptions()), signer);
        var credential = await service.GetAsync(created.OrderId, created.BookingId, created.AccessToken);
        Assert.Equal(credential, await service.GetAsync(created.OrderId, created.BookingId, created.AccessToken));
        var parts = credential.Payload.Split('.');
        Assert.Equal(3, parts.Length);
        using var header = JsonDocument.Parse(Decode(parts[0]));
        using var payload = JsonDocument.Parse(Decode(parts[1]));
        Assert.Equal("RS256", header.RootElement.GetProperty("alg").GetString());
        Assert.Equal("service-test-key", header.RootElement.GetProperty("kid").GetString());
        Assert.Equal(context.Bookings.Single().PublicBookingId, payload.RootElement.GetProperty("pbid").GetString());
        Assert.Equal(seed.Trip.Id, payload.RootElement.GetProperty("tid").GetGuid());
        Assert.Equal(1, payload.RootElement.GetProperty("ver").GetInt32());
        Assert.Equal(context.Bookings.Single().QrIssuedAt!.Value.ToUnixTimeSeconds(), payload.RootElement.GetProperty("iat").GetInt64());
        var publicKey = service.GetPublicKey();
        Assert.DoesNotContain("PRIVATE", publicKey.PublicKeyPem);
        using var verifier = RSA.Create();
        verifier.ImportFromPem(publicKey.PublicKeyPem);
        Assert.True(verifier.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), Decode(parts[2]),
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        Assert.False(verifier.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1] + "modified"), Decode(parts[2]),
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QrRejectsUnpaidOrConfirmedBookingWithoutIssuedTickets(bool confirmed)
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);
        var booking = await BookingApplicationServiceTests.Service(context).CreateAsync(new(seed.Trip.Id, [seed.Seat1.Id],
            seed.StationA.Id, seed.StationC.Id, "Buyer", "buyer@example.com", null));
        if (confirmed) context.Bookings.Single().Status = BookingStatus.Confirmed;
        await context.SaveChangesAsync();
        var signer = new Mock<IBookingQrSigner>(MockBehavior.Strict);
        var service = new BookingQrService(new BookingRepository(context),
            new CheckoutAccessService(new AccessGrantRepository(context), new CheckoutOptions()), signer.Object);
        await Assert.ThrowsAsync<ConcurrencyException>(() => service.GetAsync(booking.OrderId, booking.BookingId, booking.AccessToken));
        signer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task QrRejectsUnauthorizedAccessAndBookingFromAnotherOrder()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);
        var booking = await BookingApplicationServiceTests.Service(context).CreateAsync(new(seed.Trip.Id, [seed.Seat1.Id],
            seed.StationA.Id, seed.StationC.Id, "Buyer", "buyer@example.com", null));
        var service = new BookingQrService(new BookingRepository(context),
            new CheckoutAccessService(new AccessGrantRepository(context), new CheckoutOptions()), Mock.Of<IBookingQrSigner>());
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.GetAsync(booking.OrderId, booking.BookingId, "wrong"));
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetAsync(booking.OrderId, Guid.NewGuid(), booking.AccessToken));
    }
}

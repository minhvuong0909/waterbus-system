using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using WaterbusSystem.Application.Common.Exceptions;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Responses.Payments;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.UnitTests.Bookings;
using WaterbusSystem.UnitTests.TestHelpers;
using WaterbusSystem.WebApi.Controllers;
using Xunit;

namespace WaterbusSystem.UnitTests.Payments;

public sealed class PaymentServiceTests
{
    [Fact]
    public async Task InvalidSignaturePreservesGatewayContractAndDoesNotWrite()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var gateway = new Mock<IVnPayService>();
        gateway.Setup(g => g.ValidateSignature(It.IsAny<IReadOnlyDictionary<string, string>>())).Returns(false);
        var controller = new PaymentsController(CommercialHandlerFactory.Payment(context, gateway.Object))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        var result = Assert.IsType<OkObjectResult>(await controller.VnPayIpn());
        var response = Assert.IsType<VnPayIpnResultDto>(result.Value);
        Assert.Equal("97", response.RspCode);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal("97", json.RootElement.GetProperty("RspCode").GetString());
        Assert.True(json.RootElement.TryGetProperty("Message", out _));
        Assert.Empty(context.FinancialTransactions);
        Assert.Empty(context.Tickets);
    }

    [Fact]
    public async Task ValidBrowserReturnCannotConfirmPayment()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);
        var booking = await BookingApplicationServiceTests.Service(context).CreateAsync(new(seed.Trip.Id, [seed.Seat1.Id],
            seed.StationA.Id, seed.StationC.Id, "Buyer", "buyer@example.com", null));
        var gateway = new CommercialCheckoutTests.Gateway();
        var service = CommercialHandlerFactory.Payment(context, gateway);
        var attempt = await service.CreateVnPayUrlAsync(booking.OrderId, "127.0.0.1", booking.AccessToken);
        var result = await service.GetReturnAsync(new Dictionary<string, string>
        {
            ["vnp_TmnCode"] = "test", ["vnp_TxnRef"] = attempt.MerchantReference,
            ["vnp_ResponseCode"] = "00", ["vnp_TransactionStatus"] = "00"
        });
        Assert.True(result.Success);
        Assert.Equal(result.MerchantReference, result.BookingCode);
        Assert.Equal(OrderStatus.PendingPayment, context.PurchaseOrders.Single().Status);
        Assert.Equal(FinancialTransactionStatus.Requested, context.FinancialTransactions.Single().Status);
        Assert.Empty(context.Tickets);
    }

    [Fact]
    public async Task PaymentInitiationRejectsAnotherOrdersToken()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => CommercialHandlerFactory.Payment(context,
            new CommercialCheckoutTests.Gateway()).CreateVnPayUrlAsync(Guid.NewGuid(), "127.0.0.1", "wrong"));
        Assert.Empty(context.FinancialTransactions);
    }

    [Fact]
    public async Task DuplicateVerifiedIpnIssuesTicketsOnce()
    {
        using var context = TestDbContextFactory.CreateDbContext();
        var seed = TestDbContextFactory.SeedTrip(context);
        var booking = await BookingApplicationServiceTests.Service(context).CreateAsync(new(seed.Trip.Id, [seed.Seat1.Id],
            seed.StationA.Id, seed.StationC.Id, "Buyer", "buyer@example.com", null));
        var service = CommercialHandlerFactory.Payment(context, new CommercialCheckoutTests.Gateway());
        await service.CreateVnPayUrlAsync(booking.OrderId, "127.0.0.1", booking.AccessToken);
        var query = CommercialCheckoutTests.Ipn(context.FinancialTransactions.Single()).Query;
        Assert.Equal("00", (await service.ConfirmIpnAsync(query)).RspCode);
        Assert.Equal("02", (await service.ConfirmIpnAsync(query)).RspCode);
        Assert.Single(context.Tickets);
        Assert.Equal(OrderStatus.Confirmed, context.PurchaseOrders.Single().Status);
    }
}

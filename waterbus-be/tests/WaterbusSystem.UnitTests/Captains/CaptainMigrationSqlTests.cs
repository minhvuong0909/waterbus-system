using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.DTOs.Requests.Captains;
using WaterbusSystem.Application.DTOs.Responses.Bookings;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure;
using WaterbusSystem.Infrastructure.Identity;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Services.Checkout;
using WaterbusSystem.UnitTests.Bookings;
using WaterbusSystem.UnitTests.Domain;
using WaterbusSystem.UnitTests.Payments;
using WaterbusSystem.UnitTests.TestHelpers;
using Xunit;

namespace WaterbusSystem.UnitTests.Captains;

[Collection(SqlCollections.Serial)]
public sealed class CaptainMigrationSqlTests
{
    private static async Task PreflightAsync(string connection)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "docs", "sql", "CaptainSalesCutoverPreflight.sql"))) root = root.Parent;
        Assert.NotNull(root);
        await using var sql = new SqlConnection(connection); await sql.OpenAsync();
        await using var command = sql.CreateCommand();
        command.CommandText = await File.ReadAllTextAsync(Path.Combine(root!.FullName, "docs", "sql", "CaptainSalesCutoverPreflight.sql"), System.Text.Encoding.UTF8);
        await command.ExecuteNonQueryAsync();
    }
    private sealed class LegacyContext(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        { base.OnModelCreating(builder); CaptainLegacyModel.Configure(builder); }
    }

    [SqlServerDomainFact]
    public async Task ExistingCaptainTripGuestHoldAndPaymentSurviveUpgrade_ReapplyAndRealProfileRepair_ThenIpnIssuesOriginalTicket()
    {
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("WATERBUS_TEST_SQLSERVER"))
        { InitialCatalog = "WaterbusDomainTests_CaptainUpgrade_" + Guid.NewGuid().ToString("N") }.ConnectionString;
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection).Options;
        try
        {
            await using (var setup = new ApplicationDbContext(options))
                await setup.GetService<IMigrator>().MigrateAsync("20261115113530_AddPurchaseOrderLinkingIndexes");
            TestDbContextFactory.TripSeedData seed;
            BookingResponse held;
            FinancialTransaction attempt;
            string passwordHash;
            string accessTokenHash;
            DateTimeOffset? originalExpiry;
            using var gateway = new VerifiedVnPayFixture();
            await using (var legacy = new LegacyContext(options))
            {
                seed = TestDbContextFactory.SeedTrip(legacy);
                var captain = await legacy.Users.SingleAsync(u => u.Id == seed.Boat.CaptainUserId);
                captain.PhoneNumber = "0901234567";
                captain.SecurityStamp = Guid.NewGuid().ToString("N");
                captain.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(captain, "LegacyCaptain1234");
                passwordHash = captain.PasswordHash;
                await legacy.SaveChangesAsync();
                // Mô phỏng nghiệp vụ bản cũ: khi đó chưa có điều kiện giấy phép Captain.
                held = await new SqlCommercialTransactionRunner(legacy).RunAsync(() =>
                    CommercialHandlerFactory.Creation(legacy, captains: Mock.Of<ICaptainOperationsService>())
                        .CreateAsync(new WaterbusSystem.Application.Features.Bookings.Services.BookingCreationInput(seed.Trip.Id, [seed.Seat1.Id], seed.StationA.Id, seed.StationC.Id, "Buyer", "buyer@example.com", null), default));
                attempt = new FinancialTransaction { OrderId = held.OrderId, Amount = held.TotalAmount,
                    MerchantReference = Guid.NewGuid().ToString("N"), GatewayName = "VNPay", Status = FinancialTransactionStatus.Requested,
                    IdempotencyKey = "legacy-captain-" + Guid.NewGuid().ToString("N") };
                legacy.FinancialTransactions.Add(attempt); await legacy.SaveChangesAsync();
                originalExpiry = (await legacy.PurchaseOrders.SingleAsync()).ExpiresAt;
                accessTokenHash = (await legacy.AccessGrants.SingleAsync()).TokenHash;
            }
            await using (var upgrade = new ApplicationDbContext(options))
            {
                await upgrade.Database.MigrateAsync(); await upgrade.Database.MigrateAsync();
                var captain = await upgrade.Users.SingleAsync(u => u.Id == seed.Boat.CaptainUserId);
                Assert.Null(captain.CaptainLicenseNumber); Assert.Null(captain.CaptainStatus); Assert.Equal(passwordHash, captain.PasswordHash);
                Assert.Single(await upgrade.Set<CaptainAlert>().Where(a => a.Type == "CaptainProfileRequired").ToListAsync());
                Assert.Equal(FinancialTransactionStatus.Requested, (await upgrade.FinancialTransactions.SingleAsync()).Status);
                Assert.Equal(originalExpiry, (await upgrade.PurchaseOrders.SingleAsync()).ExpiresAt);
                Assert.Equal(accessTokenHash, (await upgrade.AccessGrants.SingleAsync()).TokenHash);
                Assert.Single(await upgrade.AccessGrants.ToListAsync());
                Assert.Equal(51013, (await Assert.ThrowsAsync<SqlException>(() => PreflightAsync(connection))).Number);
                await Assert.ThrowsAsync<ConflictException>(() => CommercialHandlerFactory.Booking(upgrade, transactions: new SqlCommercialTransactionRunner(upgrade))
                    .CreateAsync(CommercialCheckoutTests.Command(seed) with { SeatIds = [seed.Seat2.Id] }));
            }
            var services = new ServiceCollection(); services.AddLogging();
            services.AddSingleton(Mock.Of<Microsoft.Extensions.Hosting.IHostEnvironment>(e => e.EnvironmentName == "Development"));
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
                ["ConnectionStrings:DefaultConnection"] = connection,
                ["JwtSettings:Secret"] = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)) }).Build();
            services.AddInfrastructureServices(config);
            services.AddScoped<ICurrentUserService>(_ => Mock.Of<ICurrentUserService>(u => u.UserRole == "Admin" && u.UserId == "00000000-0000-0000-0000-000000000001"));
            await using (var provider = services.BuildServiceProvider())
            {
                using var scope = provider.CreateScope(); var captains = scope.ServiceProvider.GetRequiredService<ICaptainService>();
                var captain = await captains.GetAsync(seed.Boat.CaptainUserId!.Value);
                await captains.UpdateAsync(captain.Id, new UpdateCaptainRequest(captain.FullName, captain.Email, "0901234567", "REAL-ADMIN-VERIFIED-TEST-LICENSE",
                    DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)), "Admin reviewed actual license documents"));
                await captains.ChangeStatusAsync(captain.Id, new ChangeCaptainStatusRequest(CaptainStatus.Active, "Profile review completed"));
            }
            await PreflightAsync(connection);
            await using (var verify = new ApplicationDbContext(options))
            {
                Assert.Equal(originalExpiry, (await verify.PurchaseOrders.SingleAsync()).ExpiresAt);
                var response = await CommercialHandlerFactory.Confirmation(verify, gateway.Gateway, new SqlCommercialTransactionRunner(verify))
                    .ConfirmIpnAsync(gateway.Callback(attempt.MerchantReference!, attempt.Amount), default);
                Assert.Equal("00", response.RspCode);
                Assert.Null((await verify.PurchaseOrders.SingleAsync()).ExpiresAt); // Payment confirmation clears the hold normally.
                Assert.Equal(OrderStatus.Confirmed, (await verify.PurchaseOrders.SingleAsync()).Status);
                Assert.Equal(held.BookingId, (await verify.Tickets.SingleAsync()).BookingId);
                Assert.Single(await verify.AccessGrants.ToListAsync());
                Assert.Equal(accessTokenHash, (await verify.AccessGrants.SingleAsync()).TokenHash);
                Assert.Equal(attempt.Id, (await verify.FinancialTransactions.SingleAsync()).Id);
                Assert.Equal(passwordHash, (await verify.Users.SingleAsync(u => u.Id == seed.Boat.CaptainUserId)).PasswordHash);
                Assert.Equal(TripStatus.Scheduled, (await verify.Trips.SingleAsync()).Status);
                var next = await CommercialHandlerFactory.Booking(verify, transactions: new SqlCommercialTransactionRunner(verify))
                    .CreateAsync(CommercialCheckoutTests.Command(seed) with { SeatIds = [seed.Seat2.Id] });
                Assert.NotEqual(held.OrderId, next.OrderId);
                Assert.Equal(2, await verify.AccessGrants.CountAsync());
            }
        }
        finally
        {
            Assert.StartsWith("WaterbusDomainTests_CaptainUpgrade_", new SqlConnectionStringBuilder(connection).InitialCatalog);
            await using var cleanup = new ApplicationDbContext(options); await cleanup.Database.EnsureDeletedAsync();
        }
    }
}

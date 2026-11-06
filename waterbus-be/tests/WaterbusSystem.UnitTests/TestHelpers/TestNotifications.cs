using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Notifications;
using WaterbusSystem.Application.Interfaces.Services;
using WaterbusSystem.Infrastructure.Persistence;
using WaterbusSystem.Infrastructure.Repositories;

namespace WaterbusSystem.UnitTests.TestHelpers;

public sealed class SystemTestClock : IDateTimeService { public DateTimeOffset Now => DateTimeOffset.UtcNow; }

/// <summary>Outbox thật (EF) trên cùng DbContext của test: service nghiệp vụ ghi outbox y như production.</summary>
public static class TestNotifications
{
    public static INotificationOutbox Outbox(ApplicationDbContext context) =>
        new NotificationOutbox(new EmailOutboxRepository(context), new NotificationDataRepository(context), new SystemTestClock());
}

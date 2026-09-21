using WaterbusSystem.Application.Common.Interfaces;

namespace WaterbusSystem.Infrastructure.Services;

/// <summary>
/// Dịch vụ lấy thời gian hệ thống chuẩn UTC
/// </summary>
public class DateTimeService : IDateTimeService
{
    public DateTimeOffset Now => DateTimeOffset.UtcNow;
}

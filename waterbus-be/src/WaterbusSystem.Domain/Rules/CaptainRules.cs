using WaterbusSystem.Domain.Enums;

namespace WaterbusSystem.Domain.Rules;

/// <summary>Quy tắc Captain thuần nghiệp vụ; ngày hết hạn có hiệu lực hết ngày tại Việt Nam.</summary>
public static class CaptainRules
{
    public static DateOnly VietnamDate(DateTimeOffset at) => DateOnly.FromDateTime(at.ToOffset(TimeSpan.FromHours(7)).DateTime);
    public static bool IsEligible(bool accountActive, bool isCaptain, CaptainStatus? status, string? license, DateOnly? expiresOn, DateTimeOffset at) =>
        accountActive && isCaptain && status == CaptainStatus.Active && !string.IsNullOrWhiteSpace(license)
        && expiresOn.HasValue && expiresOn.Value >= VietnamDate(at);
    public static DateTimeOffset RequiredAt(DateTimeOffset staffCheckInOpensAt, int preparationMinutes) =>
        preparationMinutes > 0 ? staffCheckInOpensAt.AddMinutes(-preparationMinutes)
        : throw new ArgumentOutOfRangeException(nameof(preparationMinutes));
    public static bool IsLate(DateTimeOffset recordedAt, DateTimeOffset requiredAt) => recordedAt > requiredAt;
    public static bool BlocksAssignmentChange(TripStatus status) => status is TripStatus.Boarding or TripStatus.EnRoute or TripStatus.Suspended;
}

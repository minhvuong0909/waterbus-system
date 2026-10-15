namespace WaterbusSystem.Application.Features.Bookings.Services;

/// <summary>Tham số checkout dùng chung cho DB, Redis và payment.</summary>
public sealed class CheckoutOptions
{
    public int SessionTtlMinutes { get; set; } = 10;
    public int PaymentPendingTtlMinutes { get; set; } = 15;
    public int AccessTokenTtlDays { get; set; } = 30;
    public TimeSpan HoldDuration => TimeSpan.FromMinutes(Math.Clamp(SessionTtlMinutes, 1, 120));
    public TimeSpan PaymentDuration => TimeSpan.FromMinutes(Math.Clamp(PaymentPendingTtlMinutes, 1, 120));
}

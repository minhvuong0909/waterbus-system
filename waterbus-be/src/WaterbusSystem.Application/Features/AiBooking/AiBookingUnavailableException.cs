namespace WaterbusSystem.Application.Features.AiBooking;

/// <summary>AI Assistant chưa sẵn sàng phục vụ vì thiếu cấu hình hoặc contract tích hợp.</summary>
public sealed class AiBookingUnavailableException : InvalidOperationException
{
    public AiBookingUnavailableException() : base("AI Booking Assistant chưa được kích hoạt hoặc thiếu OpenAI API key.") { }
}

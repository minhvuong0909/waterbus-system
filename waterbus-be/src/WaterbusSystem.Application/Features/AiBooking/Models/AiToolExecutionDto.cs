namespace WaterbusSystem.Application.Features.AiBooking.Models;

/// <summary>
/// Ghi nhận công cụ (tool) đã được gọi trong lượt hội thoại nhằm phục vụ audit/debug
/// và thể hiện tính minh bạch giữa AI và dữ liệu API.
/// </summary>
public record AiToolExecutionDto(
    string ToolName,
    string ArgumentsSummary,
    bool Success,
    string? ExecutionNote = null);

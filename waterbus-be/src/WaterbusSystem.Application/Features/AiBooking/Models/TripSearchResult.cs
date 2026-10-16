namespace WaterbusSystem.Application.Features.AiBooking.Models;

/// <summary>
/// Kết quả tìm kiếm chuyến tàu trả về từ ITripSearchTool.
/// </summary>
/// <param name="Trips">Danh sách các chuyến tìm thấy phù hợp tiêu chí.</param>
/// <param name="IsSuccess">Trạng thái thực thi của tool tìm kiếm.</param>
/// <param name="Message">Thông điệp giải thích từ tool (khi không tìm thấy hoặc có lỗi).</param>
/// <param name="RequiresMoreClarification">
/// Đánh dấu tiêu chí còn thiếu thông tin quan trọng (ví dụ chưa rõ bến hoặc ngày),
/// yêu cầu AI hỏi thêm người dùng thay vì tự đoán.
/// </param>
public record TripSearchResult(
    IReadOnlyList<AiTripCardDto> Trips,
    bool IsSuccess,
    string? Message = null,
    bool RequiresMoreClarification = false);

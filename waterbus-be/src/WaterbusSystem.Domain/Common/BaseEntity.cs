using System.ComponentModel.DataAnnotations;

namespace WaterbusSystem.Domain.Common;

/// <summary>
/// Lớp cơ sở (Base Entity) cho toàn bộ thực thể trong hệ thống.
/// Chứa các thuộc tính định danh, thời gian tạo, cập nhật, xóa mềm và token chống xung đột.
/// </summary>
public abstract class BaseEntity
{
    /// <summary>
    /// Khóa chính định danh duy nhất (Guid v4)
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Thời điểm bản ghi được tạo (UTC)
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Thời điểm bản ghi được cập nhật lần cuối (UTC)
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>
    /// Cờ đánh dấu xóa mềm (Soft Delete) để bảo toàn dữ liệu lịch sử đối soát
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// Định danh người tạo bản ghi (UserId dạng chuỗi lấy từ ICurrentUserService), tự động gán tại
    /// ApplicationDbContext.SaveChangesAsync khi entity ở trạng thái Added. Null nếu được tạo bởi hệ thống/seed.
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Định danh người chỉnh sửa gần nhất, tự động gán tại ApplicationDbContext.SaveChangesAsync
    /// khi entity ở trạng thái Modified.
    /// </summary>
    public string? LastModifiedBy { get; set; }

    /// <summary>
    /// Optimistic Concurrency Token (Lớp phòng thủ 2 chống Double-booking tại CSDL).
    /// Thuộc tính [Timestamp] báo cho EF Core tự động so sánh RowVersion trong mệnh đề WHERE khi UPDATE.
    /// Nếu có 2 transaction cùng cố gắng cập nhật 1 ghế, transaction thứ 2 sẽ bị ném DbUpdateConcurrencyException.
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = [];
}

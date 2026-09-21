namespace WaterbusSystem.Domain.Common;

/// <summary>
/// Interface đánh dấu thực thể cần lưu vết người tạo và người chỉnh sửa
/// </summary>
public interface IAuditableEntity
{
    string? CreatedBy { get; set; }
    string? LastModifiedBy { get; set; }
}

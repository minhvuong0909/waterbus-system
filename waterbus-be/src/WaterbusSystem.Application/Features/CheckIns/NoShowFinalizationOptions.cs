namespace WaterbusSystem.Application.Features.CheckIns;

/// <summary>Cấu hình job định kỳ kết luận NoShow (BR-BOARD-05, BR-OFF-04).</summary>
public class NoShowFinalizationOptions
{
    public const string SectionName = "CheckIn:NoShowFinalization";

    public bool Enabled { get; set; } = true;

    /// <summary>Lịch quét dạng cron (mặc định mỗi 5 phút).</summary>
    public string CronExpression { get; set; } = "*/5 * * * *";

    /// <summary>
    /// Thời gian chờ thêm sau CheckInCloseAt trước khi coi là đủ điều kiện kết luận NoShow, để các
    /// sự kiện check-in offline còn kịp đồng bộ về (BR-OFF-04: không coi offline là tuyệt đối trước
    /// khi đối soát).
    /// </summary>
    public int GraceMinutes { get; set; } = 15;
}

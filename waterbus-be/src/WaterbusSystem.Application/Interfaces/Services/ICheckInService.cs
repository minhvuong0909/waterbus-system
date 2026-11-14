using WaterbusSystem.Application.Common.Models;
using WaterbusSystem.Application.DTOs.Requests.CheckIns;
using WaterbusSystem.Application.DTOs.Responses.CheckIns;

namespace WaterbusSystem.Application.Interfaces.Services;

public interface ICheckInService
{
    Task<PaginatedList<ScannerStaffResponse>> ListScannerStaffAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    Task<PaginatedList<ScannerAssignmentAdminResponse>> ListScannerAssignmentsAsync(
        Guid tripStopCallId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Admin phân công thiết bị quét + Staff cho một điểm dừng cụ thể (BR-CFG-01, BR-OFF-02).</summary>
    Task<AssignScannerResponse> AssignScannerAsync(AssignScannerRequest request, CancellationToken cancellationToken = default);

    /// <summary>Admin thu hồi (kết thúc ngay) một phân công scanner (BR-OFF-02).</summary>
    Task<RevokeScannerAssignmentResponse> RevokeScannerAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken = default);

    /// <summary>Admin cấu hình cửa sổ check-in cho một điểm dừng cụ thể (BR-BOARD-05, BR-CFG-01).</summary>
    Task<SetCheckInWindowResponse> SetCheckInWindowAsync(Guid tripStopCallId, SetCheckInWindowRequest request, CancellationToken cancellationToken = default);

    /// <summary>Staff quét QR và check-in 1 Ticket tại đúng điểm dừng khi có mạng (BR-QR-03, BR-BOARD-01..05).</summary>
    Task<CheckInResultResponse> SubmitCheckInAsync(SubmitCheckInRequest request, CancellationToken cancellationToken = default);

    /// <summary>Đồng bộ theo lô các sự kiện check-in đã ghi offline (BR-OFF-01..05).</summary>
    Task<SyncCheckInEventsResponse> SyncCheckInEventsAsync(SyncCheckInEventsRequest request, CancellationToken cancellationToken = default);

    /// <summary>Staff Scanner hỏi "tôi được phân công soát vé ở điểm dừng nào" (BR-OFF-01, BR-BOARD-03).</summary>
    Task<IReadOnlyList<MyScannerAssignmentResponse>> GetMyScannerAssignmentsAsync(CancellationToken cancellationToken = default);

    /// <summary>Staff Scanner preload manifest trước giờ mở cổng (BR-OFF-01).</summary>
    Task<ScannerManifestResponse> GetScannerManifestAsync(Guid tripStopCallId, CancellationToken cancellationToken = default);

    /// <summary>Job định kỳ kết luận NoShow cho Ticket chưa check-in sau khi cửa sổ check-in đóng (BR-BOARD-05, BR-OFF-04).</summary>
    Task<NoShowFinalizationResultResponse> RunNoShowFinalizationAsync(CancellationToken cancellationToken = default);
}

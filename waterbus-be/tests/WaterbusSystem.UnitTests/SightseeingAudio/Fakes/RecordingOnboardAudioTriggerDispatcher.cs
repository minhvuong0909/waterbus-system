using WaterbusSystem.Application.Features.SightseeingAudio.Interfaces;
using WaterbusSystem.Application.Features.SightseeingAudio.Models;

namespace WaterbusSystem.UnitTests.SightseeingAudio.Fakes;

/// <summary>
/// Fake implementation của <see cref="IOnboardAudioTriggerDispatcher"/> ghi lại các yêu cầu trigger phục vụ unit tests.
/// LƯU Ý: Bộ dispatcher này chỉ ghi lại việc dispatch yêu cầu tới Onboard Player (BR-EXP-03).
/// Nó KHÔNG giả lập hoặc xác nhận âm thanh "đã phát ra loa" (has played out loud) vì việc đó cần thiết bị trên tàu phản hồi.
/// </summary>
public sealed class RecordingOnboardAudioTriggerDispatcher : IOnboardAudioTriggerDispatcher
{
    private readonly List<AudioTriggerRequest> _dispatchedRequests = new();
    private readonly object _lock = new();

    /// <summary>
    /// Cho phép cấu hình giả lập lỗi dispatch (ví dụ mất kết nối mạng với tàu).
    /// </summary>
    public bool ShouldFailDispatch { get; set; }

    /// <summary>
    /// Thông điệp lỗi khi giả lập dispatch thất bại.
    /// </summary>
    public string FailureMessage { get; set; } = "Mất kết nối với Onboard Player trên tàu.";

    public TaskCompletionSource<bool>? DispatchStarted { get; set; }
    public Task? DispatchGate { get; set; }
    public Exception? DispatchException { get; set; }

    /// <summary>
    /// Danh sách các yêu cầu trigger đã được dispatch thành công.
    /// </summary>
    public IReadOnlyList<AudioTriggerRequest> DispatchedRequests
    {
        get
        {
            lock (_lock)
            {
                return _dispatchedRequests.ToList();
            }
        }
    }

    public int DispatchedCount
    {
        get
        {
            lock (_lock)
            {
                return _dispatchedRequests.Count;
            }
        }
    }

    public async Task<AudioTriggerDispatchResult> DispatchTriggerRequestAsync(
        AudioTriggerRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        DispatchStarted?.TrySetResult(true);
        if (DispatchGate != null)
            await DispatchGate.WaitAsync(cancellationToken);
        if (DispatchException != null)
            throw DispatchException;

        if (ShouldFailDispatch)
        {
            return AudioTriggerDispatchResult.Failed(request.RequestId, FailureMessage);
        }

        lock (_lock)
        {
            _dispatchedRequests.Add(request);
        }

        return AudioTriggerDispatchResult.Success(
            request.RequestId,
            $"Đã gửi lệnh phát audio cho POI '{request.PoiName}' tới Onboard Player.");
    }

    public void Clear()
    {
        lock (_lock)
        {
            _dispatchedRequests.Clear();
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.Features.Scanner.Commands.SyncOfflineCheckIns;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>Đồng bộ lượt check-in đã ghi nhận offline bởi Staff Scanner.</summary>
[Authorize(Roles = "Staff")]
public sealed class TicketsController : BaseApiController
{
    /// <summary>Đối soát và đồng bộ tối đa 50 sự kiện check-in, khử trùng theo ClientEventId.</summary>
    [HttpPost("sync-offline")]
    [ProducesResponseType(typeof(SyncOfflineCheckInsResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SyncOfflineCheckInsResult>> SyncOffline(
        [FromBody] SyncOfflineCheckInsRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var command = new SyncOfflineCheckInsCommand(
            request.ScannerDeviceId,
            request.BatchTimestamp,
            request.Events,
            idempotencyKey ?? string.Empty);
        var result = await Mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}

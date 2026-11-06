using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaterbusSystem.Application.Features.Scanner.Commands.ClaimPrimaryScanner;
using WaterbusSystem.Application.Features.Scanner.Queries.GetScannerManifest;
using WaterbusSystem.Application.Features.Scanner.Queries.GetScannerPublicKey;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>Các API phục vụ ứng dụng Staff Scanner và chế độ tác nghiệp offline.</summary>
public sealed class ScannerController : BaseApiController
{
    /// <summary>Lấy khóa công khai hiện hành để xác minh chữ ký QR offline.</summary>
    [HttpGet("public-key")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ScannerPublicKeyDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ScannerPublicKeyDto>> GetPublicKey(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetScannerPublicKeyQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Tải manifest cho bến mà nhân viên hiện tại được phân công.</summary>
    [HttpGet("manifest")]
    [Authorize(Roles = "Staff")]
    [ProducesResponseType(typeof(ScannerManifestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ScannerManifestDto>> GetManifest(
        [FromQuery] string stationId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetScannerManifestQuery(stationId), cancellationToken);
        return Ok(result);
    }

    /// <summary>Claim quyền ghi check-in offline chính cho phân công đã được thiết lập.</summary>
    [HttpPost("claim-primary")]
    [Authorize(Roles = "Staff")]
    [ProducesResponseType(typeof(ClaimPrimaryScannerResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClaimPrimaryScannerResult>> ClaimPrimary(
        [FromBody] ClaimPrimaryScannerCommand command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}

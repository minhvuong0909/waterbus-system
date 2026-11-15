using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WaterbusSystem.Application.DTOs.Requests.ScannerDevices;
using WaterbusSystem.Application.DTOs.Responses.ScannerDevices;
using WaterbusSystem.Application.Features.ScannerDevices.Services;
using WaterbusSystem.Application.Interfaces.Services;

namespace WaterbusSystem.WebApi.Controllers;

[Authorize(Policy = "StaffOnly")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ScannerPairingController(IStationScannerService pairing, StationScannerAccessService access,
    StationScannerPolicy policy) : BaseApiController
{
    [HttpPost("~/api/v1/scanner/pair")]
    [EnableRateLimiting("ScannerPairingPolicy")]
    public async Task<ActionResult<ScannerPairingResponse>> Pair([FromBody] RedeemScannerInvitationRequest request) =>
        Ok(await pairing.PairAsync(request, HttpContext.RequestAborted));

    [HttpGet("~/api/v1/scanner/status")]
    public async Task<IActionResult> Status([FromHeader(Name = "X-Scanner-Credential")] string? credential)
    {
        await using var scanner = await access.OpenAsync(credential, false, HttpContext.RequestAborted);
        var shift = await policy.GetShiftAsync(scanner, HttpContext.RequestAborted);
        return Ok(new { scanner.Scanner, Shift = shift });
    }
}

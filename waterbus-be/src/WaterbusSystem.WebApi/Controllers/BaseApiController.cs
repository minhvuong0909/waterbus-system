using Microsoft.AspNetCore.Mvc;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>
/// Controller cơ sở chuẩn hóa tiền tố định tuyến /api/v1/[controller]
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    /// <summary>
    /// IP client dạng IPv4 khi có thể (vnp_IpAddr của VNPay không nhận dạng IPv6-mapped như ::ffff:1.2.3.4).
    /// </summary>
    protected string ClientIp() => ToIpv4String(HttpContext.Connection.RemoteIpAddress);

    public static string ToIpv4String(System.Net.IPAddress? address) =>
        address is null ? "127.0.0.1" : (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();

}

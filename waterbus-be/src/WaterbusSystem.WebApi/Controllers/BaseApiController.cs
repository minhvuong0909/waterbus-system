using Microsoft.AspNetCore.Mvc;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>
/// Controller cơ sở chuẩn hóa tiền tố định tuyến /api/v1/[controller]
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public abstract class BaseApiController : ControllerBase
{
}

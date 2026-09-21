using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace WaterbusSystem.WebApi.Controllers;

/// <summary>
/// Controller cơ sở chuẩn hóa tiền tố định tuyến /api/v1/[controller] và inject ISender (MediatR)
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    private ISender? _mediator;

    protected ISender Mediator => _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();
}

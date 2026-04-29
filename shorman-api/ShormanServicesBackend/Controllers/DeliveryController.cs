using MediatR;
using Microsoft.AspNetCore.Mvc;
using ShormanServicesBackend.Api.Contracts;
using ShormanServicesBackend.Api.Features;

namespace ShormanServicesBackend.Controllers;

[ApiController]
[Route("api/delivery")]
public class DeliveryController(IMediator mediator) : ControllerBase
{
    [HttpGet("check")]
    public async Task<ActionResult<DeliveryCheckResult>> Check([FromQuery] string postalCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(postalCode))
        {
            return BadRequest(new { message = "postalCode is required." });
        }

        return Ok(await mediator.Send(new CheckDeliveryQuery(postalCode), cancellationToken));
    }
}

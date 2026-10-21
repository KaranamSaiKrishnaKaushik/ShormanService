using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShormanServicesBackend.Api.Contracts;
using ShormanServicesBackend.Api.Features;
using ShormanServicesBackend.Api.Security;

namespace ShormanServicesBackend.Controllers;

[ApiController]
[Route("api/product-management/pricing")]
[Authorize(Roles = RoleNames.SuperAdmin)]
public class ProductPricingController(IMediator mediator) : ControllerBase
{
    [HttpGet("current")]
    public Task<PricingPolicyVersionDto> GetCurrent(CancellationToken cancellationToken) =>
        mediator.Send(new GetCurrentPricingPolicyQuery(), cancellationToken);

    [HttpGet("history")]
    public Task<PagedResultDto<PricingPolicyVersionDto>> GetHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default) =>
        mediator.Send(new GetPricingPolicyHistoryQuery(page, pageSize), cancellationToken);

    [HttpGet("audit")]
    public Task<PagedResultDto<PricingPolicyAuditEventDto>> GetAudit(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default) =>
        mediator.Send(new GetPricingPolicyAuditQuery(page, pageSize), cancellationToken);

    [HttpPost("apply")]
    public async Task<ActionResult<PricingPolicyVersionDto>> Apply([FromBody] ApplyPricingPolicyRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await mediator.Send(new ApplyPricingPolicyCommand(GetUserId(), request), cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShormanServicesBackend.Api.Contracts;
using ShormanServicesBackend.Api.Features;

namespace ShormanServicesBackend.Controllers;

[ApiController]
[Route("api/order-insights")]
[Authorize(Roles = "SuperAdmin,Admin,Customer")]
public class OrderInsightsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public Task<OrderInsightsDto> Get([FromQuery] string? range, CancellationToken cancellationToken) =>
        mediator.Send(new GetOrderInsightsQuery(GetUserId(), range ?? "1y"), cancellationToken);

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

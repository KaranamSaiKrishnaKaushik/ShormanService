using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShormanServicesBackend.Api.Contracts;
using ShormanServicesBackend.Api.Features;
using ShormanServicesBackend.Api.Security;

namespace ShormanServicesBackend.Controllers;

[ApiController]
[Route("api/rider/orders")]
[Authorize(Roles = $"{RoleNames.SuperAdmin},{RoleNames.Rider}")]
public class RiderOrdersController(IMediator mediator) : ControllerBase
{
    [HttpGet("available")]
    public Task<IReadOnlyCollection<OrderDto>> GetAvailableOrders(CancellationToken cancellationToken) =>
        mediator.Send(new GetAvailableRiderOrdersQuery(), cancellationToken);

    [HttpGet("mine")]
    public Task<IReadOnlyCollection<OrderDto>> GetMyOrders(CancellationToken cancellationToken) =>
        mediator.Send(new GetAssignedRiderOrdersQuery(GetUserId()), cancellationToken);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderDto>> GetOrder(int id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetRiderOrderByIdQuery(GetUserId(), id), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:int}/accept")]
    public Task<ActionResult<OrderDto>> Accept(int id, CancellationToken cancellationToken) =>
        ExecuteRiderTransitionAsync(new AcceptRiderOrderCommand(GetUserId(), id), cancellationToken);

    [HttpPost("{id:int}/picked-up")]
    public Task<ActionResult<OrderDto>> MarkPickedUp(int id, CancellationToken cancellationToken) =>
        ExecuteRiderTransitionAsync(new MarkRiderOrderPickedUpCommand(GetUserId(), id), cancellationToken);

    [HttpPost("{id:int}/out-for-delivery")]
    public Task<ActionResult<OrderDto>> MarkOutForDelivery(int id, CancellationToken cancellationToken) =>
        ExecuteRiderTransitionAsync(new MarkRiderOrderOutForDeliveryCommand(GetUserId(), id), cancellationToken);

    [HttpPost("{id:int}/delivered")]
    public Task<ActionResult<OrderDto>> MarkDelivered(int id, CancellationToken cancellationToken) =>
        ExecuteRiderTransitionAsync(new MarkRiderOrderDeliveredCommand(GetUserId(), id), cancellationToken);

    [HttpPost("{id:int}/cash-collected")]
    public Task<ActionResult<OrderDto>> MarkCashCollected(int id, CancellationToken cancellationToken) =>
        ExecuteRiderTransitionAsync(new MarkRiderOrderCashCollectedCommand(GetUserId(), id), cancellationToken);

    [HttpPost("{id:int}/complete")]
    public Task<ActionResult<OrderDto>> Complete(int id, CancellationToken cancellationToken) =>
        ExecuteRiderTransitionAsync(new CompleteRiderOrderCommand(GetUserId(), id), cancellationToken);

    private async Task<ActionResult<OrderDto>> ExecuteRiderTransitionAsync<TCommand>(TCommand command, CancellationToken cancellationToken)
        where TCommand : IRequest<OrderDto>
    {
        try
        {
            return Ok(await mediator.Send(command, cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
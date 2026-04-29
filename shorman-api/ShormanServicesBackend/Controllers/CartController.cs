using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShormanServicesBackend.Api.Contracts;
using ShormanServicesBackend.Api.Features;

namespace ShormanServicesBackend.Controllers;

[ApiController]
[Route("api/cart")]
[Authorize]
public class CartController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public Task<CartDto> GetCart(CancellationToken cancellationToken) =>
        mediator.Send(new GetCartQuery(GetUserId()), cancellationToken);

    [HttpPost("items")]
    public async Task<ActionResult<CartDto>> AddItem([FromBody] AddCartItemRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await mediator.Send(new AddCartItemCommand(GetUserId(), request), cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPut("items/{itemId:int}")]
    public async Task<ActionResult<CartDto>> UpdateItem(int itemId, [FromBody] UpdateCartItemRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await mediator.Send(new UpdateCartItemCommand(GetUserId(), itemId, request), cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpDelete("items/{itemId:int}")]
    public Task<CartDto> RemoveItem(int itemId, CancellationToken cancellationToken) =>
        mediator.Send(new RemoveCartItemCommand(GetUserId(), itemId), cancellationToken);

    [HttpDelete]
    public Task<CartDto> ClearCart(CancellationToken cancellationToken) =>
        mediator.Send(new ClearCartCommand(GetUserId()), cancellationToken);

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
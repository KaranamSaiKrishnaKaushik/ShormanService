using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShormanServicesBackend.Api.Contracts;
using ShormanServicesBackend.Api.Features;
using ShormanServicesBackend.Api.Security;

namespace ShormanServicesBackend.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize(Roles = $"{RoleNames.SuperAdmin},{RoleNames.Admin},{RoleNames.Customer}")]
public class OrdersController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public Task<PagedResultDto<OrderDto>> GetOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery(Name = "sort")] string sortDirection = "desc",
        CancellationToken cancellationToken = default) =>
        mediator.Send(new GetOrdersQuery(GetUserId(), page, pageSize, search, sortDirection), cancellationToken);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderDto>> GetOrder(int id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetOrderByIdQuery(GetUserId(), id), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<OrderDto>> CreateOrder([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await mediator.Send(new CreateOrderCommand(GetUserId(), request), cancellationToken);
            return CreatedAtAction(nameof(GetOrder), new { id = result.Id }, result);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("checkout-session")]
    public async Task<ActionResult<CheckoutSessionResponse>> CreateCheckoutSession([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await mediator.Send(new CreateCheckoutSessionCommand(GetUserId(), request), cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (Stripe.StripeException exception)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = $"Stripe error: {exception.StripeError?.Message ?? exception.Message}" });
        }
    }

    [HttpPost("{id:int}/payment-cancelled")]
    public async Task<ActionResult<OrderDto>> CancelPendingPayment(int id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await mediator.Send(new CancelPendingOrderPaymentCommand(GetUserId(), id), cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpGet("{id:int}/invoice")]
    public async Task<IActionResult> DownloadInvoice(int id, [FromServices] ShormanServicesBackend.Api.Invoicing.IInvoiceService invoiceService, CancellationToken cancellationToken)
    {
        var isAdmin = User.IsInRole(RoleNames.SuperAdmin) || User.IsInRole(RoleNames.Admin);
        var file = await invoiceService.GetInvoiceFileAsync(id, GetUserId(), isAdmin, cancellationToken);
        return file is null ? NotFound(new { message = "Invoice not available." }) : File(file.Content, "application/pdf", file.FileName);
    }

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

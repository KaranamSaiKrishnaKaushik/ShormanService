using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShormanServicesBackend.Api.Contracts;
using ShormanServicesBackend.Api.Features;
using ShormanServicesBackend.Api.Security;

namespace ShormanServicesBackend.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public Task<PagedResultDto<ProductDto>> GetProducts(
        [FromQuery] string? search,
        [FromQuery] int? categoryId,
        [FromQuery] int? supermarketId,
        [FromQuery] int[]? supermarketIds,
        [FromQuery] ProductSortOption sort = ProductSortOption.Default,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        CancellationToken cancellationToken = default) =>
        mediator.Send(new GetProductsQuery(search, categoryId, supermarketId, supermarketIds, sort, page, pageSize), cancellationToken);

    [HttpGet("admin")]
    [Authorize(Roles = $"{RoleNames.SuperAdmin},{RoleNames.Admin}")]
    public Task<PagedResultDto<ProductDto>> GetAdminProducts(
        [FromQuery] string? search,
        [FromQuery] int? categoryId,
        [FromQuery] int? supermarketId,
        [FromQuery] int[]? supermarketIds,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        mediator.Send(new GetAdminProductsQuery(search, categoryId, supermarketId, supermarketIds, page, pageSize), cancellationToken);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductDto>> GetProduct(int id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetProductByIdQuery(id), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = $"{RoleNames.SuperAdmin},{RoleNames.Admin}")]
    public async Task<ActionResult<ProductDto>> UpdateProduct(int id, [FromBody] UpdateProductRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await mediator.Send(new UpdateProductCommand(id, request), cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = $"{RoleNames.SuperAdmin},{RoleNames.Admin}")]
    public async Task<IActionResult> DeleteProduct(int id, CancellationToken cancellationToken)
    {
        var deleted = await mediator.Send(new DeleteProductCommand(id), cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}

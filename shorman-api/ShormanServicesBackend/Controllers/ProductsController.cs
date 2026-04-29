using MediatR;
using Microsoft.AspNetCore.Mvc;
using ShormanServicesBackend.Api.Contracts;
using ShormanServicesBackend.Api.Features;

namespace ShormanServicesBackend.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyCollection<ProductDto>> GetProducts([FromQuery] string? search, [FromQuery] int? categoryId, [FromQuery] int? supermarketId, CancellationToken cancellationToken) =>
        mediator.Send(new GetProductsQuery(search, categoryId, supermarketId), cancellationToken);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductDto>> GetProduct(int id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetProductByIdQuery(id), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}

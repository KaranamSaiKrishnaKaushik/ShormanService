using MediatR;
using Microsoft.AspNetCore.Mvc;
using ShormanServicesBackend.Api.Contracts;
using ShormanServicesBackend.Api.Features;

namespace ShormanServicesBackend.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoriesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyCollection<CategoryDto>> GetCategories(CancellationToken cancellationToken) =>
        mediator.Send(new GetCategoriesQuery(), cancellationToken);
}

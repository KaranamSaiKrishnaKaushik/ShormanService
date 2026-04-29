using MediatR;
using Microsoft.AspNetCore.Mvc;
using ShormanServicesBackend.Api.Contracts;
using ShormanServicesBackend.Api.Features;

namespace ShormanServicesBackend.Controllers;

[ApiController]
[Route("api/supermarkets")]
public class SupermarketsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyCollection<SupermarketDto>> GetSupermarkets(CancellationToken cancellationToken) =>
        mediator.Send(new GetSupermarketsQuery(), cancellationToken);
}

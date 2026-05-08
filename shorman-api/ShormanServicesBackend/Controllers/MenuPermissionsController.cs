using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShormanServicesBackend.Api.Contracts;
using ShormanServicesBackend.Api.Features;

namespace ShormanServicesBackend.Controllers;

[ApiController]
[Route("api/menu-permissions")]
[Authorize]
public class MenuPermissionsController(IMediator mediator) : ControllerBase
{
    [HttpGet("current")]
    public Task<CurrentMenuPermissionsDto> GetCurrent(CancellationToken cancellationToken)
    {
        var roles = User.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return mediator.Send(new GetCurrentMenuPermissionsQuery(roles), cancellationToken);
    }
}
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShormanServicesBackend.Api.Contracts;
using ShormanServicesBackend.Api.Features;
using ShormanServicesBackend.Api.Security;

namespace ShormanServicesBackend.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = RoleNames.SuperAdmin)]
public class AdminUsersController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyCollection<AdminUserListItemDto>> GetUsers(CancellationToken cancellationToken) =>
        mediator.Send(new GetAdminUsersQuery(), cancellationToken);

    [HttpPut("{id:int}/role")]
    public async Task<ActionResult<AdminUserListItemDto>> UpdateRole(int id, [FromBody] UpdateUserRoleRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await mediator.Send(new UpdateUserRoleCommand(id, request.Role), cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }
}
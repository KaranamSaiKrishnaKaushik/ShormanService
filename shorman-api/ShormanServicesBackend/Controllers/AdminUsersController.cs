using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ShormanServicesBackend.Api.Contracts;
using ShormanServicesBackend.Api.Features;
using ShormanServicesBackend.Api.Security;

namespace ShormanServicesBackend.Controllers;

[ApiController]
[Route("api/admin/users")]
public class AdminUsersController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = RoleNames.SuperAdmin)]
    public Task<IReadOnlyCollection<AdminUserListItemDto>> GetUsers(CancellationToken cancellationToken) =>
        mediator.Send(new GetAdminUsersQuery(), cancellationToken);

    [HttpGet("order-summary")]
    [Authorize(Roles = $"{RoleNames.SuperAdmin},{RoleNames.Admin},{RoleNames.Rider}")]
    public Task<IReadOnlyCollection<AdminOrderSummaryDto>> GetOrderSummary(CancellationToken cancellationToken) =>
        mediator.Send(new GetAdminOrderSummariesQuery(), cancellationToken);

    [HttpPut("{id:int}/role")]
    [Authorize(Roles = RoleNames.SuperAdmin)]
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

    [HttpDelete("{id:int}")]
    [Authorize(Roles = RoleNames.SuperAdmin)]
    public async Task<ActionResult<DeleteUserResponse>> DeleteUser(int id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await mediator.Send(new DeleteUserCommand(id, GetUserId()), cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpGet("menu-permissions")]
    [Authorize(Roles = RoleNames.SuperAdmin)]
    public Task<IReadOnlyCollection<RoleMenuPermissionsDto>> GetMenuPermissions(CancellationToken cancellationToken) =>
        mediator.Send(new GetRoleMenuPermissionsQuery(), cancellationToken);

    [HttpPut("menu-permissions/{role}")]
    [Authorize(Roles = RoleNames.SuperAdmin)]
    public async Task<ActionResult<RoleMenuPermissionsDto>> UpdateMenuPermissions(string role, [FromBody] UpdateRoleMenuPermissionsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await mediator.Send(new UpdateRoleMenuPermissionsCommand(role, request.EnabledMenuKeys), cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
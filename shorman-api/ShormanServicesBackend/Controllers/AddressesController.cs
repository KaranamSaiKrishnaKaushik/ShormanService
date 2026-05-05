using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShormanServicesBackend.Api.Contracts;
using ShormanServicesBackend.Api.Features;
using ShormanServicesBackend.Api.Security;

namespace ShormanServicesBackend.Controllers;

[ApiController]
[Route("api/addresses")]
[Authorize(Roles = $"{RoleNames.SuperAdmin},{RoleNames.Admin},{RoleNames.Customer}")]
public class AddressesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyCollection<AddressDto>> GetAddresses(CancellationToken cancellationToken) =>
        mediator.Send(new GetAddressesQuery(GetUserId()), cancellationToken);

    [HttpPost]
    public Task<AddressDto> AddAddress([FromBody] CreateAddressRequest request, CancellationToken cancellationToken) =>
        mediator.Send(new AddAddressCommand(GetUserId(), request), cancellationToken);

    [HttpPut("{id:int}")]
    public async Task<ActionResult<AddressDto>> UpdateAddress(int id, [FromBody] UpdateAddressRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UpdateAddressCommand(GetUserId(), id, request), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAddress(int id, CancellationToken cancellationToken)
    {
        var deleted = await mediator.Send(new DeleteAddressCommand(GetUserId(), id), cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPatch("{id:int}/default")]
    public async Task<ActionResult<AddressDto>> SetDefault(int id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SetDefaultAddressCommand(GetUserId(), id), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

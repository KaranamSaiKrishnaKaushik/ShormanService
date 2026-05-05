using MediatR;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShormanServicesBackend.Api.Contracts;
using ShormanServicesBackend.Api.Features;
using ShormanServicesBackend.Api.Security;

namespace ShormanServicesBackend.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IMediator mediator) : ControllerBase
{
    [Authorize(AuthenticationSchemes = AuthSchemes.Auth0)]
    [HttpPost("exchange")]
    public async Task<ActionResult<AuthResponse>> Exchange([FromBody] Auth0ExchangeRequest request, CancellationToken cancellationToken)
    {
        var email = string.IsNullOrWhiteSpace(request.Email)
            ? User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email")
            : request.Email;

        if (string.IsNullOrWhiteSpace(email))
        {
            return Unauthorized(new { message = "The Auth0 login did not provide an email address." });
        }

        var firstName = string.IsNullOrWhiteSpace(request.FirstName)
            ? User.FindFirstValue(ClaimTypes.GivenName) ?? User.FindFirstValue("given_name")
            : request.FirstName;
        var lastName = string.IsNullOrWhiteSpace(request.LastName)
            ? User.FindFirstValue(ClaimTypes.Surname) ?? User.FindFirstValue("family_name")
            : request.LastName;

        return Ok(await mediator.Send(new Auth0ExchangeCommand(email, firstName, lastName), cancellationToken));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await mediator.Send(new LoginCommand(request), cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return Unauthorized(new { message = exception.Message });
        }
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await mediator.Send(new RegisterCommand(request), cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }
}

using MediatR;
using Microsoft.EntityFrameworkCore;
using ShormanServicesBackend.Api.Contracts;
using ShormanServicesBackend.Api.Persistence;
using ShormanServicesBackend.Api.Persistence.Entities;
using ShormanServicesBackend.Api.Security;

namespace ShormanServicesBackend.Api.Features;

public record LoginCommand(LoginRequest Request) : IRequest<AuthResponse>;
public record RegisterCommand(RegisterRequest Request) : IRequest<AuthResponse>;

public class LoginCommandHandler(ApiDbContext dbContext, JwtTokenService jwtTokenService) : IRequestHandler<LoginCommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Email == request.Request.Email.Trim().ToLowerInvariant(), cancellationToken)
            ?? throw new InvalidOperationException("Invalid email or password.");

        if (!BCrypt.Net.BCrypt.Verify(request.Request.Password, user.PasswordHash))
        {
            throw new InvalidOperationException("Invalid email or password.");
        }

        return ToAuthResponse(user, jwtTokenService);
    }

    internal static AuthResponse ToAuthResponse(ApiUser user, JwtTokenService jwtTokenService)
    {
        return new AuthResponse(
            jwtTokenService.CreateToken(user),
            new UserDto(user.Id, user.Email, user.FirstName, user.LastName, user.Phone, user.CreatedAtUtc.ToString("O")));
    }
}

public class RegisterCommandHandler(ApiDbContext dbContext, JwtTokenService jwtTokenService) : IRequestHandler<RegisterCommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var email = request.Request.Email.Trim().ToLowerInvariant();
        if (await dbContext.Users.AnyAsync(x => x.Email == email, cancellationToken))
        {
            throw new InvalidOperationException("Email already registered.");
        }

        var user = new ApiUser
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Request.Password),
            FirstName = request.Request.FirstName.Trim(),
            LastName = request.Request.LastName.Trim(),
            Phone = request.Request.Phone?.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return LoginCommandHandler.ToAuthResponse(user, jwtTokenService);
    }
}

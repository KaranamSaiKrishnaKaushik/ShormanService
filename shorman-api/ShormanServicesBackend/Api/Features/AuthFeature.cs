using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ShormanServicesBackend.Api.Contracts;
using ShormanServicesBackend.Api.Persistence;
using ShormanServicesBackend.Api.Persistence.Entities;
using ShormanServicesBackend.Api.Security;

namespace ShormanServicesBackend.Api.Features;

public record LoginCommand(LoginRequest Request) : IRequest<AuthResponse>;
public record RegisterCommand(RegisterRequest Request) : IRequest<AuthResponse>;
public record Auth0ExchangeCommand(string Email, string? FirstName, string? LastName) : IRequest<AuthResponse>;

public class LoginCommandHandler(ApiDbContext dbContext, JwtTokenService jwtTokenService) : IRequestHandler<LoginCommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.Email == request.Request.Email.Trim().ToLowerInvariant(), cancellationToken)
            ?? throw new InvalidOperationException("Invalid email or password.");

        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            throw new InvalidOperationException("Invalid email or password.");
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Request.Password, user.PasswordHash))
        {
            throw new InvalidOperationException("Invalid email or password.");
        }

        return ToAuthResponse(user, jwtTokenService);
    }

    internal static AuthResponse ToAuthResponse(ApiUser user, JwtTokenService jwtTokenService)
    {
        var roles = user.UserRoles
            .Select(x => x.Role.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new AuthResponse(
            jwtTokenService.CreateToken(user),
            new UserDto(user.Id, user.Email, user.FirstName, user.LastName, user.Phone, user.CreatedAtUtc.ToString("O"), roles));
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
        await AuthFeatureShared.AssignSingleRoleAsync(dbContext, user.Id, RoleNames.Customer, cancellationToken);

        user = await AuthFeatureShared.LoadUserWithRolesAsync(dbContext, user.Id, cancellationToken);

        return LoginCommandHandler.ToAuthResponse(user, jwtTokenService);
    }
}

public class Auth0ExchangeCommandHandler(ApiDbContext dbContext, JwtTokenService jwtTokenService, IConfiguration configuration) : IRequestHandler<Auth0ExchangeCommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(Auth0ExchangeCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Email == email, cancellationToken);

        if (user is null)
        {
            var fallbackFirstName = !string.IsNullOrWhiteSpace(request.FirstName)
                ? request.FirstName.Trim()
                : email.Split('@')[0];
            var fallbackLastName = !string.IsNullOrWhiteSpace(request.LastName)
                ? request.LastName.Trim()
                : "User";

            user = new ApiUser
            {
                Email = email,
                PasswordHash = string.Empty,
                FirstName = fallbackFirstName,
                LastName = fallbackLastName,
                CreatedAtUtc = DateTime.UtcNow
            };

            dbContext.Users.Add(user);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(request.FirstName))
            {
                user.FirstName = request.FirstName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.LastName))
            {
                user.LastName = request.LastName.Trim();
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        var bootstrapSuperAdminEmails = configuration
            .GetSection("RoleBootstrap:SuperAdminEmails")
            .Get<string[]>()?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? [];

        var desiredRole = bootstrapSuperAdminEmails.Contains(email)
            ? RoleNames.SuperAdmin
            : RoleNames.Customer;

        await AuthFeatureShared.AssignRoleIfMissingOrDifferentAsync(dbContext, user.Id, desiredRole, cancellationToken);

        user = await AuthFeatureShared.LoadUserWithRolesAsync(dbContext, user.Id, cancellationToken);

        return LoginCommandHandler.ToAuthResponse(user, jwtTokenService);
    }
}

internal static class AuthFeatureShared
{
    public static async Task AssignDefaultRoleIfMissingAsync(ApiDbContext dbContext, int userId, CancellationToken cancellationToken)
    {
        var hasRole = await dbContext.UserRoles.AnyAsync(x => x.UserId == userId, cancellationToken);
        if (hasRole)
        {
            return;
        }

        await AssignSingleRoleAsync(dbContext, userId, RoleNames.Customer, cancellationToken);
    }

    public static async Task AssignRoleIfMissingOrDifferentAsync(ApiDbContext dbContext, int userId, string roleName, CancellationToken cancellationToken)
    {
        var currentRoleNames = await dbContext.UserRoles
            .Where(x => x.UserId == userId)
            .Select(x => x.Role.Name)
            .ToArrayAsync(cancellationToken);

        if (currentRoleNames.Length == 1 && string.Equals(currentRoleNames[0], roleName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await AssignSingleRoleAsync(dbContext, userId, roleName, cancellationToken);
    }

    public static async Task AssignSingleRoleAsync(ApiDbContext dbContext, int userId, string roleName, CancellationToken cancellationToken)
    {
        var roleId = await dbContext.Roles
            .Where(x => x.Name == roleName)
            .Select(x => x.Id)
            .SingleAsync(cancellationToken);

        var existingRoles = await dbContext.UserRoles.Where(x => x.UserId == userId).ToListAsync(cancellationToken);
        if (existingRoles.Count > 0)
        {
            dbContext.UserRoles.RemoveRange(existingRoles);
        }

        dbContext.UserRoles.Add(new ApiUserRole
        {
            UserId = userId,
            RoleId = roleId
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public static Task<ApiUser> LoadUserWithRolesAsync(ApiDbContext dbContext, int userId, CancellationToken cancellationToken) =>
        dbContext.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .SingleAsync(x => x.Id == userId, cancellationToken);
}

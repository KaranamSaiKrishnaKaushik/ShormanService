using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using System.Security.Cryptography;
using ShormanServicesBackend.Api.Contracts;
using ShormanServicesBackend.Api.Persistence;
using ShormanServicesBackend.Api.Persistence.Entities;
using ShormanServicesBackend.Api.Security;

namespace ShormanServicesBackend.Api.Features;

public record LoginCommand(LoginRequest Request) : IRequest<AuthResponse>;
public record RegisterCommand(RegisterRequest Request) : IRequest<RegisterResponse>;
public record VerifyEmailCommand(VerifyEmailRequest Request) : IRequest<AuthResponse>;
public record RequestPasswordResetCommand(PasswordResetRequest Request) : IRequest<PasswordResetRequestResponse>;
public record ConfirmPasswordResetCommand(PasswordResetConfirmRequest Request) : IRequest<object>;
public record Auth0ExchangeCommand(string Email, string? FirstName, string? LastName) : IRequest<AuthResponse>;
public record UpdateCurrentUserProfileCommand(int UserId, UpdateCurrentUserProfileRequest Request) : IRequest<UserDto>;

public class LoginCommandHandler(ApiDbContext dbContext, JwtTokenService jwtTokenService) : IRequestHandler<LoginCommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.Email == request.Request.Email.Trim().ToLowerInvariant(), cancellationToken)
            ?? throw new InvalidOperationException("Invalid email or password.");

        if (user.IsDeleted)
        {
            throw new InvalidOperationException("Invalid email or password.");
        }

        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            throw new InvalidOperationException("Invalid email or password.");
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Request.Password, user.PasswordHash))
        {
            throw new InvalidOperationException("Invalid email or password.");
        }

        if (!user.IsEmailVerified)
        {
            throw new InvalidOperationException("Email not verified. Verify your email before signing in.");
        }

        return ToAuthResponse(user, jwtTokenService);
    }

    internal static AuthResponse ToAuthResponse(ApiUser user, JwtTokenService jwtTokenService)
    {
        var userDto = ToUserDto(user);

        return new AuthResponse(
            jwtTokenService.CreateToken(user),
            userDto);
    }

    internal static UserDto ToUserDto(ApiUser user)
    {
        var roles = user.UserRoles
            .Select(x => x.Role.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new UserDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.DisplayName,
            user.ThemePreference,
            user.Phone,
            user.CreatedAtUtc.ToString("O"),
            roles);
    }
}

public class RegisterCommandHandler(ApiDbContext dbContext) : IRequestHandler<RegisterCommand, RegisterResponse>
{
    public async Task<RegisterResponse> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var email = request.Request.Email.Trim().ToLowerInvariant();
        if (await dbContext.Users.AnyAsync(x => x.Email == email, cancellationToken))
        {
            throw new InvalidOperationException("Email already registered.");
        }

        var verificationCode = AuthFeatureShared.CreateOneTimeCode();

        var user = new ApiUser
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Request.Password),
            FirstName = request.Request.FirstName.Trim(),
            LastName = request.Request.LastName.Trim(),
            Phone = request.Request.Phone?.Trim(),
            IsEmailVerified = false,
            EmailVerificationCode = verificationCode,
            EmailVerificationExpiresAtUtc = DateTime.UtcNow.AddMinutes(15),
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        await AuthFeatureShared.AssignSingleRoleAsync(dbContext, user.Id, RoleNames.Customer, cancellationToken);

        return new RegisterResponse(
            user.Email,
            "Account created. Use the verification code returned by the app to verify your email before signing in.",
            true,
            verificationCode);
    }
}

public class VerifyEmailCommandHandler(ApiDbContext dbContext, JwtTokenService jwtTokenService) : IRequestHandler<VerifyEmailCommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        var email = request.Request.Email.Trim().ToLowerInvariant();
        var code = request.Request.Code.Trim();
        var user = await dbContext.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.Email == email, cancellationToken)
            ?? throw new InvalidOperationException("Verification code is invalid or expired.");

        if (user.IsEmailVerified)
        {
            return LoginCommandHandler.ToAuthResponse(user, jwtTokenService);
        }

        if (
            string.IsNullOrWhiteSpace(user.EmailVerificationCode) ||
            !string.Equals(user.EmailVerificationCode, code, StringComparison.Ordinal) ||
            user.EmailVerificationExpiresAtUtc is null ||
            user.EmailVerificationExpiresAtUtc < DateTime.UtcNow)
        {
            throw new InvalidOperationException("Verification code is invalid or expired.");
        }

        user.IsEmailVerified = true;
        user.EmailVerificationCode = null;
        user.EmailVerificationExpiresAtUtc = null;
        user.PasswordResetCode = null;
        user.PasswordResetExpiresAtUtc = null;
        await dbContext.SaveChangesAsync(cancellationToken);

        return LoginCommandHandler.ToAuthResponse(user, jwtTokenService);
    }
}

public class RequestPasswordResetCommandHandler(ApiDbContext dbContext) : IRequestHandler<RequestPasswordResetCommand, PasswordResetRequestResponse>
{
    public async Task<PasswordResetRequestResponse> Handle(RequestPasswordResetCommand request, CancellationToken cancellationToken)
    {
        var email = request.Request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Email == email, cancellationToken);

        if (user is null || string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return new PasswordResetRequestResponse(
                "If an account exists for that email, a reset code has been generated. In local development the app shows it on screen.",
                null);
        }

        if (!user.IsEmailVerified)
        {
            throw new InvalidOperationException("Verify your email before resetting the password.");
        }

        user.PasswordResetCode = AuthFeatureShared.CreateOneTimeCode();
        user.PasswordResetExpiresAtUtc = DateTime.UtcNow.AddMinutes(15);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new PasswordResetRequestResponse(
            "If an account exists for that email, a reset code has been generated. In local development the app shows it on screen.",
            user.PasswordResetCode);
    }
}

public class ConfirmPasswordResetCommandHandler(ApiDbContext dbContext) : IRequestHandler<ConfirmPasswordResetCommand, object>
{
    public async Task<object> Handle(ConfirmPasswordResetCommand request, CancellationToken cancellationToken)
    {
        var email = request.Request.Email.Trim().ToLowerInvariant();
        var code = request.Request.Code.Trim();
        var newPassword = request.Request.NewPassword.Trim();

        if (newPassword.Length < 6)
        {
            throw new InvalidOperationException("Password must be at least 6 characters long.");
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Email == email, cancellationToken)
            ?? throw new InvalidOperationException("Reset code is invalid or expired.");

        if (
            string.IsNullOrWhiteSpace(user.PasswordResetCode) ||
            !string.Equals(user.PasswordResetCode, code, StringComparison.Ordinal) ||
            user.PasswordResetExpiresAtUtc is null ||
            user.PasswordResetExpiresAtUtc < DateTime.UtcNow)
        {
            throw new InvalidOperationException("Reset code is invalid or expired.");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.PasswordResetCode = null;
        user.PasswordResetExpiresAtUtc = null;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new { message = "Password reset successfully. You can now sign in with the new password." };
    }
}

public class Auth0ExchangeCommandHandler(ApiDbContext dbContext, JwtTokenService jwtTokenService, IConfiguration configuration) : IRequestHandler<Auth0ExchangeCommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(Auth0ExchangeCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Email == email, cancellationToken);

        if (user is not null && user.IsDeleted)
        {
            throw new InvalidOperationException("This account has been deleted.");
        }

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
                IsEmailVerified = true,
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

            user.IsEmailVerified = true;
            user.EmailVerificationCode = null;
            user.EmailVerificationExpiresAtUtc = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        var bootstrapSuperAdminEmails = AuthFeatureShared.GetBootstrapSuperAdminEmails(configuration);

        if (bootstrapSuperAdminEmails.Contains(email))
        {
            await AuthFeatureShared.AssignRoleIfMissingOrDifferentAsync(dbContext, user.Id, RoleNames.SuperAdmin, cancellationToken);
        }
        else
        {
            await AuthFeatureShared.AssignDefaultRoleIfMissingAsync(dbContext, user.Id, cancellationToken);
        }

        user = await AuthFeatureShared.LoadUserWithRolesAsync(dbContext, user.Id, cancellationToken);

        return LoginCommandHandler.ToAuthResponse(user, jwtTokenService);
    }
}

public class UpdateCurrentUserProfileCommandHandler(ApiDbContext dbContext) : IRequestHandler<UpdateCurrentUserProfileCommand, UserDto>
{
    public async Task<UserDto> Handle(UpdateCurrentUserProfileCommand request, CancellationToken cancellationToken)
    {
        var user = await AuthFeatureShared.LoadUserWithRolesAsync(dbContext, request.UserId, cancellationToken);

        if (request.Request.DisplayName is not null)
        {
            var displayName = request.Request.DisplayName.Trim();
            if (displayName.Length > 80)
            {
                throw new InvalidOperationException("Display name must be 80 characters or fewer.");
            }

            user.DisplayName = displayName.Length == 0 ? null : displayName;
        }

        if (request.Request.ThemePreference is not null)
        {
            var themePreference = request.Request.ThemePreference.Trim();
            if (themePreference.Length == 0)
            {
                user.ThemePreference = null;
            }
            else if (AuthFeatureShared.IsSupportedTheme(themePreference))
            {
                user.ThemePreference = themePreference;
            }
            else
            {
                throw new InvalidOperationException("Theme preference is not supported.");
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return LoginCommandHandler.ToUserDto(user);
    }
}

internal static class AuthFeatureShared
{
    private static readonly HashSet<string> SupportedThemes =
    [
        "fresh-market",
        "terracotta",
        "coastal"
    ];

    public static string CreateOneTimeCode() => RandomNumberGenerator.GetInt32(100000, 1000000).ToString(CultureInfo.InvariantCulture);

    public static bool IsSupportedTheme(string themePreference) => SupportedThemes.Contains(themePreference);

    public static HashSet<string> GetBootstrapSuperAdminEmails(IConfiguration configuration)
    {
        var fromArraySection = configuration
            .GetSection("RoleBootstrap:SuperAdminEmails")
            .Get<string[]>()
            ?? [];

        var fromScalarValue = configuration["RoleBootstrap:SuperAdminEmails"]
            ?.Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? [];

        return fromArraySection
            .Concat(fromScalarValue)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

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

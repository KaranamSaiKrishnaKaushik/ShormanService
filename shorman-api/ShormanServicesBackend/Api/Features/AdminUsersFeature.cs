using MediatR;
using Microsoft.EntityFrameworkCore;
using ShormanServicesBackend.Api.Contracts;
using ShormanServicesBackend.Api.Persistence;
using ShormanServicesBackend.Api.Persistence.Entities;
using ShormanServicesBackend.Api.Security;

namespace ShormanServicesBackend.Api.Features;

public record GetAdminUsersQuery() : IRequest<IReadOnlyCollection<AdminUserListItemDto>>;
public record UpdateUserRoleCommand(int UserId, string Role) : IRequest<AdminUserListItemDto?>;
public record GetRoleMenuPermissionsQuery() : IRequest<IReadOnlyCollection<RoleMenuPermissionsDto>>;
public record UpdateRoleMenuPermissionsCommand(string Role, IReadOnlyCollection<string> EnabledMenuKeys) : IRequest<RoleMenuPermissionsDto>;
public record GetCurrentMenuPermissionsQuery(IReadOnlyCollection<string> Roles) : IRequest<CurrentMenuPermissionsDto>;

public class GetAdminUsersQueryHandler(ApiDbContext dbContext) : IRequestHandler<GetAdminUsersQuery, IReadOnlyCollection<AdminUserListItemDto>>
{
    public async Task<IReadOnlyCollection<AdminUserListItemDto>> Handle(GetAdminUsersQuery request, CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .AsNoTracking()
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .OrderBy(x => x.FirstName)
            .ThenBy(x => x.LastName)
            .Select(ToDto())
            .ToListAsync(cancellationToken);
    }

    private static System.Linq.Expressions.Expression<Func<ApiUser, AdminUserListItemDto>> ToDto() => user =>
        new AdminUserListItemDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Phone,
            user.CreatedAtUtc.ToString("O"),
            user.UserRoles.Select(x => x.Role.Name).OrderBy(x => x).ToArray());
}

public class UpdateUserRoleCommandHandler(ApiDbContext dbContext) : IRequestHandler<UpdateUserRoleCommand, AdminUserListItemDto?>
{
    public async Task<AdminUserListItemDto?> Handle(UpdateUserRoleCommand request, CancellationToken cancellationToken)
    {
        if (!RoleNames.IsValid(request.Role))
        {
            throw new InvalidOperationException("Unsupported role.");
        }

        var user = await dbContext.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.Id == request.UserId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var currentRoles = user.UserRoles.Select(x => x.Role.Name).ToArray();
        var isDemotingLastSuperAdmin = currentRoles.Contains(RoleNames.SuperAdmin, StringComparer.OrdinalIgnoreCase)
            && !string.Equals(request.Role, RoleNames.SuperAdmin, StringComparison.OrdinalIgnoreCase)
            && await CountSuperAdminsAsync(cancellationToken) <= 1;

        if (isDemotingLastSuperAdmin)
        {
            throw new InvalidOperationException("At least one SuperAdmin must remain assigned.");
        }

        var role = await dbContext.Roles.SingleAsync(x => x.Name == request.Role, cancellationToken);

        if (user.UserRoles.Count > 0)
        {
            dbContext.UserRoles.RemoveRange(user.UserRoles);
        }

        dbContext.UserRoles.Add(new ApiUserRole
        {
            UserId = user.Id,
            RoleId = role.Id
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        user = await dbContext.Users
            .AsNoTracking()
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .SingleAsync(x => x.Id == request.UserId, cancellationToken);

        return new AdminUserListItemDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Phone,
            user.CreatedAtUtc.ToString("O"),
            user.UserRoles.Select(x => x.Role.Name).OrderBy(x => x).ToArray());
    }

    private Task<int> CountSuperAdminsAsync(CancellationToken cancellationToken) =>
        dbContext.UserRoles.CountAsync(x => x.Role.Name == RoleNames.SuperAdmin, cancellationToken);
}

public class GetRoleMenuPermissionsQueryHandler(ApiDbContext dbContext) : IRequestHandler<GetRoleMenuPermissionsQuery, IReadOnlyCollection<RoleMenuPermissionsDto>>
{
    public async Task<IReadOnlyCollection<RoleMenuPermissionsDto>> Handle(GetRoleMenuPermissionsQuery request, CancellationToken cancellationToken)
    {
        var roles = await dbContext.Roles
            .AsNoTracking()
            .Include(x => x.MenuPermissions)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return roles.Select(AdminUsersFeatureMappings.MapRoleMenuPermissions).ToArray();
    }
}

public class UpdateRoleMenuPermissionsCommandHandler(ApiDbContext dbContext) : IRequestHandler<UpdateRoleMenuPermissionsCommand, RoleMenuPermissionsDto>
{
    public async Task<RoleMenuPermissionsDto> Handle(UpdateRoleMenuPermissionsCommand request, CancellationToken cancellationToken)
    {
        if (!RoleNames.IsValid(request.Role))
        {
            throw new InvalidOperationException("Unsupported role.");
        }

        if (string.Equals(request.Role, RoleNames.SuperAdmin, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("SuperAdmin permissions are fixed.");
        }

        var invalidKey = request.EnabledMenuKeys.FirstOrDefault(key => !MenuPermissionKeys.IsValid(key));
        if (!string.IsNullOrWhiteSpace(invalidKey))
        {
            throw new InvalidOperationException($"Unsupported menu key '{invalidKey}'.");
        }

        var role = await dbContext.Roles
            .Include(x => x.MenuPermissions)
            .SingleAsync(x => x.Name == request.Role, cancellationToken);

        if (role.MenuPermissions.Count > 0)
        {
            dbContext.RoleMenuPermissions.RemoveRange(role.MenuPermissions);
        }

        var distinctKeys = request.EnabledMenuKeys
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var key in distinctKeys)
        {
            dbContext.RoleMenuPermissions.Add(new ApiRoleMenuPermission
            {
                RoleId = role.Id,
                MenuKey = key,
                IsEnabled = true
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        role = await dbContext.Roles
            .AsNoTracking()
            .Include(x => x.MenuPermissions)
            .SingleAsync(x => x.Name == request.Role, cancellationToken);

        return AdminUsersFeatureMappings.MapRoleMenuPermissions(role);
    }
}

public class GetCurrentMenuPermissionsQueryHandler(ApiDbContext dbContext) : IRequestHandler<GetCurrentMenuPermissionsQuery, CurrentMenuPermissionsDto>
{
    public async Task<CurrentMenuPermissionsDto> Handle(GetCurrentMenuPermissionsQuery request, CancellationToken cancellationToken)
    {
        if (request.Roles.Any(role => string.Equals(role, RoleNames.SuperAdmin, StringComparison.OrdinalIgnoreCase)))
        {
            return new CurrentMenuPermissionsDto(MenuPermissionKeys.All);
        }

        var roles = request.Roles
            .Where(RoleNames.IsValid)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var enabledKeys = await dbContext.RoleMenuPermissions
            .AsNoTracking()
            .Where(x => roles.Contains(x.Role.Name))
            .Where(x => x.IsEnabled)
            .Select(x => x.MenuKey)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        return new CurrentMenuPermissionsDto(enabledKeys);
    }
}

internal static class AdminUsersFeatureMappings
{
    internal static RoleMenuPermissionsDto MapRoleMenuPermissions(ApiRole role)
    {
        var enabledKeys = role.MenuPermissions
            .Where(x => x.IsEnabled)
            .Select(x => x.MenuKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var permissions = MenuPermissionKeys.All
            .Select(menuKey => new RoleMenuPermissionDto(role.Name, menuKey, IsMenuEnabled(role.Name, menuKey, enabledKeys)))
            .ToArray();

        return new RoleMenuPermissionsDto(role.Name, permissions);
    }

    private static bool IsMenuEnabled(string role, string menuKey, IReadOnlySet<string> enabledKeys)
    {
        if (string.Equals(role, RoleNames.SuperAdmin, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (enabledKeys.Contains(menuKey))
        {
            return true;
        }

        return MenuPermissionKeys.GetDefaultEnabledKeys(role)
            .Contains(menuKey, StringComparer.OrdinalIgnoreCase);
    }
}
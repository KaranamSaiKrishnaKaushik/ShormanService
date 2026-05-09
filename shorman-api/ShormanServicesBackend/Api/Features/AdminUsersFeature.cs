using MediatR;
using Microsoft.EntityFrameworkCore;
using ShormanServicesBackend.Api.Contracts;
using ShormanServicesBackend.Api.Persistence;
using ShormanServicesBackend.Api.Persistence.Entities;
using ShormanServicesBackend.Api.Security;

namespace ShormanServicesBackend.Api.Features;

public record GetAdminUsersQuery() : IRequest<IReadOnlyCollection<AdminUserListItemDto>>;
public record GetAdminOrderSummariesQuery() : IRequest<IReadOnlyCollection<AdminOrderSummaryDto>>;
public record UpdateUserRoleCommand(int UserId, string Role) : IRequest<AdminUserListItemDto?>;
public record DeleteUserCommand(int UserId, int RequestedByUserId) : IRequest<DeleteUserResponse?>;
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
            .Where(x => !x.IsDeleted)
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
            .SingleOrDefaultAsync(x => x.Id == request.UserId && !x.IsDeleted, cancellationToken);

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

public class GetAdminOrderSummariesQueryHandler(ApiDbContext dbContext) : IRequestHandler<GetAdminOrderSummariesQuery, IReadOnlyCollection<AdminOrderSummaryDto>>
{
    public async Task<IReadOnlyCollection<AdminOrderSummaryDto>> Handle(GetAdminOrderSummariesQuery request, CancellationToken cancellationToken)
    {
        var orders = await dbContext.Orders
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Address)
            .Include(x => x.AssignedRider)
            .Include(x => x.Items)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return orders.Select(AdminUsersFeatureMappings.MapAdminOrderSummary).ToArray();
    }
}

public class DeleteUserCommandHandler(ApiDbContext dbContext) : IRequestHandler<DeleteUserCommand, DeleteUserResponse?>
{
    public async Task<DeleteUserResponse?> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.Id == request.UserId && !x.IsDeleted, cancellationToken);

        if (user is null)
        {
            return null;
        }

        if (user.Id == request.RequestedByUserId)
        {
            throw new InvalidOperationException("You cannot delete your own account.");
        }

        if (user.UserRoles.Any(x => string.Equals(x.Role.Name, RoleNames.SuperAdmin, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("SuperAdmin accounts cannot be deleted.");
        }

        var displayName = $"{user.FirstName} {user.LastName}".Trim();
        var snapshotName = string.IsNullOrWhiteSpace(displayName) ? user.Email : displayName;

        await dbContext.Orders
            .Where(x => x.UserId == user.Id)
            .Where(x => x.CustomerNameSnapshot == null || x.CustomerEmailSnapshot == null)
            .ExecuteUpdateAsync(updates => updates
                .SetProperty(x => x.CustomerNameSnapshot, snapshotName)
                .SetProperty(x => x.CustomerEmailSnapshot, user.Email), cancellationToken);

        var cartIds = await dbContext.Carts
            .Where(x => x.UserId == user.Id)
            .Select(x => x.Id)
            .ToArrayAsync(cancellationToken);

        if (cartIds.Length > 0)
        {
            await dbContext.CartItems.Where(x => cartIds.Contains(x.CartId)).ExecuteDeleteAsync(cancellationToken);
            await dbContext.Carts.Where(x => cartIds.Contains(x.Id)).ExecuteDeleteAsync(cancellationToken);
        }

        await dbContext.UserRoles.Where(x => x.UserId == user.Id).ExecuteDeleteAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM refresh_tokens WHERE UserId = {0}", [user.Id], cancellationToken);

        user.Email = $"deleted-user-{user.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}@deleted.local";
        user.PasswordHash = string.Empty;
        user.FirstName = "Deleted";
        user.LastName = "Account";
        user.Phone = null;
        user.IsEmailVerified = false;
        user.EmailVerificationCode = null;
        user.EmailVerificationExpiresAtUtc = null;
        user.PasswordResetCode = null;
        user.PasswordResetExpiresAtUtc = null;
        user.IsDeleted = true;
        user.DeletedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new DeleteUserResponse(user.Id, "User account deleted while preserving order history for audit.");
    }
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
    internal static AdminOrderSummaryDto MapAdminOrderSummary(ApiOrder order)
    {
        var customerName = string.IsNullOrWhiteSpace(order.CustomerNameSnapshot)
            ? $"{order.User.FirstName} {order.User.LastName}".Trim()
            : order.CustomerNameSnapshot;
        var customerEmail = string.IsNullOrWhiteSpace(order.CustomerEmailSnapshot)
            ? order.User.Email
            : order.CustomerEmailSnapshot;
        var orderDto = GetOrdersQueryHandler.Map(order);

        return new AdminOrderSummaryDto(
            orderDto.Id,
            orderDto.UserId,
            string.IsNullOrWhiteSpace(customerName) ? customerEmail : customerName,
            customerEmail,
            orderDto.Status,
            orderDto.PaymentMethod,
            orderDto.PaymentStatus,
            orderDto.AddressId,
            orderDto.DeliveryAddress,
            orderDto.Items,
            orderDto.Subtotal,
            orderDto.DeliveryFee,
            orderDto.Total,
            orderDto.AssignedRiderId,
            orderDto.AssignedRiderName,
            orderDto.CreatedAt,
            orderDto.UpdatedAt,
            orderDto.AcceptedAt,
            orderDto.PickedUpAt,
            orderDto.OutForDeliveryAt,
            orderDto.DeliveredAt,
            orderDto.CashCollectedAt,
            orderDto.CompletedAt);
    }

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
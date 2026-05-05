using MediatR;
using Microsoft.EntityFrameworkCore;
using ShormanServicesBackend.Api.Contracts;
using ShormanServicesBackend.Api.Persistence;
using ShormanServicesBackend.Api.Persistence.Entities;
using ShormanServicesBackend.Api.Security;

namespace ShormanServicesBackend.Api.Features;

public record GetAdminUsersQuery() : IRequest<IReadOnlyCollection<AdminUserListItemDto>>;
public record UpdateUserRoleCommand(int UserId, string Role) : IRequest<AdminUserListItemDto?>;

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
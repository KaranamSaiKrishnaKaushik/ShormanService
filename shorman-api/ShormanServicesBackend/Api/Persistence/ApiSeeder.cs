using Microsoft.EntityFrameworkCore;
using ShormanServicesBackend.Api.Persistence.Entities;
using ShormanServicesBackend.Api.Security;

namespace ShormanServicesBackend.Api.Persistence;

public static class ApiSeeder
{
    public static async Task SeedAsync(ApiDbContext dbContext)
    {
        await SeedRolesAsync(dbContext);
        await SeedRoleMenuPermissionsAsync(dbContext);

        // ── 1. Categories ──────────────────────────────────────────────────────
        if (!await dbContext.Categories.AnyAsync())
        {
            dbContext.Categories.AddRange(
                new ApiCategory { Name = "Eggs & Dairy",        Slug = "eggs-dairy",          Icon = "🥚" },
                new ApiCategory { Name = "Meat",                Slug = "meat",                Icon = "🥩" },
                new ApiCategory { Name = "Fruits & Vegetables", Slug = "fruits-vegetables",   Icon = "🥦" },
                new ApiCategory { Name = "Bakery",              Slug = "bakery",              Icon = "🍞" },
                new ApiCategory { Name = "Beverages",           Slug = "beverages",           Icon = "🥤" },
                new ApiCategory { Name = "Snacks",              Slug = "snacks",              Icon = "🍿" }
            );
            await dbContext.SaveChangesAsync();
        }

        // ── 2. Supermarkets ────────────────────────────────────────────────────
        if (!await dbContext.Supermarkets.AnyAsync())
        {
            dbContext.Supermarkets.AddRange(
                new ApiSupermarket { Name = "REWE",  Slug = "rewe",  Color = "#CC0000" },
                new ApiSupermarket { Name = "ALDI",  Slug = "aldi",  Color = "#00519C" },
                new ApiSupermarket { Name = "PENNY", Slug = "penny", Color = "#CC0000" },
                new ApiSupermarket { Name = "LIDL",  Slug = "lidl",  Color = "#0050AA" }
            );
            await dbContext.SaveChangesAsync();
        }

        // ── 3. Users ───────────────────────────────────────────────────────────
        if (!await dbContext.Users.AnyAsync())
        {
            dbContext.Users.AddRange(
                new ApiUser
                {
                    Email        = "demo@shorman.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("demo123"),
                    FirstName    = "Super",
                    LastName     = "Admin",
                    Phone        = "+49 123 456789",
                    CreatedAtUtc = DateTime.UtcNow
                },
                new ApiUser
                {
                    Email        = "admin@shorman.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                    FirstName    = "Admin",
                    LastName     = "User",
                    Phone        = "+49 222 333444",
                    CreatedAtUtc = DateTime.UtcNow
                },
                new ApiUser
                {
                    Email        = "test@test.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("test123"),
                    FirstName    = "Customer",
                    LastName     = "User",
                    Phone        = "+49 987 654321",
                    CreatedAtUtc = DateTime.UtcNow
                },
                new ApiUser
                {
                    Email        = "rider@shorman.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("rider123"),
                    FirstName    = "Rider",
                    LastName     = "User",
                    Phone        = "+49 555 123456",
                    CreatedAtUtc = DateTime.UtcNow
                }
            );
            await dbContext.SaveChangesAsync();
        }

        await SeedUserRoleAssignmentsAsync(dbContext);

        // ── 4. Addresses (resolve demo user ID) ────────────────────────────────
        if (!await dbContext.Addresses.AnyAsync())
        {
            var demoUserId = await dbContext.Users
                .Where(u => u.Email == "demo@shorman.com")
                .Select(u => u.Id)
                .FirstAsync();

            dbContext.Addresses.AddRange(
                new ApiAddress { UserId = demoUserId, Label = "Home", Street = "Hauptstraße",  HouseNumber = "123", PostalCode = "10115", City = "Berlin", Country = "Germany", IsDefault = true },
                new ApiAddress { UserId = demoUserId, Label = "Work", Street = "Alexanderplatz", HouseNumber = "5", PostalCode = "10178", City = "Berlin", Country = "Germany", IsDefault = false }
            );
            await dbContext.SaveChangesAsync();
        }
    }

    private static async Task SeedRoleMenuPermissionsAsync(ApiDbContext dbContext)
    {
        var roles = await dbContext.Roles
            .AsNoTracking()
            .ToDictionaryAsync(x => x.Name, StringComparer.OrdinalIgnoreCase);

        var existing = await dbContext.RoleMenuPermissions
            .AsNoTracking()
            .Select(x => new { x.RoleId, x.MenuKey })
            .ToListAsync();

        var existingSet = existing
            .Select(x => $"{x.RoleId}:{x.MenuKey}" )
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingPermissions = new List<ApiRoleMenuPermission>();
        foreach (var role in RoleNames.All)
        {
            if (!roles.TryGetValue(role, out var roleEntity))
            {
                continue;
            }

            foreach (var key in MenuPermissionKeys.GetDefaultEnabledKeys(role))
            {
                if (existingSet.Contains($"{roleEntity.Id}:{key}"))
                {
                    continue;
                }

                missingPermissions.Add(new ApiRoleMenuPermission
                {
                    RoleId = roleEntity.Id,
                    MenuKey = key,
                    IsEnabled = true
                });
            }
        }

        if (missingPermissions.Count == 0)
        {
            return;
        }

        dbContext.RoleMenuPermissions.AddRange(missingPermissions);
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedRolesAsync(ApiDbContext dbContext)
    {
        var existingRoles = await dbContext.Roles.Select(x => x.Name).ToListAsync();
        var missingRoles = RoleNames.All
            .Where(role => existingRoles.All(existing => !string.Equals(existing, role, StringComparison.OrdinalIgnoreCase)))
            .Select(role => new ApiRole { Name = role })
            .ToList();

        if (missingRoles.Count == 0)
        {
            return;
        }

        dbContext.Roles.AddRange(missingRoles);
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedUserRoleAssignmentsAsync(ApiDbContext dbContext)
    {
        var users = await dbContext.Users
            .ToDictionaryAsync(x => x.Email, StringComparer.OrdinalIgnoreCase);

        var roles = await dbContext.Roles
            .ToDictionaryAsync(x => x.Name, StringComparer.OrdinalIgnoreCase);

        await EnsureSingleRoleAsync(dbContext, users, roles, "demo@shorman.com", RoleNames.SuperAdmin);
        await EnsureSingleRoleAsync(dbContext, users, roles, "admin@shorman.com", RoleNames.Admin);
        await EnsureSingleRoleAsync(dbContext, users, roles, "test@test.com", RoleNames.Customer);
        await EnsureSingleRoleAsync(dbContext, users, roles, "rider@shorman.com", RoleNames.Rider);
    }

    private static async Task EnsureSingleRoleAsync(
        ApiDbContext dbContext,
        IReadOnlyDictionary<string, ApiUser> users,
        IReadOnlyDictionary<string, ApiRole> roles,
        string email,
        string roleName)
    {
        if (!users.TryGetValue(email, out var user) || !roles.TryGetValue(roleName, out var role))
        {
            return;
        }

        var existingAssignments = await dbContext.UserRoles
            .Where(x => x.UserId == user.Id)
            .ToListAsync();

        var alreadyAssigned = existingAssignments.Any(x => x.RoleId == role.Id);
        if (alreadyAssigned && existingAssignments.Count == 1)
        {
            return;
        }

        if (existingAssignments.Count > 0)
        {
            dbContext.UserRoles.RemoveRange(existingAssignments);
        }

        dbContext.UserRoles.Add(new ApiUserRole
        {
            UserId = user.Id,
            RoleId = role.Id
        });

        await dbContext.SaveChangesAsync();
    }
}

using Microsoft.EntityFrameworkCore;
using ShormanServicesBackend.Api.Persistence.Entities;
using ShormanServicesBackend.Api.Security;

namespace ShormanServicesBackend.Api.Persistence;

public static class ApiSeeder
{
    public static async Task SeedAsync(ApiDbContext dbContext)
    {
        await SeedRolesAsync(dbContext);

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

        // ── 4. Products (resolve category/supermarket IDs from DB) ────────────
        if (!await dbContext.Products.AnyAsync())
        {
            var catId  = await dbContext.Categories.ToDictionaryAsync(c => c.Slug, c => c.Id);
            var supId  = await dbContext.Supermarkets.ToDictionaryAsync(s => s.Slug, s => s.Id);

            dbContext.Products.AddRange(
                new ApiProduct { Name = "Whole Milk 1L",         Price = 1.29m, CategoryId = catId["eggs-dairy"],        SupermarketId = supId["rewe"],  IsAvailable = true, Unit = "1L",      Stock = 50 },
                new ApiProduct { Name = "Free Range Eggs x12",   Price = 3.49m, CategoryId = catId["eggs-dairy"],        SupermarketId = supId["aldi"],  IsAvailable = true, Unit = "12 pcs",  Stock = 30 },
                new ApiProduct { Name = "Chicken Breast 500g",   Price = 4.99m, CategoryId = catId["meat"],              SupermarketId = supId["rewe"],  IsAvailable = true, Unit = "500g",    Stock = 20 },
                new ApiProduct { Name = "Ground Beef 400g",      Price = 5.49m, CategoryId = catId["meat"],              SupermarketId = supId["penny"], IsAvailable = true, Unit = "400g",    Stock = 15 },
                new ApiProduct { Name = "Bananas 1kg",           Price = 1.49m, CategoryId = catId["fruits-vegetables"], SupermarketId = supId["aldi"],  IsAvailable = true, Unit = "1kg",     Stock = 100 },
                new ApiProduct { Name = "Apples Bag 1.5kg",      Price = 2.99m, CategoryId = catId["fruits-vegetables"], SupermarketId = supId["lidl"],  IsAvailable = true, Unit = "1.5kg",   Stock = 80 },
                new ApiProduct { Name = "Sourdough Bread",       Price = 2.49m, CategoryId = catId["bakery"],            SupermarketId = supId["rewe"],  IsAvailable = true, Unit = "500g",    Stock = 25 },
                new ApiProduct { Name = "Croissants x4",         Price = 1.89m, CategoryId = catId["bakery"],            SupermarketId = supId["penny"], IsAvailable = true, Unit = "4 pcs",   Stock = 40 },
                new ApiProduct { Name = "Orange Juice 1L",       Price = 1.99m, CategoryId = catId["beverages"],         SupermarketId = supId["aldi"],  IsAvailable = true, Unit = "1L",      Stock = 60 },
                new ApiProduct { Name = "Sparkling Water 6x500ml",Price= 2.19m, CategoryId = catId["beverages"],         SupermarketId = supId["lidl"],  IsAvailable = true, Unit = "6x500ml", Stock = 50 },
                new ApiProduct { Name = "Potato Chips 200g",     Price = 1.79m, CategoryId = catId["snacks"],            SupermarketId = supId["penny"], IsAvailable = true, Unit = "200g",    Stock = 75 },
                new ApiProduct { Name = "Mixed Nuts 250g",       Price = 3.99m, CategoryId = catId["snacks"],            SupermarketId = supId["rewe"],  IsAvailable = true, Unit = "250g",    Stock = 35 },
                new ApiProduct { Name = "Greek Yogurt 500g",     Price = 2.29m, CategoryId = catId["eggs-dairy"],        SupermarketId = supId["lidl"],  IsAvailable = true, Unit = "500g",    Stock = 45 },
                new ApiProduct { Name = "Salmon Fillet 300g",    Price = 6.99m, CategoryId = catId["meat"],              SupermarketId = supId["aldi"],  IsAvailable = true, Unit = "300g",    Stock = 10 },
                new ApiProduct { Name = "Cherry Tomatoes 500g",  Price = 2.49m, CategoryId = catId["fruits-vegetables"], SupermarketId = supId["rewe"],  IsAvailable = true, Unit = "500g",    Stock = 55 },
                new ApiProduct { Name = "Cola 1.5L",             Price = 1.59m, CategoryId = catId["beverages"],         SupermarketId = supId["penny"], IsAvailable = true, Unit = "1.5L",    Stock = 90 }
            );
            await dbContext.SaveChangesAsync();
        }

        // ── 5. Addresses (resolve demo user ID) ────────────────────────────────
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

        // ── 6. Sample orders ────────────────────────────────────────────────────
        if (!await dbContext.Orders.AnyAsync())
        {
            var demoUserId    = await dbContext.Users.Where(u => u.Email == "demo@shorman.com").Select(u => u.Id).FirstAsync();
            var homeAddressId = await dbContext.Addresses.Where(a => a.UserId == demoUserId && a.Label == "Home").Select(a => a.Id).FirstAsync();
            var productIds    = await dbContext.Products.ToDictionaryAsync(p => p.Name, p => p.Id);

            dbContext.Orders.AddRange(
                new ApiOrder
                {
                    UserId        = demoUserId,
                    Status        = "DELIVERED",
                    PaymentMethod = "PAYPAL",
                    AddressId     = homeAddressId,
                    Subtotal      = 5.27m,
                    DeliveryFee   = 3.99m,
                    Total         = 9.26m,
                    CreatedAtUtc  = DateTime.UtcNow.AddDays(-7),
                    UpdatedAtUtc  = DateTime.UtcNow.AddDays(-6),
                    Items =
                    [
                        new ApiOrderItem { ProductId = productIds["Bananas 1kg"],    ProductName = "Bananas 1kg",    Quantity = 2, UnitPrice = 1.49m, TotalPrice = 2.98m },
                        new ApiOrderItem { ProductId = productIds["Whole Milk 1L"],  ProductName = "Whole Milk 1L",  Quantity = 1, UnitPrice = 1.29m, TotalPrice = 1.29m }
                    ]
                },
                new ApiOrder
                {
                    UserId        = demoUserId,
                    Status        = "PROCESSING",
                    PaymentMethod = "BANK_TRANSFER",
                    AddressId     = homeAddressId,
                    Subtotal      = 5.98m,
                    DeliveryFee   = 3.99m,
                    Total         = 9.97m,
                    CreatedAtUtc  = DateTime.UtcNow.AddDays(-2),
                    UpdatedAtUtc  = DateTime.UtcNow.AddDays(-1),
                    Items =
                    [
                        new ApiOrderItem { ProductId = productIds["Cherry Tomatoes 500g"], ProductName = "Cherry Tomatoes 500g", Quantity = 1, UnitPrice = 2.49m, TotalPrice = 2.49m },
                        new ApiOrderItem { ProductId = productIds["Sourdough Bread"],       ProductName = "Sourdough Bread",       Quantity = 1, UnitPrice = 2.49m, TotalPrice = 2.49m }
                    ]
                }
            );
            await dbContext.SaveChangesAsync();
        }
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

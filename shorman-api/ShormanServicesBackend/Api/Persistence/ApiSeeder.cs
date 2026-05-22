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
        await SeedCategoriesAsync(dbContext);
        await SeedSupermarketsAsync(dbContext);

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
                    IsEmailVerified = true,
                    CreatedAtUtc = DateTime.UtcNow
                },
                new ApiUser
                {
                    Email        = "admin@shorman.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                    FirstName    = "Admin",
                    LastName     = "User",
                    Phone        = "+49 222 333444",
                    IsEmailVerified = true,
                    CreatedAtUtc = DateTime.UtcNow
                },
                new ApiUser
                {
                    Email        = "test@test.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("test123"),
                    FirstName    = "Customer",
                    LastName     = "User",
                    Phone        = "+49 987 654321",
                    IsEmailVerified = true,
                    CreatedAtUtc = DateTime.UtcNow
                },
                new ApiUser
                {
                    Email        = "rider@shorman.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("rider123"),
                    FirstName    = "Rider",
                    LastName     = "User",
                    Phone        = "+49 555 123456",
                    IsEmailVerified = true,
                    CreatedAtUtc = DateTime.UtcNow
                }
            );
            await dbContext.SaveChangesAsync();
        }

        var unverifiedSeedUsers = await dbContext.Users.Where(x => !x.IsEmailVerified).ToListAsync();
        if (unverifiedSeedUsers.Count > 0)
        {
            foreach (var user in unverifiedSeedUsers)
            {
                user.IsEmailVerified = true;
                user.EmailVerificationCode = null;
                user.EmailVerificationExpiresAtUtc = null;
            }

            await dbContext.SaveChangesAsync();
        }

        await SeedUserRoleAssignmentsAsync(dbContext);
        await SeedPricingPolicyAsync(dbContext);

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

        await SeedInsightsDemoOrdersAsync(dbContext, "karanamsaikrishna.kaushik@gmail.com");
    }

    private static async Task SeedInsightsDemoOrdersAsync(ApiDbContext dbContext, string email)
    {
        var targetUser = await dbContext.Users
            .SingleOrDefaultAsync(x => x.Email.ToLower() == email.ToLower());

        if (targetUser is null)
        {
            targetUser = new ApiUser
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("test123"),
                FirstName = "Karanam",
                LastName = "Customer",
                Phone = "+49 111 222333",
                IsEmailVerified = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            dbContext.Users.Add(targetUser);
            await dbContext.SaveChangesAsync();
        }

        var userAddress = await dbContext.Addresses
            .Where(x => x.UserId == targetUser.Id)
            .OrderByDescending(x => x.IsDefault)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync();

        if (userAddress is null)
        {
            userAddress = new ApiAddress
            {
                UserId = targetUser.Id,
                Label = "Home",
                Street = "Musterstraße",
                HouseNumber = "24",
                PostalCode = "10115",
                City = "Berlin",
                Country = "Germany",
                IsDefault = true
            };

            dbContext.Addresses.Add(userAddress);
            await dbContext.SaveChangesAsync();
        }

        var supermarkets = await dbContext.Supermarkets
            .ToDictionaryAsync(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var categories = await dbContext.Categories
            .ToDictionaryAsync(x => x.Name, StringComparer.OrdinalIgnoreCase);

        var requiredStores = new[] { "LIDL", "ALDI", "REWE", "PENNY", "EDEKA", "dm", "ROSSMANN" };
        var requiredCategories = new[]
        {
            "Beverages", "Bakery", "Eggs & Dairy", "Fruits & Vegetables", "Snacks",
            "Skin Care", "Hair Care", "Body & Bath", "Health & Wellness", "Meat"
        };

        if (requiredStores.Any(x => !supermarkets.ContainsKey(x)) || requiredCategories.Any(x => !categories.ContainsKey(x)))
        {
            return;
        }

        var products = new[]
        {
            new { Key = "insights-bev-lidl", Name = "Mineral Water 1.5L", Store = "LIDL", Category = "Beverages", Price = 1.45m },
            new { Key = "insights-bak-lidl", Name = "Bauernbrot 750g", Store = "LIDL", Category = "Bakery", Price = 2.95m },
            new { Key = "insights-dairy-aldi", Name = "Frische Vollmilch 1L", Store = "ALDI", Category = "Eggs & Dairy", Price = 1.29m },
            new { Key = "insights-fruit-rewe", Name = "Banane lose 1kg", Store = "REWE", Category = "Fruits & Vegetables", Price = 2.19m },
            new { Key = "insights-snack-penny", Name = "Dark Chocolate 100g", Store = "PENNY", Category = "Snacks", Price = 1.59m },
            new { Key = "insights-meat-edeka", Name = "Hähnchenbrust 500g", Store = "EDEKA", Category = "Meat", Price = 5.49m },
            new { Key = "insights-skin-dm", Name = "Body Lotion 500ml", Store = "dm", Category = "Skin Care", Price = 4.79m },
            new { Key = "insights-hair-rossmann", Name = "Shampoo Volume", Store = "ROSSMANN", Category = "Hair Care", Price = 3.99m },
            new { Key = "insights-bath-dm", Name = "Duschgel Fresh", Store = "dm", Category = "Body & Bath", Price = 2.49m },
            new { Key = "insights-health-rossmann", Name = "Vitamin C Brausetabletten", Store = "ROSSMANN", Category = "Health & Wellness", Price = 3.49m }
        };

        var existingProducts = await dbContext.Products
            .Where(x => x.ProductKey != null && products.Select(y => y.Key).Contains(x.ProductKey))
            .ToDictionaryAsync(x => x.ProductKey!, StringComparer.OrdinalIgnoreCase);

        foreach (var item in products)
        {
            if (existingProducts.ContainsKey(item.Key))
            {
                continue;
            }

            var product = new ApiProduct
            {
                ProductKey = item.Key,
                Name = item.Name,
                Description = "Insights seed product",
                Price = item.Price,
                CategoryId = categories[item.Category].Id,
                SupermarketId = supermarkets[item.Store].Id,
                Unit = "pcs",
                Stock = 100,
                IsAvailable = true,
                DataSource = "seed",
                UpdatedAtUtc = DateTime.UtcNow
            };

            dbContext.Products.Add(product);
            existingProducts[item.Key] = product;
        }

        await dbContext.SaveChangesAsync();

        var seedProductIds = existingProducts.Values
            .Select(x => x.Id)
            .ToHashSet();

        var seededOrders = await dbContext.Orders
            .Include(x => x.Items)
            .Where(x => x.UserId == targetUser.Id)
            .Where(x => x.PaymentMethod == "SEEDED_INSIGHTS" || x.Items.Any(item => seedProductIds.Contains(item.ProductId)))
            .OrderBy(x => x.Id)
            .ToListAsync();

        if (seededOrders.Count > 0)
        {
            var legacySeededOrderIds = seededOrders
                .Where(x => x.PaymentMethod == "SEEDED_INSIGHTS")
                .Select(x => x.Id)
                .ToArray();

            if (legacySeededOrderIds.Length > 0)
            {
                var nowForMigration = DateTime.UtcNow;

                await dbContext.Orders
                    .Where(x => legacySeededOrderIds.Contains(x.Id))
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.PaymentMethod, "STRIPE_CARD"));

                await dbContext.Orders
                    .Where(x => legacySeededOrderIds.Contains(x.Id) && x.CreatedAtUtc > nowForMigration)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.CreatedAtUtc, nowForMigration.AddDays(-1))
                        .SetProperty(x => x.UpdatedAtUtc, nowForMigration.AddDays(-1))
                        .SetProperty(x => x.CompletedAtUtc, nowForMigration.AddDays(-1)));

                await dbContext.PaymentTransactions
                    .Where(x => legacySeededOrderIds.Contains(x.OrderId))
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.PaymentType, "STRIPE_CARD"));
            }

            var duplicateSeededOrderIds = seededOrders
                .GroupBy(BuildSeededInsightsDuplicateBucketKey)
                .SelectMany(group => group
                    .OrderBy(x => x.CreatedAtUtc)
                    .ThenBy(x => x.Id)
                    .Skip(2))
                .Select(x => x.Id)
                .ToArray();

            if (duplicateSeededOrderIds.Length > 0)
            {
                await dbContext.PaymentTransactions
                    .Where(x => duplicateSeededOrderIds.Contains(x.OrderId))
                    .ExecuteDeleteAsync();

                await dbContext.OrderItems
                    .Where(x => duplicateSeededOrderIds.Contains(x.OrderId))
                    .ExecuteDeleteAsync();

                await dbContext.Orders
                    .Where(x => duplicateSeededOrderIds.Contains(x.Id))
                    .ExecuteDeleteAsync();
            }

            await dbContext.SaveChangesAsync();
            return;
        }

        var monthlyByStore = new Dictionary<string, decimal[]>
        {
            ["LIDL"] = [86, 92, 97, 101, 104, 112, 116, 109, 113, 121, 126, 132],
            ["ALDI"] = [54, 58, 56, 61, 59, 64, 63, 66, 65, 67, 69, 72],
            ["REWE"] = [42, 45, 48, 47, 50, 53, 55, 54, 52, 56, 57, 60],
            ["PENNY"] = [18, 19, 20, 19, 21, 23, 22, 24, 25, 24, 26, 28],
            ["EDEKA"] = [15, 14, 16, 15, 16, 18, 17, 18, 19, 20, 19, 20],
            ["dm"] = [32, 34, 36, 35, 38, 39, 41, 42, 43, 45, 44, 47],
            ["ROSSMANN"] = [19, 20, 21, 22, 21, 23, 24, 23, 24, 25, 26, 27]
        };

        var storeToProductKeys = new Dictionary<string, string[]>
        {
            ["LIDL"] = ["insights-bev-lidl", "insights-bak-lidl"],
            ["ALDI"] = ["insights-dairy-aldi"],
            ["REWE"] = ["insights-fruit-rewe"],
            ["PENNY"] = ["insights-snack-penny"],
            ["EDEKA"] = ["insights-meat-edeka"],
            ["dm"] = ["insights-skin-dm", "insights-bath-dm"],
            ["ROSSMANN"] = ["insights-hair-rossmann", "insights-health-rossmann"]
        };

        var now = DateTime.UtcNow;
        var firstMonth = new DateTime(now.Year, now.Month, 1).AddMonths(-11);

        for (var i = 0; i < 12; i++)
        {
            var monthStart = firstMonth.AddMonths(i);
            foreach (var kv in monthlyByStore)
            {
                var store = kv.Key;
                var targetSpend = kv.Value[i];
                var productKeys = storeToProductKeys[store];
                var perOrderSpend = Math.Round(targetSpend / 2m, 2);

                for (var orderNo = 0; orderNo < 2; orderNo++)
                {
                    var createdAt = monthStart
                        .AddDays(orderNo == 0 ? 8 : 20)
                        .AddHours(10 + ((i + orderNo) % 5));

                    // Keep demo history in the past so seeded rows never look like newly placed live orders.
                    if (createdAt > now)
                    {
                        createdAt = now.AddDays(-(orderNo + 1)).AddHours(-(i % 6));
                    }

                    var order = new ApiOrder
                    {
                        UserId = targetUser.Id,
                        CustomerNameSnapshot = $"{targetUser.FirstName} {targetUser.LastName}".Trim(),
                        CustomerEmailSnapshot = targetUser.Email,
                        Status = "COMPLETED",
                        PaymentMethod = "STRIPE_CARD",
                        PaymentStatus = "PAID",
                        AddressId = userAddress.Id,
                        Subtotal = perOrderSpend,
                        DeliveryFee = 0m,
                        Total = perOrderSpend,
                        CreatedAtUtc = createdAt,
                        UpdatedAtUtc = createdAt,
                        CompletedAtUtc = createdAt.AddHours(2)
                    };

                    var lineSplit = productKeys.Length == 1 ? new[] { 1m } : new[] { 0.62m, 0.38m };
                    var lineRemainder = perOrderSpend;

                    for (var p = 0; p < productKeys.Length; p++)
                    {
                        var product = existingProducts[productKeys[p]];
                        var lineTarget = p == productKeys.Length - 1
                            ? lineRemainder
                            : Math.Round(perOrderSpend * lineSplit[p], 2);

                        var qty = Math.Max(1, (int)Math.Round(lineTarget / Math.Max(product.Price, 0.5m), MidpointRounding.AwayFromZero));
                        var totalPrice = Math.Round(qty * product.Price, 2);
                        lineRemainder = Math.Max(0m, lineRemainder - totalPrice);

                        order.Items.Add(new ApiOrderItem
                        {
                            ProductId = product.Id,
                            ProductName = product.Name,
                            ProductImageUrl = product.ImageUrl,
                            SupermarketName = supermarkets[store].Name,
                            Quantity = qty,
                            UnitPrice = product.Price,
                            TotalPrice = totalPrice
                        });
                    }

                    if (order.Items.Count > 0)
                    {
                        var orderSubtotal = order.Items.Sum(x => x.TotalPrice);
                        order.Subtotal = orderSubtotal;
                        order.Total = orderSubtotal;
                    }

                    dbContext.Orders.Add(order);
                }
            }
        }

        await dbContext.SaveChangesAsync();
    }

    private static string BuildSeededInsightsDuplicateBucketKey(ApiOrder order)
    {
        var itemSignature = string.Join(
            '|',
            order.Items
                .OrderBy(x => x.ProductId)
                .ThenBy(x => x.Quantity)
                .Select(x => $"{x.ProductId}:{x.Quantity}:{x.UnitPrice:F2}:{x.TotalPrice:F2}"));

        return $"{order.UserId}:{order.AddressId}:{order.CreatedAtUtc:yyyy-MM}:{order.Total:F2}:{itemSignature}";
    }

    private static async Task SeedCategoriesAsync(ApiDbContext dbContext)
    {
        var categories = new[]
        {
            new ApiCategory { Name = "Eggs & Dairy", Slug = "eggs-dairy", Icon = "🥚" },
            new ApiCategory { Name = "Meat", Slug = "meat", Icon = "🥩" },
            new ApiCategory { Name = "Fruits & Vegetables", Slug = "fruits-vegetables", Icon = "🥦" },
            new ApiCategory { Name = "Bakery", Slug = "bakery", Icon = "🍞" },
            new ApiCategory { Name = "Beverages", Slug = "beverages", Icon = "🥤" },
            new ApiCategory { Name = "Snacks", Slug = "snacks", Icon = "🍿" },
            new ApiCategory { Name = "Hair Care", Slug = "hair-care", Icon = "✨" },
            new ApiCategory { Name = "Skin Care", Slug = "skin-care", Icon = "☀️" },
            new ApiCategory { Name = "Body & Bath", Slug = "body-bath", Icon = "💧" },
            new ApiCategory { Name = "Makeup & Fragrance", Slug = "makeup-fragrance", Icon = "🎨" },
            new ApiCategory { Name = "Health & Wellness", Slug = "health-wellness", Icon = "❤️" },
            new ApiCategory { Name = "Baby & Kids", Slug = "baby-kids", Icon = "👶" }
        };

        var existingSlugs = await dbContext.Categories
            .Select(x => x.Slug)
            .ToListAsync();

        var missing = categories
            .Where(category => existingSlugs.All(existing => !string.Equals(existing, category.Slug, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        dbContext.Categories.AddRange(missing);
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedSupermarketsAsync(ApiDbContext dbContext)
    {
        var supermarkets = new[]
        {
            new ApiSupermarket { Name = "REWE", Slug = "rewe", Color = "#CC0000" },
            new ApiSupermarket { Name = "ALDI", Slug = "aldi", Color = "#00519C" },
            new ApiSupermarket { Name = "EDEKA", Slug = "edeka", Color = "#003A70" },
            new ApiSupermarket { Name = "PENNY", Slug = "penny", Color = "#CC0000" },
            new ApiSupermarket { Name = "LIDL", Slug = "lidl", Color = "#0050AA" },
            new ApiSupermarket { Name = "dm", Slug = "dm", Color = "#003E91" },
            new ApiSupermarket { Name = "ROSSMANN", Slug = "rossmann", Color = "#C3002F" }
        };

        var existingSlugs = await dbContext.Supermarkets
            .Select(x => x.Slug)
            .ToListAsync();

        var missing = supermarkets
            .Where(supermarket => existingSlugs.All(existing => !string.Equals(existing, supermarket.Slug, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        dbContext.Supermarkets.AddRange(missing);
        await dbContext.SaveChangesAsync();
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

    private static async Task SeedPricingPolicyAsync(ApiDbContext dbContext)
    {
        if (await dbContext.PricingPolicyVersions.AnyAsync())
        {
            return;
        }

        var seedUserId = await dbContext.Users
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .FirstOrDefaultAsync();

        var now = DateTime.UtcNow;
        var version = new ApiPricingPolicyVersion
        {
            VersionNo = 1,
            XFactorPercent = 0m,
            YFactorAmount = 0m,
            DeliveryCharge = 2.99m,
            IsActive = true,
            EffectiveFromUtc = now,
            Reason = "Initial baseline policy",
            CreatedByUserId = seedUserId,
            CreatedAtUtc = now
        };

        dbContext.PricingPolicyVersions.Add(version);
        await dbContext.SaveChangesAsync();

        dbContext.PricingPolicyAuditEvents.Add(new ApiPricingPolicyAuditEvent
        {
            PolicyVersionId = version.Id,
            ActionType = "created",
            OldXFactorPercent = null,
            NewXFactorPercent = version.XFactorPercent,
            OldYFactorAmount = null,
            NewYFactorAmount = version.YFactorAmount,
            OldDeliveryCharge = null,
            NewDeliveryCharge = version.DeliveryCharge,
            ChangedByUserId = seedUserId,
            ChangedAtUtc = now,
            CorrelationId = Guid.NewGuid().ToString("N"),
            MetadataJson = "Seeded initial baseline policy"
        });

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

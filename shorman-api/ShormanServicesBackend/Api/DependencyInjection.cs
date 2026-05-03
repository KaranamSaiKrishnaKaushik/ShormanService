using System.Text;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ShormanServicesBackend.Api.Persistence;
using ShormanServicesBackend.Api.Security;

namespace ShormanServicesBackend.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        var connectionString =
            configuration.GetConnectionString("ApiConnection")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "No database connection string found. Configure ConnectionStrings__DefaultConnection or ConnectionStrings__ApiConnection.");

        var provider = configuration["Database:Provider"];

        if (string.IsNullOrWhiteSpace(provider))
        {
            provider = connectionString.Contains("mysql.database.azure.com", StringComparison.OrdinalIgnoreCase)
                       || connectionString.Contains("Port=3306", StringComparison.OrdinalIgnoreCase)
                ? "MySql"
                : "SqlServer";
        }

        services.AddDbContext<ApiDbContext>(options =>
        {
            if (provider.Equals("MySql", StringComparison.OrdinalIgnoreCase))
            {
                options.UseMySql(
                    connectionString,
                    new MySqlServerVersion(new Version(8, 0, 0))
                    //ServerVersion.AutoDetect(connectionString)
                );
            }
            else if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlServer(connectionString);
            }
            else
            {
                throw new InvalidOperationException($"Unsupported database provider: {provider}");
            }
        });

        services.AddMediatR(typeof(DependencyInjection).Assembly);
        services.AddScoped<JwtTokenService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey))
                };
            });

        services.AddAuthorization();

        return services;
    }

    public static async Task InitializeApiAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        await MigrateTableNamesAsync(dbContext);
        await EnsureTablesCreatedAsync(dbContext);
        await ApiSeeder.SeedAsync(dbContext);
    }

    private static async Task EnsureTablesCreatedAsync(ApiDbContext dbContext)
    {
        var scripts = dbContext.Database.IsMySql()
            ? GetMySqlCreateScripts()
            : GetSqlServerCreateScripts();

        foreach (var script in scripts)
            await dbContext.Database.ExecuteSqlRawAsync(script);
    }

    private static IEnumerable<string> GetSqlServerCreateScripts() =>
    [
        """
        IF OBJECT_ID('users', 'U') IS NULL
        CREATE TABLE users (
            Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
            Email NVARCHAR(256) NOT NULL,
            PasswordHash NVARCHAR(200) NOT NULL,
            FirstName NVARCHAR(100) NOT NULL,
            LastName NVARCHAR(100) NOT NULL,
            Phone NVARCHAR(50) NULL,
            CreatedAtUtc DATETIME2 NOT NULL,
            CONSTRAINT UQ_users_Email UNIQUE (Email)
        );
        """,
        """
        IF OBJECT_ID('categories', 'U') IS NULL
        CREATE TABLE categories (
            Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
            Name NVARCHAR(100) NOT NULL,
            Slug NVARCHAR(100) NOT NULL,
            Icon NVARCHAR(20) NULL,
            CONSTRAINT UQ_categories_Slug UNIQUE (Slug)
        );
        """,
        """
        IF OBJECT_ID('supermarkets', 'U') IS NULL
        CREATE TABLE supermarkets (
            Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
            Name NVARCHAR(100) NOT NULL,
            Slug NVARCHAR(100) NOT NULL,
            LogoUrl NVARCHAR(1000) NULL,
            Color NVARCHAR(20) NULL,
            CONSTRAINT UQ_supermarkets_Slug UNIQUE (Slug)
        );
        """,
        """
        IF OBJECT_ID('products', 'U') IS NULL
        CREATE TABLE products (
            Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
            Name NVARCHAR(220) NOT NULL,
            Description NVARCHAR(1000) NULL,
            Price DECIMAL(10,2) NOT NULL,
            ImageUrl NVARCHAR(1000) NULL,
            CategoryId INT NOT NULL,
            SupermarketId INT NOT NULL,
            Unit NVARCHAR(50) NULL,
            Stock INT NULL,
            IsAvailable BIT NOT NULL DEFAULT 1,
            CONSTRAINT FK_products_Category FOREIGN KEY (CategoryId) REFERENCES categories(Id),
            CONSTRAINT FK_products_Supermarket FOREIGN KEY (SupermarketId) REFERENCES supermarkets(Id)
        );
        """,
        """
        IF OBJECT_ID('carts', 'U') IS NULL
        CREATE TABLE carts (
            Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
            UserId INT NOT NULL,
            CreatedAtUtc DATETIME2 NOT NULL,
            UpdatedAtUtc DATETIME2 NULL,
            CONSTRAINT FK_carts_User FOREIGN KEY (UserId) REFERENCES users(Id),
            CONSTRAINT UQ_carts_User UNIQUE (UserId)
        );
        """,
        """
        IF OBJECT_ID('cart_items', 'U') IS NULL
        CREATE TABLE cart_items (
            Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
            CartId INT NOT NULL,
            ProductId INT NOT NULL,
            Quantity INT NOT NULL,
            UnitPrice DECIMAL(10,2) NOT NULL,
            CONSTRAINT FK_cart_items_Cart FOREIGN KEY (CartId) REFERENCES carts(Id) ON DELETE CASCADE,
            CONSTRAINT FK_cart_items_Product FOREIGN KEY (ProductId) REFERENCES products(Id),
            CONSTRAINT UQ_cart_items_Cart_Product UNIQUE (CartId, ProductId)
        );
        """,
        """
        IF OBJECT_ID('addresses', 'U') IS NULL
        CREATE TABLE addresses (
            Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
            UserId INT NOT NULL,
            Label NVARCHAR(100) NULL,
            Street NVARCHAR(150) NOT NULL,
            HouseNumber NVARCHAR(30) NOT NULL,
            PostalCode NVARCHAR(20) NOT NULL,
            City NVARCHAR(100) NOT NULL,
            Country NVARCHAR(100) NOT NULL,
            IsDefault BIT NOT NULL DEFAULT 0,
            CONSTRAINT FK_addresses_User FOREIGN KEY (UserId) REFERENCES users(Id)
        );
        """,
        """
        IF OBJECT_ID('orders', 'U') IS NULL
        CREATE TABLE orders (
            Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
            UserId INT NOT NULL,
            Status NVARCHAR(30) NOT NULL,
            PaymentMethod NVARCHAR(30) NOT NULL,
            AddressId INT NOT NULL,
            Subtotal DECIMAL(10,2) NOT NULL,
            DeliveryFee DECIMAL(10,2) NOT NULL,
            Total DECIMAL(10,2) NOT NULL,
            CreatedAtUtc DATETIME2 NOT NULL,
            UpdatedAtUtc DATETIME2 NULL,
            CONSTRAINT FK_orders_User FOREIGN KEY (UserId) REFERENCES users(Id),
            CONSTRAINT FK_orders_Address FOREIGN KEY (AddressId) REFERENCES addresses(Id)
        );
        """,
        """
        IF OBJECT_ID('order_items', 'U') IS NULL
        CREATE TABLE order_items (
            Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
            OrderId INT NOT NULL,
            ProductId INT NOT NULL,
            ProductName NVARCHAR(220) NOT NULL,
            ProductImageUrl NVARCHAR(1000) NULL,
            Quantity INT NOT NULL,
            UnitPrice DECIMAL(10,2) NOT NULL,
            TotalPrice DECIMAL(10,2) NOT NULL,
            CONSTRAINT FK_order_items_Order FOREIGN KEY (OrderId) REFERENCES orders(Id)
        );
        """,
        """
        IF OBJECT_ID('roles', 'U') IS NULL
        CREATE TABLE roles (
            Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
            Name NVARCHAR(50) NOT NULL,
            CONSTRAINT UQ_roles_Name UNIQUE (Name)
        );
        """,
        """
        IF OBJECT_ID('user_roles', 'U') IS NULL
        CREATE TABLE user_roles (
            UserId INT NOT NULL,
            RoleId INT NOT NULL,
            CONSTRAINT PK_user_roles PRIMARY KEY (UserId, RoleId),
            CONSTRAINT FK_user_roles_User FOREIGN KEY (UserId) REFERENCES users(Id) ON DELETE CASCADE,
            CONSTRAINT FK_user_roles_Role FOREIGN KEY (RoleId) REFERENCES roles(Id) ON DELETE CASCADE
        );
        """,
        """
        IF OBJECT_ID('refresh_tokens', 'U') IS NULL
        CREATE TABLE refresh_tokens (
            Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
            UserId INT NOT NULL,
            Token NVARCHAR(512) NOT NULL,
            ExpiresAtUtc DATETIME2 NOT NULL,
            CreatedAtUtc DATETIME2 NOT NULL,
            RevokedAtUtc DATETIME2 NULL,
            CONSTRAINT UQ_refresh_tokens_Token UNIQUE (Token),
            CONSTRAINT FK_refresh_tokens_User FOREIGN KEY (UserId) REFERENCES users(Id) ON DELETE CASCADE
        );
        """,
        """
        IF OBJECT_ID('stores', 'U') IS NULL
        CREATE TABLE stores (
            Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
            SupermarketId INT NOT NULL,
            Name NVARCHAR(150) NOT NULL,
            AddressStreet NVARCHAR(150) NOT NULL,
            AddressHouseNumber NVARCHAR(30) NOT NULL,
            AddressPostalCode NVARCHAR(20) NOT NULL,
            AddressCity NVARCHAR(100) NOT NULL,
            AddressCountry NVARCHAR(100) NOT NULL,
            Latitude DECIMAL(9,6) NULL,
            Longitude DECIMAL(9,6) NULL,
            Phone NVARCHAR(50) NULL,
            IsActive BIT NOT NULL DEFAULT 1,
            CONSTRAINT FK_stores_Supermarket FOREIGN KEY (SupermarketId) REFERENCES supermarkets(Id)
        );
        """,
        """
        IF OBJECT_ID('store_hours', 'U') IS NULL
        CREATE TABLE store_hours (
            Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
            StoreId INT NOT NULL,
            DayOfWeek TINYINT NOT NULL,
            OpenTime TIME NULL,
            CloseTime TIME NULL,
            IsClosed BIT NOT NULL DEFAULT 0,
            CONSTRAINT FK_store_hours_Store FOREIGN KEY (StoreId) REFERENCES stores(Id) ON DELETE CASCADE
        );
        """,
        """
        IF OBJECT_ID('store_delivery_zones', 'U') IS NULL
        CREATE TABLE store_delivery_zones (
            Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
            StoreId INT NOT NULL,
            PostalCode NVARCHAR(20) NOT NULL,
            DeliveryFee DECIMAL(10,2) NOT NULL DEFAULT 0,
            MinOrderAmount DECIMAL(10,2) NOT NULL DEFAULT 0,
            EstimatedMinutes INT NULL,
            CONSTRAINT FK_store_delivery_zones_Store FOREIGN KEY (StoreId) REFERENCES stores(Id) ON DELETE CASCADE,
            CONSTRAINT UQ_store_delivery_zones UNIQUE (StoreId, PostalCode)
        );
        """,
        """
        IF OBJECT_ID('store_products', 'U') IS NULL
        CREATE TABLE store_products (
            StoreId INT NOT NULL,
            ProductId INT NOT NULL,
            Price DECIMAL(10,2) NULL,
            Stock INT NULL,
            IsAvailable BIT NOT NULL DEFAULT 1,
            CONSTRAINT PK_store_products PRIMARY KEY (StoreId, ProductId),
            CONSTRAINT FK_store_products_Store FOREIGN KEY (StoreId) REFERENCES stores(Id) ON DELETE CASCADE,
            CONSTRAINT FK_store_products_Product FOREIGN KEY (ProductId) REFERENCES products(Id) ON DELETE CASCADE
        );
        """,
        """
        IF OBJECT_ID('product_images', 'U') IS NULL
        CREATE TABLE product_images (
            Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
            ProductId INT NOT NULL,
            Url NVARCHAR(1000) NOT NULL,
            AltText NVARCHAR(200) NULL,
            SortOrder INT NOT NULL DEFAULT 0,
            CONSTRAINT FK_product_images_Product FOREIGN KEY (ProductId) REFERENCES products(Id) ON DELETE CASCADE
        );
        """,
        """
        IF OBJECT_ID('payment_methods', 'U') IS NULL
        CREATE TABLE payment_methods (
            Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
            UserId INT NOT NULL,
            Type NVARCHAR(30) NOT NULL,
            Provider NVARCHAR(50) NULL,
            Last4 NVARCHAR(4) NULL,
            ExpiryMonth TINYINT NULL,
            ExpiryYear SMALLINT NULL,
            IsDefault BIT NOT NULL DEFAULT 0,
            CreatedAtUtc DATETIME2 NOT NULL,
            CONSTRAINT FK_payment_methods_User FOREIGN KEY (UserId) REFERENCES users(Id) ON DELETE CASCADE
        );
        """,
        """
        IF OBJECT_ID('payment_transactions', 'U') IS NULL
        CREATE TABLE payment_transactions (
            Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
            OrderId INT NOT NULL,
            PaymentMethodId INT NULL,
            Amount DECIMAL(10,2) NOT NULL,
            Currency NVARCHAR(10) NOT NULL DEFAULT 'EUR',
            Status NVARCHAR(30) NOT NULL,
            ProviderRef NVARCHAR(200) NULL,
            CreatedAtUtc DATETIME2 NOT NULL,
            UpdatedAtUtc DATETIME2 NULL,
            CONSTRAINT FK_payment_transactions_Order FOREIGN KEY (OrderId) REFERENCES orders(Id),
            CONSTRAINT FK_payment_transactions_PaymentMethod FOREIGN KEY (PaymentMethodId) REFERENCES payment_methods(Id)
        );
        """,
        """
        IF OBJECT_ID('order_status_history', 'U') IS NULL
        CREATE TABLE order_status_history (
            Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
            OrderId INT NOT NULL,
            Status NVARCHAR(30) NOT NULL,
            Note NVARCHAR(500) NULL,
            ChangedAtUtc DATETIME2 NOT NULL,
            CONSTRAINT FK_order_status_history_Order FOREIGN KEY (OrderId) REFERENCES orders(Id) ON DELETE CASCADE
        );
        """
    ];

    private static IEnumerable<string> GetMySqlCreateScripts() =>
    [
        """
        CREATE TABLE IF NOT EXISTS `users` (
            `Id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
            `Email` VARCHAR(256) NOT NULL,
            `PasswordHash` VARCHAR(200) NOT NULL,
            `FirstName` VARCHAR(100) NOT NULL,
            `LastName` VARCHAR(100) NOT NULL,
            `Phone` VARCHAR(50) NULL,
            `CreatedAtUtc` DATETIME NOT NULL,
            UNIQUE KEY `UQ_users_Email` (`Email`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """,
        """
        CREATE TABLE IF NOT EXISTS `categories` (
            `Id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
            `Name` VARCHAR(100) NOT NULL,
            `Slug` VARCHAR(100) NOT NULL,
            `Icon` VARCHAR(20) NULL,
            UNIQUE KEY `UQ_categories_Slug` (`Slug`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """,
        """
        CREATE TABLE IF NOT EXISTS `supermarkets` (
            `Id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
            `Name` VARCHAR(100) NOT NULL,
            `Slug` VARCHAR(100) NOT NULL,
            `LogoUrl` VARCHAR(1000) NULL,
            `Color` VARCHAR(20) NULL,
            UNIQUE KEY `UQ_supermarkets_Slug` (`Slug`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """,
        """
        CREATE TABLE IF NOT EXISTS `products` (
            `Id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
            `Name` VARCHAR(220) NOT NULL,
            `Description` VARCHAR(1000) NULL,
            `Price` DECIMAL(10,2) NOT NULL,
            `ImageUrl` VARCHAR(1000) NULL,
            `CategoryId` INT NOT NULL,
            `SupermarketId` INT NOT NULL,
            `Unit` VARCHAR(50) NULL,
            `Stock` INT NULL,
            `IsAvailable` TINYINT(1) NOT NULL DEFAULT 1,
            CONSTRAINT `FK_products_Category` FOREIGN KEY (`CategoryId`) REFERENCES `categories` (`Id`),
            CONSTRAINT `FK_products_Supermarket` FOREIGN KEY (`SupermarketId`) REFERENCES `supermarkets` (`Id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """,
        """
        CREATE TABLE IF NOT EXISTS `carts` (
            `Id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
            `UserId` INT NOT NULL,
            `CreatedAtUtc` DATETIME NOT NULL,
            `UpdatedAtUtc` DATETIME NULL,
            CONSTRAINT `FK_carts_User` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`),
            UNIQUE KEY `UQ_carts_User` (`UserId`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """,
        """
        CREATE TABLE IF NOT EXISTS `cart_items` (
            `Id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
            `CartId` INT NOT NULL,
            `ProductId` INT NOT NULL,
            `Quantity` INT NOT NULL,
            `UnitPrice` DECIMAL(10,2) NOT NULL,
            CONSTRAINT `FK_cart_items_Cart` FOREIGN KEY (`CartId`) REFERENCES `carts` (`Id`) ON DELETE CASCADE,
            CONSTRAINT `FK_cart_items_Product` FOREIGN KEY (`ProductId`) REFERENCES `products` (`Id`),
            UNIQUE KEY `UQ_cart_items_Cart_Product` (`CartId`, `ProductId`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """,
        """
        CREATE TABLE IF NOT EXISTS `addresses` (
            `Id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
            `UserId` INT NOT NULL,
            `Label` VARCHAR(100) NULL,
            `Street` VARCHAR(150) NOT NULL,
            `HouseNumber` VARCHAR(30) NOT NULL,
            `PostalCode` VARCHAR(20) NOT NULL,
            `City` VARCHAR(100) NOT NULL,
            `Country` VARCHAR(100) NOT NULL,
            `IsDefault` TINYINT(1) NOT NULL DEFAULT 0,
            CONSTRAINT `FK_addresses_User` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """,
        """
        CREATE TABLE IF NOT EXISTS `orders` (
            `Id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
            `UserId` INT NOT NULL,
            `Status` VARCHAR(30) NOT NULL,
            `PaymentMethod` VARCHAR(30) NOT NULL,
            `AddressId` INT NOT NULL,
            `Subtotal` DECIMAL(10,2) NOT NULL,
            `DeliveryFee` DECIMAL(10,2) NOT NULL,
            `Total` DECIMAL(10,2) NOT NULL,
            `CreatedAtUtc` DATETIME NOT NULL,
            `UpdatedAtUtc` DATETIME NULL,
            CONSTRAINT `FK_orders_User` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`),
            CONSTRAINT `FK_orders_Address` FOREIGN KEY (`AddressId`) REFERENCES `addresses` (`Id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """,
        """
        CREATE TABLE IF NOT EXISTS `order_items` (
            `Id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
            `OrderId` INT NOT NULL,
            `ProductId` INT NOT NULL,
            `ProductName` VARCHAR(220) NOT NULL,
            `ProductImageUrl` VARCHAR(1000) NULL,
            `Quantity` INT NOT NULL,
            `UnitPrice` DECIMAL(10,2) NOT NULL,
            `TotalPrice` DECIMAL(10,2) NOT NULL,
            CONSTRAINT `FK_order_items_Order` FOREIGN KEY (`OrderId`) REFERENCES `orders` (`Id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """,
        """
        CREATE TABLE IF NOT EXISTS `roles` (
            `Id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
            `Name` VARCHAR(50) NOT NULL,
            UNIQUE KEY `UQ_roles_Name` (`Name`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """,
        """
        CREATE TABLE IF NOT EXISTS `user_roles` (
            `UserId` INT NOT NULL,
            `RoleId` INT NOT NULL,
            PRIMARY KEY (`UserId`, `RoleId`),
            CONSTRAINT `FK_user_roles_User` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`) ON DELETE CASCADE,
            CONSTRAINT `FK_user_roles_Role` FOREIGN KEY (`RoleId`) REFERENCES `roles` (`Id`) ON DELETE CASCADE
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """,
        """
        CREATE TABLE IF NOT EXISTS `refresh_tokens` (
            `Id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
            `UserId` INT NOT NULL,
            `Token` VARCHAR(512) NOT NULL,
            `ExpiresAtUtc` DATETIME NOT NULL,
            `CreatedAtUtc` DATETIME NOT NULL,
            `RevokedAtUtc` DATETIME NULL,
            UNIQUE KEY `UQ_refresh_tokens_Token` (`Token`),
            CONSTRAINT `FK_refresh_tokens_User` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`) ON DELETE CASCADE
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """,
        """
        CREATE TABLE IF NOT EXISTS `stores` (
            `Id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
            `SupermarketId` INT NOT NULL,
            `Name` VARCHAR(150) NOT NULL,
            `AddressStreet` VARCHAR(150) NOT NULL,
            `AddressHouseNumber` VARCHAR(30) NOT NULL,
            `AddressPostalCode` VARCHAR(20) NOT NULL,
            `AddressCity` VARCHAR(100) NOT NULL,
            `AddressCountry` VARCHAR(100) NOT NULL,
            `Latitude` DECIMAL(9,6) NULL,
            `Longitude` DECIMAL(9,6) NULL,
            `Phone` VARCHAR(50) NULL,
            `IsActive` TINYINT(1) NOT NULL DEFAULT 1,
            CONSTRAINT `FK_stores_Supermarket` FOREIGN KEY (`SupermarketId`) REFERENCES `supermarkets` (`Id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """,
        """
        CREATE TABLE IF NOT EXISTS `store_hours` (
            `Id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
            `StoreId` INT NOT NULL,
            `DayOfWeek` TINYINT NOT NULL,
            `OpenTime` TIME NULL,
            `CloseTime` TIME NULL,
            `IsClosed` TINYINT(1) NOT NULL DEFAULT 0,
            CONSTRAINT `FK_store_hours_Store` FOREIGN KEY (`StoreId`) REFERENCES `stores` (`Id`) ON DELETE CASCADE
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """,
        """
        CREATE TABLE IF NOT EXISTS `store_delivery_zones` (
            `Id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
            `StoreId` INT NOT NULL,
            `PostalCode` VARCHAR(20) NOT NULL,
            `DeliveryFee` DECIMAL(10,2) NOT NULL DEFAULT 0,
            `MinOrderAmount` DECIMAL(10,2) NOT NULL DEFAULT 0,
            `EstimatedMinutes` INT NULL,
            CONSTRAINT `FK_store_delivery_zones_Store` FOREIGN KEY (`StoreId`) REFERENCES `stores` (`Id`) ON DELETE CASCADE,
            UNIQUE KEY `UQ_store_delivery_zones` (`StoreId`, `PostalCode`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """,
        """
        CREATE TABLE IF NOT EXISTS `store_products` (
            `StoreId` INT NOT NULL,
            `ProductId` INT NOT NULL,
            `Price` DECIMAL(10,2) NULL,
            `Stock` INT NULL,
            `IsAvailable` TINYINT(1) NOT NULL DEFAULT 1,
            PRIMARY KEY (`StoreId`, `ProductId`),
            CONSTRAINT `FK_store_products_Store` FOREIGN KEY (`StoreId`) REFERENCES `stores` (`Id`) ON DELETE CASCADE,
            CONSTRAINT `FK_store_products_Product` FOREIGN KEY (`ProductId`) REFERENCES `products` (`Id`) ON DELETE CASCADE
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """,
        """
        CREATE TABLE IF NOT EXISTS `product_images` (
            `Id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
            `ProductId` INT NOT NULL,
            `Url` VARCHAR(1000) NOT NULL,
            `AltText` VARCHAR(200) NULL,
            `SortOrder` INT NOT NULL DEFAULT 0,
            CONSTRAINT `FK_product_images_Product` FOREIGN KEY (`ProductId`) REFERENCES `products` (`Id`) ON DELETE CASCADE
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """,
        """
        CREATE TABLE IF NOT EXISTS `payment_methods` (
            `Id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
            `UserId` INT NOT NULL,
            `Type` VARCHAR(30) NOT NULL,
            `Provider` VARCHAR(50) NULL,
            `Last4` VARCHAR(4) NULL,
            `ExpiryMonth` TINYINT NULL,
            `ExpiryYear` SMALLINT NULL,
            `IsDefault` TINYINT(1) NOT NULL DEFAULT 0,
            `CreatedAtUtc` DATETIME NOT NULL,
            CONSTRAINT `FK_payment_methods_User` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`) ON DELETE CASCADE
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """,
        """
        CREATE TABLE IF NOT EXISTS `payment_transactions` (
            `Id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
            `OrderId` INT NOT NULL,
            `PaymentMethodId` INT NULL,
            `Amount` DECIMAL(10,2) NOT NULL,
            `Currency` VARCHAR(10) NOT NULL DEFAULT 'EUR',
            `Status` VARCHAR(30) NOT NULL,
            `ProviderRef` VARCHAR(200) NULL,
            `CreatedAtUtc` DATETIME NOT NULL,
            `UpdatedAtUtc` DATETIME NULL,
            CONSTRAINT `FK_payment_transactions_Order` FOREIGN KEY (`OrderId`) REFERENCES `orders` (`Id`),
            CONSTRAINT `FK_payment_transactions_PaymentMethod` FOREIGN KEY (`PaymentMethodId`) REFERENCES `payment_methods` (`Id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """,
        """
        CREATE TABLE IF NOT EXISTS `order_status_history` (
            `Id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
            `OrderId` INT NOT NULL,
            `Status` VARCHAR(30) NOT NULL,
            `Note` VARCHAR(500) NULL,
            `ChangedAtUtc` DATETIME NOT NULL,
            CONSTRAINT `FK_order_status_history_Order` FOREIGN KEY (`OrderId`) REFERENCES `orders` (`Id`) ON DELETE CASCADE
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """
    ];

    private static readonly (string OldName, string NewName)[] TableRenamePairs =
    [
        ("api_users", "users"),
        ("api_addresses", "addresses"),
        ("api_categories", "categories"),
        ("api_supermarkets", "supermarkets"),
        ("api_products", "products"),
        ("api_carts", "carts"),
        ("api_cart_items", "cart_items"),
        ("api_orders", "orders"),
        ("api_order_items", "order_items")
    ];

    private static async Task MigrateTableNamesAsync(ApiDbContext dbContext)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;

        if (shouldClose) await connection.OpenAsync();

        try
        {
            foreach (var (oldName, newName) in TableRenamePairs)
            {
                if (!await TableExistsAsync(connection, oldName) || await TableExistsAsync(connection, newName))
                    continue;

                var sql = dbContext.Database.IsMySql()
                    ? $"RENAME TABLE `{oldName}` TO `{newName}`;"
                    : $"EXEC sp_rename 'dbo.{oldName}', '{newName}';";

                await dbContext.Database.ExecuteSqlRawAsync(sql);
            }

            if (await TableExistsAsync(connection, "api_orders_old")
                && !await TableExistsAsync(connection, "orders")
                && !await TableExistsAsync(connection, "api_orders"))
            {
                var sql = dbContext.Database.IsMySql()
                    ? "RENAME TABLE `api_orders_old` TO `orders`;"
                    : "EXEC sp_rename 'dbo.api_orders_old', 'orders';";
                await dbContext.Database.ExecuteSqlRawAsync(sql);
            }
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }

    private static async Task<bool> TableExistsAsync(System.Data.Common.DbConnection connection, string tableName)
    {
        await using var command = connection.CreateCommand();
        var providerName = connection.GetType().FullName ?? string.Empty;
        var isMySql = providerName.Contains("MySql", StringComparison.OrdinalIgnoreCase);
        var isSqlite = providerName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase);

        if (isSqlite)
            command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = @name LIMIT 1;";
        else if (isMySql)
            command.CommandText = "SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @name LIMIT 1;";
        else
            command.CommandText = "SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @name AND TABLE_TYPE = 'BASE TABLE';";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@name";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);

        var result = await command.ExecuteScalarAsync();
        return result is not null;
    }
}

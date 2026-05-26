using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ShormanServicesBackend.Api.Features;
using ShormanServicesBackend.Api.Payments;
using ShormanServicesBackend.Api.Persistence;
using ShormanServicesBackend.Api.Security;

namespace ShormanServicesBackend.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<Auth0Options>(configuration.GetSection(Auth0Options.SectionName));
        services.Configure<DeliveryZoneOptions>(configuration.GetSection(DeliveryZoneOptions.SectionName));
        services.Configure<ProductManagementOptions>(configuration.GetSection(ProductManagementOptions.SectionName));
        services.Configure<StripeOptions>(configuration.GetSection(StripeOptions.SectionName));
        services.Configure<DeliveryZoneOptions>(configuration.GetSection(DeliveryZoneOptions.SectionName));
        services.Configure<ProductManagementOptions>(configuration.GetSection(ProductManagementOptions.SectionName));
        services.Configure<StripeOptions>(configuration.GetSection(StripeOptions.SectionName));
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        var auth0Options = configuration.GetSection(Auth0Options.SectionName).Get<Auth0Options>() ?? new Auth0Options();
        var apiConnection = configuration.GetConnectionString("ApiConnection");
        var defaultConnection = configuration.GetConnectionString("DefaultConnection");

        var connectionString = !string.IsNullOrWhiteSpace(apiConnection)
            ? apiConnection
            : !string.IsNullOrWhiteSpace(defaultConnection)
                ? defaultConnection
                : throw new InvalidOperationException(
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
        services.AddMemoryCache();
        services.AddMemoryCache();
        services.AddScoped<JwtTokenService>();
        services.AddScoped<IDeliveryGeocodingService, DeliveryGeocodingService>();
        services.AddScoped<IProductManagementImportService, ProductManagementImportService>();
        services.AddScoped<IPricingPolicyProvider, PricingPolicyProvider>();
        services.AddScoped<StripePaymentService>();
        services.AddHttpClient("delivery-geocoder", client =>
        {
            client.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
            client.Timeout = TimeSpan.FromSeconds(8);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ShormanService/1.0");
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.8,de;q=0.7");
        });
        services.AddScoped<IDeliveryGeocodingService, DeliveryGeocodingService>();
        services.AddScoped<IProductManagementImportService, ProductManagementImportService>();
        services.AddScoped<IPricingPolicyProvider, PricingPolicyProvider>();
        services.AddScoped<StripePaymentService>();
        services.AddHttpClient("delivery-geocoder", client =>
        {
            client.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
            client.Timeout = TimeSpan.FromSeconds(8);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ShormanService/1.0");
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.8,de;q=0.7");
        });

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = AuthSchemes.AppJwt;
                options.DefaultChallengeScheme = AuthSchemes.AppJwt;
            })
            .AddJwtBearer(AuthSchemes.AppJwt, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey))
                };
            })
            .AddJwtBearer(AuthSchemes.Auth0, options =>
            {
                options.Authority = $"https://{auth0Options.Domain.Trim().TrimEnd('/')}";
                options.Audience = auth0Options.Audience;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidAudience = auth0Options.Audience,
                    NameClaimType = "name"
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

        var previousTimeout = dbContext.Database.GetCommandTimeout();
        dbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(10));

        try
        {
            foreach (var script in scripts)
            {
                await dbContext.Database.ExecuteSqlRawAsync(script);
            }
        }
        finally
        {
            dbContext.Database.SetCommandTimeout(previousTimeout);
        }
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
            DisplayName NVARCHAR(80) NULL,
            ThemePreference NVARCHAR(40) NULL,
            Phone NVARCHAR(50) NULL,
            IsEmailVerified BIT NOT NULL DEFAULT 1,
            EmailVerificationCode NVARCHAR(20) NULL,
            EmailVerificationExpiresAtUtc DATETIME2 NULL,
            PasswordResetCode NVARCHAR(20) NULL,
            PasswordResetExpiresAtUtc DATETIME2 NULL,
            IsDeleted BIT NOT NULL DEFAULT 0,
            DeletedAtUtc DATETIME2 NULL,
            CreatedAtUtc DATETIME2 NOT NULL,
            CONSTRAINT UQ_users_Email UNIQUE (Email)
        );
        """,
        """
        IF COL_LENGTH('users', 'IsDeleted') IS NULL
        ALTER TABLE users ADD IsDeleted BIT NOT NULL CONSTRAINT DF_users_IsDeleted DEFAULT 0;
        """,
        """
        IF COL_LENGTH('users', 'DeletedAtUtc') IS NULL
        ALTER TABLE users ADD DeletedAtUtc DATETIME2 NULL;
        """,
        """
        IF COL_LENGTH('users', 'IsEmailVerified') IS NULL
        ALTER TABLE users ADD IsEmailVerified BIT NOT NULL CONSTRAINT DF_users_IsEmailVerified DEFAULT 1;
        """,
        """
        IF COL_LENGTH('users', 'EmailVerificationCode') IS NULL
        ALTER TABLE users ADD EmailVerificationCode NVARCHAR(20) NULL;
        """,
        """
        IF COL_LENGTH('users', 'EmailVerificationExpiresAtUtc') IS NULL
        ALTER TABLE users ADD EmailVerificationExpiresAtUtc DATETIME2 NULL;
        """,
        """
        IF COL_LENGTH('users', 'PasswordResetCode') IS NULL
        ALTER TABLE users ADD PasswordResetCode NVARCHAR(20) NULL;
        """,
        """
        IF COL_LENGTH('users', 'PasswordResetExpiresAtUtc') IS NULL
        ALTER TABLE users ADD PasswordResetExpiresAtUtc DATETIME2 NULL;
        """,
        """
        IF COL_LENGTH('users', 'DisplayName') IS NULL
        ALTER TABLE users ADD DisplayName NVARCHAR(80) NULL;
        """,
        """
        IF COL_LENGTH('users', 'ThemePreference') IS NULL
        ALTER TABLE users ADD ThemePreference NVARCHAR(40) NULL;
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
            ProductKey NVARCHAR(64) NULL,
            Name NVARCHAR(220) NOT NULL,
            Description NVARCHAR(1000) NULL,
            Price DECIMAL(10,2) NOT NULL,
            ImageUrl NVARCHAR(1000) NULL,
            CategoryId INT NOT NULL,
            SupermarketId INT NOT NULL,
            Unit NVARCHAR(50) NULL,
            Stock INT NULL,
            IsAvailable BIT NOT NULL DEFAULT 1,
            DataSource NVARCHAR(30) NOT NULL DEFAULT 'manual',
            UpdatedAtUtc DATETIME2 NULL,
            LastImportRunId INT NULL,
            CONSTRAINT FK_products_Category FOREIGN KEY (CategoryId) REFERENCES categories(Id),
            CONSTRAINT FK_products_Supermarket FOREIGN KEY (SupermarketId) REFERENCES supermarkets(Id)
        );
        """,
        """
        IF COL_LENGTH('products', 'ProductKey') IS NULL
        ALTER TABLE products ADD ProductKey NVARCHAR(64) NULL;
        """,
        """
        IF COL_LENGTH('products', 'DataSource') IS NULL
        ALTER TABLE products ADD DataSource NVARCHAR(30) NOT NULL CONSTRAINT DF_products_DataSource DEFAULT 'manual';
        """,
        """
        IF COL_LENGTH('products', 'UpdatedAtUtc') IS NULL
        ALTER TABLE products ADD UpdatedAtUtc DATETIME2 NULL;
        """,
        """
        IF COL_LENGTH('products', 'LastImportRunId') IS NULL
        ALTER TABLE products ADD LastImportRunId INT NULL;
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_products_IsAvailable_Name' AND object_id = OBJECT_ID('products'))
        CREATE INDEX IX_products_IsAvailable_Name ON products (IsAvailable, Name);
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_products_CategoryId' AND object_id = OBJECT_ID('products'))
        CREATE INDEX IX_products_CategoryId ON products (CategoryId);
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_products_SupermarketId' AND object_id = OBJECT_ID('products'))
        CREATE INDEX IX_products_SupermarketId ON products (SupermarketId);
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_products_ProductKey' AND object_id = OBJECT_ID('products'))
        CREATE INDEX IX_products_ProductKey ON products (ProductKey);
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_products_Catalog_Name' AND object_id = OBJECT_ID('products'))
        CREATE INDEX IX_products_Catalog_Name
        ON products (Name)
        INCLUDE (Price, CategoryId, SupermarketId)
        WHERE IsAvailable = 1 AND ImageUrl IS NOT NULL AND ImageUrl <> N'';
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_products_Catalog_Supermarket_Name' AND object_id = OBJECT_ID('products'))
        CREATE INDEX IX_products_Catalog_Supermarket_Name
        ON products (SupermarketId, Name)
        INCLUDE (Price, CategoryId)
        WHERE IsAvailable = 1 AND ImageUrl IS NOT NULL AND ImageUrl <> N'';
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_products_Catalog_Category_Name' AND object_id = OBJECT_ID('products'))
        CREATE INDEX IX_products_Catalog_Category_Name
        ON products (CategoryId, Name)
        INCLUDE (Price, SupermarketId)
        WHERE IsAvailable = 1 AND ImageUrl IS NOT NULL AND ImageUrl <> N'';
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_products_Catalog_Price_Name' AND object_id = OBJECT_ID('products'))
        CREATE INDEX IX_products_Catalog_Price_Name
        ON products (Price, Name)
        INCLUDE (CategoryId, SupermarketId)
        WHERE IsAvailable = 1 AND ImageUrl IS NOT NULL AND ImageUrl <> N'';
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_products_Catalog_Supermarket_Price_Name' AND object_id = OBJECT_ID('products'))
        CREATE INDEX IX_products_Catalog_Supermarket_Price_Name
        ON products (SupermarketId, Price, Name)
        INCLUDE (CategoryId)
        WHERE IsAvailable = 1 AND ImageUrl IS NOT NULL AND ImageUrl <> N'';
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_products_Catalog_PriceDesc_Name' AND object_id = OBJECT_ID('products'))
        CREATE INDEX IX_products_Catalog_PriceDesc_Name
        ON products (Price DESC, Name ASC)
        INCLUDE (CategoryId, SupermarketId)
        WHERE IsAvailable = 1 AND ImageUrl IS NOT NULL AND ImageUrl <> N'';
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_products_Catalog_Supermarket_PriceDesc_Name' AND object_id = OBJECT_ID('products'))
        CREATE INDEX IX_products_Catalog_Supermarket_PriceDesc_Name
        ON products (SupermarketId, Price DESC, Name ASC)
        INCLUDE (CategoryId)
        WHERE IsAvailable = 1 AND ImageUrl IS NOT NULL AND ImageUrl <> N'';
        """,
        """
        IF OBJECT_ID('product_upload_runs', 'U') IS NULL
        CREATE TABLE product_upload_runs (
            Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
            StoreSlug NVARCHAR(100) NOT NULL,
            OriginalFileName NVARCHAR(260) NOT NULL,
            StoredFilePath NVARCHAR(500) NOT NULL,
            Status NVARCHAR(30) NOT NULL,
            TotalRows INT NOT NULL DEFAULT 0,
            InsertedCount INT NOT NULL DEFAULT 0,
            UpdatedCount INT NOT NULL DEFAULT 0,
            UnchangedCount INT NOT NULL DEFAULT 0,
            DeactivatedCount INT NOT NULL DEFAULT 0,
            UploadedAtUtc DATETIME2 NOT NULL,
            CompletedAtUtc DATETIME2 NULL,
            UploadedByUserId INT NOT NULL,
            ErrorMessage NVARCHAR(1000) NULL
        );
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_product_upload_runs_StoreSlug' AND object_id = OBJECT_ID('product_upload_runs'))
        CREATE INDEX IX_product_upload_runs_StoreSlug ON product_upload_runs (StoreSlug);
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_product_upload_runs_UploadedAtUtc' AND object_id = OBJECT_ID('product_upload_runs'))
        CREATE INDEX IX_product_upload_runs_UploadedAtUtc ON product_upload_runs (UploadedAtUtc DESC);
        """,
        """
        IF OBJECT_ID('product_history_data', 'U') IS NULL
        CREATE TABLE product_history_data (
            Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
            ProductId INT NULL,
            ProductKey NVARCHAR(64) NULL,
            Name NVARCHAR(220) NOT NULL,
            Description NVARCHAR(1000) NULL,
            Price DECIMAL(10,2) NOT NULL,
            ImageUrl NVARCHAR(1000) NULL,
            CategoryId INT NOT NULL,
            SupermarketId INT NOT NULL,
            Unit NVARCHAR(50) NULL,
            Stock INT NULL,
            IsAvailable BIT NOT NULL,
            DataSource NVARCHAR(30) NOT NULL,
            ChangeType NVARCHAR(30) NOT NULL,
            ChangedAtUtc DATETIME2 NOT NULL,
            ChangedByUserId INT NOT NULL,
            ImportRunId INT NULL
        );
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_product_history_data_ProductId' AND object_id = OBJECT_ID('product_history_data'))
        CREATE INDEX IX_product_history_data_ProductId ON product_history_data (ProductId);
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_product_history_data_SupermarketId' AND object_id = OBJECT_ID('product_history_data'))
        CREATE INDEX IX_product_history_data_SupermarketId ON product_history_data (SupermarketId);
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_product_history_data_ChangedAtUtc' AND object_id = OBJECT_ID('product_history_data'))
        CREATE INDEX IX_product_history_data_ChangedAtUtc ON product_history_data (ChangedAtUtc DESC);
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
            CustomerNameSnapshot NVARCHAR(201) NULL,
            CustomerEmailSnapshot NVARCHAR(256) NULL,
            Status NVARCHAR(30) NOT NULL,
            PaymentMethod NVARCHAR(30) NOT NULL,
            PaymentStatus NVARCHAR(30) NOT NULL CONSTRAINT DF_orders_PaymentStatus DEFAULT 'PENDING',
            AddressId INT NOT NULL,
            Subtotal DECIMAL(10,2) NOT NULL,
            DeliveryFee DECIMAL(10,2) NOT NULL,
            Total DECIMAL(10,2) NOT NULL,
            AssignedRiderId INT NULL,
            CreatedAtUtc DATETIME2 NOT NULL,
            UpdatedAtUtc DATETIME2 NULL,
            AcceptedAtUtc DATETIME2 NULL,
            PickedUpAtUtc DATETIME2 NULL,
            OutForDeliveryAtUtc DATETIME2 NULL,
            DeliveredAtUtc DATETIME2 NULL,
            CashCollectedAtUtc DATETIME2 NULL,
            CompletedAtUtc DATETIME2 NULL,
            CONSTRAINT FK_orders_User FOREIGN KEY (UserId) REFERENCES users(Id),
            CONSTRAINT FK_orders_Address FOREIGN KEY (AddressId) REFERENCES addresses(Id),
            CONSTRAINT FK_orders_AssignedRider FOREIGN KEY (AssignedRiderId) REFERENCES users(Id)
        );
        """,
        """
        IF COL_LENGTH('orders', 'CustomerNameSnapshot') IS NULL
        ALTER TABLE orders ADD CustomerNameSnapshot NVARCHAR(201) NULL;
        """,
        """
        IF COL_LENGTH('orders', 'CustomerEmailSnapshot') IS NULL
        ALTER TABLE orders ADD CustomerEmailSnapshot NVARCHAR(256) NULL;
        """,
        """
        IF COL_LENGTH('orders', 'PaymentStatus') IS NULL
        ALTER TABLE orders ADD PaymentStatus NVARCHAR(30) NOT NULL CONSTRAINT DF_orders_PaymentStatus_Live DEFAULT 'PENDING';
        """,
        """
        IF COL_LENGTH('orders', 'AssignedRiderId') IS NULL
        ALTER TABLE orders ADD AssignedRiderId INT NULL;
        """,
        """
        IF COL_LENGTH('orders', 'AcceptedAtUtc') IS NULL
        ALTER TABLE orders ADD AcceptedAtUtc DATETIME2 NULL;
        """,
        """
        IF COL_LENGTH('orders', 'PickedUpAtUtc') IS NULL
        ALTER TABLE orders ADD PickedUpAtUtc DATETIME2 NULL;
        """,
        """
        IF COL_LENGTH('orders', 'OutForDeliveryAtUtc') IS NULL
        ALTER TABLE orders ADD OutForDeliveryAtUtc DATETIME2 NULL;
        """,
        """
        IF COL_LENGTH('orders', 'DeliveredAtUtc') IS NULL
        ALTER TABLE orders ADD DeliveredAtUtc DATETIME2 NULL;
        """,
        """
        IF COL_LENGTH('orders', 'CashCollectedAtUtc') IS NULL
        ALTER TABLE orders ADD CashCollectedAtUtc DATETIME2 NULL;
        """,
        """
        IF COL_LENGTH('orders', 'CompletedAtUtc') IS NULL
        ALTER TABLE orders ADD CompletedAtUtc DATETIME2 NULL;
        """,
        """
        UPDATE o
        SET
            o.CustomerNameSnapshot = COALESCE(NULLIF(o.CustomerNameSnapshot, ''), NULLIF(LTRIM(RTRIM(CONCAT(u.FirstName, ' ', u.LastName))), ''), u.Email),
            o.CustomerEmailSnapshot = COALESCE(NULLIF(o.CustomerEmailSnapshot, ''), u.Email)
        FROM orders o
        INNER JOIN users u ON u.Id = o.UserId
        WHERE o.CustomerNameSnapshot IS NULL OR o.CustomerNameSnapshot = '' OR o.CustomerEmailSnapshot IS NULL OR o.CustomerEmailSnapshot = '';
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_orders_AssignedRiderId' AND object_id = OBJECT_ID('orders'))
        CREATE INDEX IX_orders_AssignedRiderId ON orders (AssignedRiderId);
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_orders_Status' AND object_id = OBJECT_ID('orders'))
        CREATE INDEX IX_orders_Status ON orders (Status);
        """,
        """
        IF OBJECT_ID('order_items', 'U') IS NULL
        CREATE TABLE order_items (
            Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
            OrderId INT NOT NULL,
            ProductId INT NOT NULL,
            ProductName NVARCHAR(220) NOT NULL,
            ProductImageUrl NVARCHAR(1000) NULL,
            SupermarketName NVARCHAR(100) NULL,
            Quantity INT NOT NULL,
            UnitPrice DECIMAL(10,2) NOT NULL,
            TotalPrice DECIMAL(10,2) NOT NULL,
            CONSTRAINT FK_order_items_Order FOREIGN KEY (OrderId) REFERENCES orders(Id)
        );
        """,
        """
        IF COL_LENGTH('order_items', 'SupermarketName') IS NULL
        ALTER TABLE order_items ADD SupermarketName NVARCHAR(100) NULL;
        """,
        """
        UPDATE oi
        SET oi.SupermarketName = s.Name
        FROM order_items oi
        INNER JOIN products p ON p.Id = oi.ProductId
        INNER JOIN supermarkets s ON s.Id = p.SupermarketId
        WHERE oi.SupermarketName IS NULL;
        """,
        """
        UPDATE oi
        SET oi.SupermarketName = s.Name
        FROM order_items oi
        INNER JOIN products p ON p.Id = oi.ProductId
        INNER JOIN supermarkets s ON s.Id = p.SupermarketId
        WHERE oi.SupermarketName IS NULL;
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
        IF OBJECT_ID('role_menu_permissions', 'U') IS NULL
        CREATE TABLE role_menu_permissions (
            RoleId INT NOT NULL,
            MenuKey NVARCHAR(100) NOT NULL,
            IsEnabled BIT NOT NULL DEFAULT 1,
            CONSTRAINT PK_role_menu_permissions PRIMARY KEY (RoleId, MenuKey),
            CONSTRAINT FK_role_menu_permissions_Role FOREIGN KEY (RoleId) REFERENCES roles(Id) ON DELETE CASCADE
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
        IF OBJECT_ID('payment_methods', 'U') IS NULL
        CREATE TABLE payment_methods (
            Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
            UserId INT NOT NULL,
            Type NVARCHAR(30) NOT NULL,
            Provider NVARCHAR(50) NULL,
            ProviderPaymentMethodRef NVARCHAR(200) NULL,
            DisplayLabel NVARCHAR(120) NULL,
            Last4 NVARCHAR(4) NULL,
            ExpiryMonth TINYINT NULL,
            ExpiryYear SMALLINT NULL,
            Country NVARCHAR(8) NULL,
            Fingerprint NVARCHAR(120) NULL,
            IsDefault BIT NOT NULL DEFAULT 0,
            CreatedAtUtc DATETIME2 NOT NULL,
            UpdatedAtUtc DATETIME2 NULL,
            CONSTRAINT FK_payment_methods_User FOREIGN KEY (UserId) REFERENCES users(Id) ON DELETE CASCADE
        );
        """,
        """
        IF COL_LENGTH('payment_methods', 'ProviderPaymentMethodRef') IS NULL
        ALTER TABLE payment_methods ADD ProviderPaymentMethodRef NVARCHAR(200) NULL;
        """,
        """
        IF COL_LENGTH('payment_methods', 'DisplayLabel') IS NULL
        ALTER TABLE payment_methods ADD DisplayLabel NVARCHAR(120) NULL;
        """,
        """
        IF COL_LENGTH('payment_methods', 'Country') IS NULL
        ALTER TABLE payment_methods ADD Country NVARCHAR(8) NULL;
        """,
        """
        IF COL_LENGTH('payment_methods', 'Fingerprint') IS NULL
        ALTER TABLE payment_methods ADD Fingerprint NVARCHAR(120) NULL;
        """,
        """
        IF COL_LENGTH('payment_methods', 'UpdatedAtUtc') IS NULL
        ALTER TABLE payment_methods ADD UpdatedAtUtc DATETIME2 NULL;
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_payment_methods_UserId' AND object_id = OBJECT_ID('payment_methods'))
        CREATE INDEX IX_payment_methods_UserId ON payment_methods (UserId);
        """,
        """
        IF OBJECT_ID('payment_transactions', 'U') IS NULL
        CREATE TABLE payment_transactions (
            Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
            OrderId INT NOT NULL,
            PaymentMethodId INT NULL,
            Provider NVARCHAR(50) NULL,
            PaymentType NVARCHAR(30) NOT NULL CONSTRAINT DF_payment_transactions_PaymentType DEFAULT 'UNKNOWN',
            Amount DECIMAL(10,2) NOT NULL,
            Currency NVARCHAR(10) NOT NULL DEFAULT 'EUR',
            Status NVARCHAR(30) NOT NULL,
            ProviderRef NVARCHAR(200) NULL,
            ProviderPaymentIntentRef NVARCHAR(200) NULL,
            ProviderSessionRef NVARCHAR(200) NULL,
            ProviderChargeRef NVARCHAR(200) NULL,
            FeeAmount DECIMAL(10,2) NULL,
            NetAmount DECIMAL(10,2) NULL,
            RawProviderStatus NVARCHAR(60) NULL,
            FailureCode NVARCHAR(100) NULL,
            FailureMessage NVARCHAR(500) NULL,
            MetadataJson NVARCHAR(4000) NULL,
            CreatedAtUtc DATETIME2 NOT NULL,
            UpdatedAtUtc DATETIME2 NULL,
            CONSTRAINT FK_payment_transactions_Order FOREIGN KEY (OrderId) REFERENCES orders(Id),
            CONSTRAINT FK_payment_transactions_PaymentMethod FOREIGN KEY (PaymentMethodId) REFERENCES payment_methods(Id)
        );
        """,
        """
        IF COL_LENGTH('payment_transactions', 'Provider') IS NULL
        ALTER TABLE payment_transactions ADD Provider NVARCHAR(50) NULL;
        """,
        """
        IF COL_LENGTH('payment_transactions', 'PaymentType') IS NULL
        ALTER TABLE payment_transactions ADD PaymentType NVARCHAR(30) NOT NULL CONSTRAINT DF_payment_transactions_PaymentType DEFAULT 'UNKNOWN';
        """,
        """
        IF COL_LENGTH('payment_transactions', 'ProviderPaymentIntentRef') IS NULL
        ALTER TABLE payment_transactions ADD ProviderPaymentIntentRef NVARCHAR(200) NULL;
        """,
        """
        IF COL_LENGTH('payment_transactions', 'ProviderSessionRef') IS NULL
        ALTER TABLE payment_transactions ADD ProviderSessionRef NVARCHAR(200) NULL;
        """,
        """
        IF COL_LENGTH('payment_transactions', 'ProviderChargeRef') IS NULL
        ALTER TABLE payment_transactions ADD ProviderChargeRef NVARCHAR(200) NULL;
        """,
        """
        IF COL_LENGTH('payment_transactions', 'FeeAmount') IS NULL
        ALTER TABLE payment_transactions ADD FeeAmount DECIMAL(10,2) NULL;
        """,
        """
        IF COL_LENGTH('payment_transactions', 'NetAmount') IS NULL
        ALTER TABLE payment_transactions ADD NetAmount DECIMAL(10,2) NULL;
        """,
        """
        IF COL_LENGTH('payment_transactions', 'RawProviderStatus') IS NULL
        ALTER TABLE payment_transactions ADD RawProviderStatus NVARCHAR(60) NULL;
        """,
        """
        IF COL_LENGTH('payment_transactions', 'FailureCode') IS NULL
        ALTER TABLE payment_transactions ADD FailureCode NVARCHAR(100) NULL;
        """,
        """
        IF COL_LENGTH('payment_transactions', 'FailureMessage') IS NULL
        ALTER TABLE payment_transactions ADD FailureMessage NVARCHAR(500) NULL;
        """,
        """
        IF COL_LENGTH('payment_transactions', 'MetadataJson') IS NULL
        ALTER TABLE payment_transactions ADD MetadataJson NVARCHAR(4000) NULL;
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_payment_transactions_OrderId' AND object_id = OBJECT_ID('payment_transactions'))
        CREATE INDEX IX_payment_transactions_OrderId ON payment_transactions (OrderId);
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_payment_transactions_ProviderRef' AND object_id = OBJECT_ID('payment_transactions'))
        CREATE INDEX IX_payment_transactions_ProviderRef ON payment_transactions (ProviderRef);
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_payment_transactions_ProviderSessionRef' AND object_id = OBJECT_ID('payment_transactions'))
        CREATE INDEX IX_payment_transactions_ProviderSessionRef ON payment_transactions (ProviderSessionRef);
        """,
        """
        IF OBJECT_ID('pricing_policy_versions', 'U') IS NULL
        CREATE TABLE pricing_policy_versions (
            Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
            VersionNo INT NOT NULL,
            XFactorPercent DECIMAL(8,4) NOT NULL,
            YFactorAmount DECIMAL(10,2) NOT NULL,
            DeliveryCharge DECIMAL(10,2) NOT NULL,
            IsActive BIT NOT NULL DEFAULT 1,
            EffectiveFromUtc DATETIME2 NOT NULL,
            EffectiveToUtc DATETIME2 NULL,
            Reason NVARCHAR(500) NULL,
            CreatedByUserId INT NOT NULL,
            CreatedAtUtc DATETIME2 NOT NULL,
            CONSTRAINT UQ_pricing_policy_versions_VersionNo UNIQUE (VersionNo),
            CONSTRAINT FK_pricing_policy_versions_User FOREIGN KEY (CreatedByUserId) REFERENCES users(Id)
        );
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_pricing_policy_versions_Active' AND object_id = OBJECT_ID('pricing_policy_versions'))
        CREATE UNIQUE INDEX UX_pricing_policy_versions_Active ON pricing_policy_versions (IsActive) WHERE IsActive = 1;
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_pricing_policy_versions_IsActive_EffectiveFromUtc' AND object_id = OBJECT_ID('pricing_policy_versions'))
        CREATE INDEX IX_pricing_policy_versions_IsActive_EffectiveFromUtc ON pricing_policy_versions (IsActive, EffectiveFromUtc DESC);
        """,
        """
        IF OBJECT_ID('pricing_policy_audit_events', 'U') IS NULL
        CREATE TABLE pricing_policy_audit_events (
            Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
            PolicyVersionId INT NOT NULL,
            ActionType NVARCHAR(30) NOT NULL,
            OldXFactorPercent DECIMAL(8,4) NULL,
            NewXFactorPercent DECIMAL(8,4) NOT NULL,
            OldYFactorAmount DECIMAL(10,2) NULL,
            NewYFactorAmount DECIMAL(10,2) NOT NULL,
            OldDeliveryCharge DECIMAL(10,2) NULL,
            NewDeliveryCharge DECIMAL(10,2) NOT NULL,
            ChangedByUserId INT NOT NULL,
            ChangedAtUtc DATETIME2 NOT NULL,
            CorrelationId NVARCHAR(64) NOT NULL,
            MetadataJson NVARCHAR(4000) NULL,
            CONSTRAINT FK_pricing_policy_audit_events_Version FOREIGN KEY (PolicyVersionId) REFERENCES pricing_policy_versions(Id),
            CONSTRAINT FK_pricing_policy_audit_events_User FOREIGN KEY (ChangedByUserId) REFERENCES users(Id)
        );
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_pricing_policy_audit_events_ChangedAtUtc' AND object_id = OBJECT_ID('pricing_policy_audit_events'))
        CREATE INDEX IX_pricing_policy_audit_events_ChangedAtUtc ON pricing_policy_audit_events (ChangedAtUtc DESC);
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_pricing_policy_audit_events_ChangedByUserId_ChangedAtUtc' AND object_id = OBJECT_ID('pricing_policy_audit_events'))
        CREATE INDEX IX_pricing_policy_audit_events_ChangedByUserId_ChangedAtUtc ON pricing_policy_audit_events (ChangedByUserId, ChangedAtUtc DESC);
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

    private static string EscapeMySqlStringLiteral(string value) => value.Replace("'", "''");

    private static string MySqlAddColumnIfMissing(string tableName, string columnName, string columnDefinition)
    {
        var escapedDefinition = EscapeMySqlStringLiteral(columnDefinition);

        return $"""
        SET @column_exists := (
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = '{tableName}'
              AND COLUMN_NAME = '{columnName}'
        );
        SET @sql := IF(@column_exists = 0,
            'ALTER TABLE `{tableName}` ADD COLUMN `{columnName}` {escapedDefinition}',
            'SELECT 1');
        PREPARE stmt FROM @sql;
        EXECUTE stmt;
        DEALLOCATE PREPARE stmt;
        """;
    }

    private static string MySqlCreateIndexIfMissing(string tableName, string indexName, string indexDefinition)
    {
        var escapedDefinition = EscapeMySqlStringLiteral(indexDefinition);

        return $"""
        SET @index_exists := (
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = '{tableName}'
              AND INDEX_NAME = '{indexName}'
        );
        SET @sql := IF(@index_exists = 0,
            'CREATE INDEX `{indexName}` ON `{tableName}` ({escapedDefinition})',
            'SELECT 1');
        PREPARE stmt FROM @sql;
        EXECUTE stmt;
        DEALLOCATE PREPARE stmt;
        """;
    }

    private static IEnumerable<string> GetMySqlCreateScripts() =>
    [
        """
        CREATE TABLE IF NOT EXISTS `users` (
            `Id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
            `Email` VARCHAR(256) NOT NULL,
            `PasswordHash` VARCHAR(200) NOT NULL,
            `FirstName` VARCHAR(100) NOT NULL,
            `LastName` VARCHAR(100) NOT NULL,
            `DisplayName` VARCHAR(80) NULL,
            `ThemePreference` VARCHAR(40) NULL,
            `Phone` VARCHAR(50) NULL,
            `IsEmailVerified` TINYINT(1) NOT NULL DEFAULT 1,
            `EmailVerificationCode` VARCHAR(20) NULL,
            `EmailVerificationExpiresAtUtc` DATETIME NULL,
            `PasswordResetCode` VARCHAR(20) NULL,
            `PasswordResetExpiresAtUtc` DATETIME NULL,
            `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0,
            `DeletedAtUtc` DATETIME NULL,
            `IsEmailVerified` TINYINT(1) NOT NULL DEFAULT 1,
            `EmailVerificationCode` VARCHAR(20) NULL,
            `EmailVerificationExpiresAtUtc` DATETIME NULL,
            `PasswordResetCode` VARCHAR(20) NULL,
            `PasswordResetExpiresAtUtc` DATETIME NULL,
            `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0,
            `DeletedAtUtc` DATETIME NULL,
            `CreatedAtUtc` DATETIME NOT NULL,
            UNIQUE KEY `UQ_users_Email` (`Email`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """,
        MySqlAddColumnIfMissing("users", "IsDeleted", "TINYINT(1) NOT NULL DEFAULT 0"),
        MySqlAddColumnIfMissing("users", "DeletedAtUtc", "DATETIME NULL"),
        MySqlAddColumnIfMissing("users", "IsEmailVerified", "TINYINT(1) NOT NULL DEFAULT 1"),
        MySqlAddColumnIfMissing("users", "EmailVerificationCode", "VARCHAR(20) NULL"),
        MySqlAddColumnIfMissing("users", "EmailVerificationExpiresAtUtc", "DATETIME NULL"),
        MySqlAddColumnIfMissing("users", "PasswordResetCode", "VARCHAR(20) NULL"),
        MySqlAddColumnIfMissing("users", "PasswordResetExpiresAtUtc", "DATETIME NULL"),
        MySqlAddColumnIfMissing("users", "DisplayName", "VARCHAR(80) NULL"),
        MySqlAddColumnIfMissing("users", "ThemePreference", "VARCHAR(40) NULL"),
        MySqlAddColumnIfMissing("users", "IsDeleted", "TINYINT(1) NOT NULL DEFAULT 0"),
        MySqlAddColumnIfMissing("users", "DeletedAtUtc", "DATETIME NULL"),
        MySqlAddColumnIfMissing("users", "IsEmailVerified", "TINYINT(1) NOT NULL DEFAULT 1"),
        MySqlAddColumnIfMissing("users", "EmailVerificationCode", "VARCHAR(20) NULL"),
        MySqlAddColumnIfMissing("users", "EmailVerificationExpiresAtUtc", "DATETIME NULL"),
        MySqlAddColumnIfMissing("users", "PasswordResetCode", "VARCHAR(20) NULL"),
        MySqlAddColumnIfMissing("users", "PasswordResetExpiresAtUtc", "DATETIME NULL"),
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
            `ProductKey` VARCHAR(64) NULL,
            `ProductKey` VARCHAR(64) NULL,
            `Name` VARCHAR(220) NOT NULL,
            `Description` VARCHAR(1000) NULL,
            `Price` DECIMAL(10,2) NOT NULL,
            `ImageUrl` VARCHAR(1000) NULL,
            `CategoryId` INT NOT NULL,
            `SupermarketId` INT NOT NULL,
            `Unit` VARCHAR(50) NULL,
            `Stock` INT NULL,
            `IsAvailable` TINYINT(1) NOT NULL DEFAULT 1,
            `DataSource` VARCHAR(30) NOT NULL DEFAULT 'manual',
            `UpdatedAtUtc` DATETIME NULL,
            `LastImportRunId` INT NULL,
            `DataSource` VARCHAR(30) NOT NULL DEFAULT 'manual',
            `UpdatedAtUtc` DATETIME NULL,
            `LastImportRunId` INT NULL,
            CONSTRAINT `FK_products_Category` FOREIGN KEY (`CategoryId`) REFERENCES `categories` (`Id`),
            CONSTRAINT `FK_products_Supermarket` FOREIGN KEY (`SupermarketId`) REFERENCES `supermarkets` (`Id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """,
        MySqlAddColumnIfMissing("products", "ProductKey", "VARCHAR(64) NULL"),
        MySqlAddColumnIfMissing("products", "DataSource", "VARCHAR(30) NOT NULL DEFAULT 'manual'"),
        MySqlAddColumnIfMissing("products", "UpdatedAtUtc", "DATETIME NULL"),
        MySqlAddColumnIfMissing("products", "LastImportRunId", "INT NULL"),
        MySqlCreateIndexIfMissing("products", "IX_products_IsAvailable_Name", "`IsAvailable`, `Name`"),
        MySqlCreateIndexIfMissing("products", "IX_products_Catalog_Supermarket_Name", "`IsAvailable`, `SupermarketId`, `Name`"),
        MySqlCreateIndexIfMissing("products", "IX_products_Catalog_Category_Name", "`IsAvailable`, `CategoryId`, `Name`"),
        MySqlCreateIndexIfMissing("products", "IX_products_Catalog_Price_Name", "`IsAvailable`, `Price`, `Name`"),
        MySqlCreateIndexIfMissing("products", "IX_products_Catalog_Supermarket_Price_Name", "`IsAvailable`, `SupermarketId`, `Price`, `Name`"),
        MySqlCreateIndexIfMissing("products", "IX_products_Catalog_PriceDesc_Name", "`IsAvailable`, `Price` DESC, `Name` ASC"),
        MySqlCreateIndexIfMissing("products", "IX_products_Catalog_Supermarket_PriceDesc_Name", "`IsAvailable`, `SupermarketId`, `Price` DESC, `Name` ASC"),
        MySqlCreateIndexIfMissing("products", "IX_products_ProductKey", "`ProductKey`"),
        """
        CREATE TABLE IF NOT EXISTS `product_upload_runs` (
            `Id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
            `StoreSlug` VARCHAR(100) NOT NULL,
            `OriginalFileName` VARCHAR(260) NOT NULL,
            `StoredFilePath` VARCHAR(500) NOT NULL,
            `Status` VARCHAR(30) NOT NULL,
            `TotalRows` INT NOT NULL DEFAULT 0,
            `InsertedCount` INT NOT NULL DEFAULT 0,
            `UpdatedCount` INT NOT NULL DEFAULT 0,
            `UnchangedCount` INT NOT NULL DEFAULT 0,
            `DeactivatedCount` INT NOT NULL DEFAULT 0,
            `UploadedAtUtc` DATETIME NOT NULL,
            `CompletedAtUtc` DATETIME NULL,
            `UploadedByUserId` INT NOT NULL,
            `ErrorMessage` VARCHAR(1000) NULL
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """,
        MySqlCreateIndexIfMissing("product_upload_runs", "IX_product_upload_runs_StoreSlug", "`StoreSlug`"),
        MySqlCreateIndexIfMissing("product_upload_runs", "IX_product_upload_runs_UploadedAtUtc", "`UploadedAtUtc`"),
        """
        CREATE TABLE IF NOT EXISTS `product_history_data` (
            `Id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
            `ProductId` INT NULL,
            `ProductKey` VARCHAR(64) NULL,
            `Name` VARCHAR(220) NOT NULL,
            `Description` VARCHAR(1000) NULL,
            `Price` DECIMAL(10,2) NOT NULL,
            `ImageUrl` VARCHAR(1000) NULL,
            `CategoryId` INT NOT NULL,
            `SupermarketId` INT NOT NULL,
            `Unit` VARCHAR(50) NULL,
            `Stock` INT NULL,
            `IsAvailable` TINYINT(1) NOT NULL,
            `DataSource` VARCHAR(30) NOT NULL,
            `ChangeType` VARCHAR(30) NOT NULL,
            `ChangedAtUtc` DATETIME NOT NULL,
            `ChangedByUserId` INT NOT NULL,
            `ImportRunId` INT NULL
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """,
        MySqlCreateIndexIfMissing("product_history_data", "IX_product_history_data_ProductId", "`ProductId`"),
        MySqlCreateIndexIfMissing("product_history_data", "IX_product_history_data_SupermarketId", "`SupermarketId`"),
        MySqlCreateIndexIfMissing("product_history_data", "IX_product_history_data_ChangedAtUtc", "`ChangedAtUtc`"),
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
            `CustomerNameSnapshot` VARCHAR(201) NULL,
            `CustomerEmailSnapshot` VARCHAR(256) NULL,
            `CustomerNameSnapshot` VARCHAR(201) NULL,
            `CustomerEmailSnapshot` VARCHAR(256) NULL,
            `Status` VARCHAR(30) NOT NULL,
            `PaymentMethod` VARCHAR(30) NOT NULL,
            `PaymentStatus` VARCHAR(30) NOT NULL DEFAULT 'PENDING',
            `PaymentStatus` VARCHAR(30) NOT NULL DEFAULT 'PENDING',
            `AddressId` INT NOT NULL,
            `Subtotal` DECIMAL(10,2) NOT NULL,
            `DeliveryFee` DECIMAL(10,2) NOT NULL,
            `Total` DECIMAL(10,2) NOT NULL,
            `AssignedRiderId` INT NULL,
            `AssignedRiderId` INT NULL,
            `CreatedAtUtc` DATETIME NOT NULL,
            `UpdatedAtUtc` DATETIME NULL,
            `AcceptedAtUtc` DATETIME NULL,
            `PickedUpAtUtc` DATETIME NULL,
            `OutForDeliveryAtUtc` DATETIME NULL,
            `DeliveredAtUtc` DATETIME NULL,
            `CashCollectedAtUtc` DATETIME NULL,
            `CompletedAtUtc` DATETIME NULL,
            `AcceptedAtUtc` DATETIME NULL,
            `PickedUpAtUtc` DATETIME NULL,
            `OutForDeliveryAtUtc` DATETIME NULL,
            `DeliveredAtUtc` DATETIME NULL,
            `CashCollectedAtUtc` DATETIME NULL,
            `CompletedAtUtc` DATETIME NULL,
            CONSTRAINT `FK_orders_User` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`),
            CONSTRAINT `FK_orders_Address` FOREIGN KEY (`AddressId`) REFERENCES `addresses` (`Id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """,
        MySqlAddColumnIfMissing("orders", "PaymentStatus", "VARCHAR(30) NOT NULL DEFAULT 'PENDING'"),
        MySqlAddColumnIfMissing("orders", "AssignedRiderId", "INT NULL"),
        MySqlAddColumnIfMissing("orders", "AcceptedAtUtc", "DATETIME NULL"),
        MySqlAddColumnIfMissing("orders", "PickedUpAtUtc", "DATETIME NULL"),
        MySqlAddColumnIfMissing("orders", "OutForDeliveryAtUtc", "DATETIME NULL"),
        MySqlAddColumnIfMissing("orders", "DeliveredAtUtc", "DATETIME NULL"),
        MySqlAddColumnIfMissing("orders", "CashCollectedAtUtc", "DATETIME NULL"),
        MySqlAddColumnIfMissing("orders", "CompletedAtUtc", "DATETIME NULL"),
        MySqlAddColumnIfMissing("orders", "CustomerNameSnapshot", "VARCHAR(201) NULL"),
        MySqlAddColumnIfMissing("orders", "CustomerEmailSnapshot", "VARCHAR(256) NULL"),
        """
        UPDATE `orders` o
        INNER JOIN `users` u ON u.`Id` = o.`UserId`
        SET
            o.`CustomerNameSnapshot` = COALESCE(NULLIF(o.`CustomerNameSnapshot`, ''), NULLIF(TRIM(CONCAT(COALESCE(u.`FirstName`, ''), ' ', COALESCE(u.`LastName`, ''))), ''), u.`Email`),
            o.`CustomerEmailSnapshot` = COALESCE(NULLIF(o.`CustomerEmailSnapshot`, ''), u.`Email`)
        WHERE o.`CustomerNameSnapshot` IS NULL OR o.`CustomerNameSnapshot` = '' OR o.`CustomerEmailSnapshot` IS NULL OR o.`CustomerEmailSnapshot` = '';
        """,
        MySqlCreateIndexIfMissing("orders", "IX_orders_AssignedRiderId", "`AssignedRiderId`"),
        MySqlCreateIndexIfMissing("orders", "IX_orders_Status", "`Status`"),
        MySqlAddColumnIfMissing("orders", "PaymentStatus", "VARCHAR(30) NOT NULL DEFAULT 'PENDING'"),
        MySqlAddColumnIfMissing("orders", "AssignedRiderId", "INT NULL"),
        MySqlAddColumnIfMissing("orders", "AcceptedAtUtc", "DATETIME NULL"),
        MySqlAddColumnIfMissing("orders", "PickedUpAtUtc", "DATETIME NULL"),
        MySqlAddColumnIfMissing("orders", "OutForDeliveryAtUtc", "DATETIME NULL"),
        MySqlAddColumnIfMissing("orders", "DeliveredAtUtc", "DATETIME NULL"),
        MySqlAddColumnIfMissing("orders", "CashCollectedAtUtc", "DATETIME NULL"),
        MySqlAddColumnIfMissing("orders", "CompletedAtUtc", "DATETIME NULL"),
        MySqlAddColumnIfMissing("orders", "CustomerNameSnapshot", "VARCHAR(201) NULL"),
        MySqlAddColumnIfMissing("orders", "CustomerEmailSnapshot", "VARCHAR(256) NULL"),
        """
        UPDATE `orders` o
        INNER JOIN `users` u ON u.`Id` = o.`UserId`
        SET
            o.`CustomerNameSnapshot` = COALESCE(NULLIF(o.`CustomerNameSnapshot`, ''), NULLIF(TRIM(CONCAT(COALESCE(u.`FirstName`, ''), ' ', COALESCE(u.`LastName`, ''))), ''), u.`Email`),
            o.`CustomerEmailSnapshot` = COALESCE(NULLIF(o.`CustomerEmailSnapshot`, ''), u.`Email`)
        WHERE o.`CustomerNameSnapshot` IS NULL OR o.`CustomerNameSnapshot` = '' OR o.`CustomerEmailSnapshot` IS NULL OR o.`CustomerEmailSnapshot` = '';
        """,
        MySqlCreateIndexIfMissing("orders", "IX_orders_AssignedRiderId", "`AssignedRiderId`"),
        MySqlCreateIndexIfMissing("orders", "IX_orders_Status", "`Status`"),
        """
        CREATE TABLE IF NOT EXISTS `order_items` (
            `Id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
            `OrderId` INT NOT NULL,
            `ProductId` INT NOT NULL,
            `ProductName` VARCHAR(220) NOT NULL,
            `ProductImageUrl` VARCHAR(1000) NULL,
            `SupermarketName` VARCHAR(100) NULL,
            `SupermarketName` VARCHAR(100) NULL,
            `Quantity` INT NOT NULL,
            `UnitPrice` DECIMAL(10,2) NOT NULL,
            `TotalPrice` DECIMAL(10,2) NOT NULL,
            CONSTRAINT `FK_order_items_Order` FOREIGN KEY (`OrderId`) REFERENCES `orders` (`Id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """,
        MySqlAddColumnIfMissing("order_items", "SupermarketName", "VARCHAR(100) NULL"),
        """
        UPDATE `order_items` oi
        INNER JOIN `products` p ON p.`Id` = oi.`ProductId`
        INNER JOIN `supermarkets` s ON s.`Id` = p.`SupermarketId`
        SET oi.`SupermarketName` = s.`Name`
        WHERE oi.`SupermarketName` IS NULL;
        """,
        MySqlAddColumnIfMissing("order_items", "SupermarketName", "VARCHAR(100) NULL"),
        """
        UPDATE `order_items` oi
        INNER JOIN `products` p ON p.`Id` = oi.`ProductId`
        INNER JOIN `supermarkets` s ON s.`Id` = p.`SupermarketId`
        SET oi.`SupermarketName` = s.`Name`
        WHERE oi.`SupermarketName` IS NULL;
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
        CREATE TABLE IF NOT EXISTS `role_menu_permissions` (
            `RoleId` INT NOT NULL,
            `MenuKey` VARCHAR(100) NOT NULL,
            `IsEnabled` TINYINT(1) NOT NULL DEFAULT 1,
            PRIMARY KEY (`RoleId`, `MenuKey`),
            CONSTRAINT `FK_role_menu_permissions_Role` FOREIGN KEY (`RoleId`) REFERENCES `roles` (`Id`) ON DELETE CASCADE
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
        CREATE TABLE IF NOT EXISTS `payment_methods` (
            `Id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
            `UserId` INT NOT NULL,
            `Type` VARCHAR(30) NOT NULL,
            `Provider` VARCHAR(50) NULL,
            `ProviderPaymentMethodRef` VARCHAR(200) NULL,
            `DisplayLabel` VARCHAR(120) NULL,
            `ProviderPaymentMethodRef` VARCHAR(200) NULL,
            `DisplayLabel` VARCHAR(120) NULL,
            `Last4` VARCHAR(4) NULL,
            `ExpiryMonth` TINYINT NULL,
            `ExpiryYear` SMALLINT NULL,
            `Country` VARCHAR(8) NULL,
            `Fingerprint` VARCHAR(120) NULL,
            `Country` VARCHAR(8) NULL,
            `Fingerprint` VARCHAR(120) NULL,
            `IsDefault` TINYINT(1) NOT NULL DEFAULT 0,
            `CreatedAtUtc` DATETIME NOT NULL,
            `UpdatedAtUtc` DATETIME NULL,
            `UpdatedAtUtc` DATETIME NULL,
            CONSTRAINT `FK_payment_methods_User` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`) ON DELETE CASCADE
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """,
        MySqlAddColumnIfMissing("payment_methods", "ProviderPaymentMethodRef", "VARCHAR(200) NULL"),
        MySqlAddColumnIfMissing("payment_methods", "DisplayLabel", "VARCHAR(120) NULL"),
        MySqlAddColumnIfMissing("payment_methods", "Country", "VARCHAR(8) NULL"),
        MySqlAddColumnIfMissing("payment_methods", "Fingerprint", "VARCHAR(120) NULL"),
        MySqlAddColumnIfMissing("payment_methods", "UpdatedAtUtc", "DATETIME NULL"),
        MySqlCreateIndexIfMissing("payment_methods", "IX_payment_methods_UserId", "`UserId`"),
        MySqlAddColumnIfMissing("payment_methods", "ProviderPaymentMethodRef", "VARCHAR(200) NULL"),
        MySqlAddColumnIfMissing("payment_methods", "DisplayLabel", "VARCHAR(120) NULL"),
        MySqlAddColumnIfMissing("payment_methods", "Country", "VARCHAR(8) NULL"),
        MySqlAddColumnIfMissing("payment_methods", "Fingerprint", "VARCHAR(120) NULL"),
        MySqlAddColumnIfMissing("payment_methods", "UpdatedAtUtc", "DATETIME NULL"),
        MySqlCreateIndexIfMissing("payment_methods", "IX_payment_methods_UserId", "`UserId`"),
        """
        CREATE TABLE IF NOT EXISTS `payment_transactions` (
            `Id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
            `OrderId` INT NOT NULL,
            `PaymentMethodId` INT NULL,
            `Provider` VARCHAR(50) NULL,
            `PaymentType` VARCHAR(30) NOT NULL DEFAULT 'UNKNOWN',
            `Provider` VARCHAR(50) NULL,
            `PaymentType` VARCHAR(30) NOT NULL DEFAULT 'UNKNOWN',
            `Amount` DECIMAL(10,2) NOT NULL,
            `Currency` VARCHAR(10) NOT NULL DEFAULT 'EUR',
            `Status` VARCHAR(30) NOT NULL,
            `ProviderRef` VARCHAR(200) NULL,
            `ProviderPaymentIntentRef` VARCHAR(200) NULL,
            `ProviderSessionRef` VARCHAR(200) NULL,
            `ProviderChargeRef` VARCHAR(200) NULL,
            `FeeAmount` DECIMAL(10,2) NULL,
            `NetAmount` DECIMAL(10,2) NULL,
            `RawProviderStatus` VARCHAR(60) NULL,
            `FailureCode` VARCHAR(100) NULL,
            `FailureMessage` VARCHAR(500) NULL,
            `MetadataJson` VARCHAR(4000) NULL,
            `ProviderPaymentIntentRef` VARCHAR(200) NULL,
            `ProviderSessionRef` VARCHAR(200) NULL,
            `ProviderChargeRef` VARCHAR(200) NULL,
            `FeeAmount` DECIMAL(10,2) NULL,
            `NetAmount` DECIMAL(10,2) NULL,
            `RawProviderStatus` VARCHAR(60) NULL,
            `FailureCode` VARCHAR(100) NULL,
            `FailureMessage` VARCHAR(500) NULL,
            `MetadataJson` VARCHAR(4000) NULL,
            `CreatedAtUtc` DATETIME NOT NULL,
            `UpdatedAtUtc` DATETIME NULL,
            CONSTRAINT `FK_payment_transactions_Order` FOREIGN KEY (`OrderId`) REFERENCES `orders` (`Id`),
            CONSTRAINT `FK_payment_transactions_PaymentMethod` FOREIGN KEY (`PaymentMethodId`) REFERENCES `payment_methods` (`Id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """,
        MySqlAddColumnIfMissing("payment_transactions", "Provider", "VARCHAR(50) NULL"),
        MySqlAddColumnIfMissing("payment_transactions", "PaymentType", "VARCHAR(30) NOT NULL DEFAULT 'UNKNOWN'"),
        MySqlAddColumnIfMissing("payment_transactions", "ProviderPaymentIntentRef", "VARCHAR(200) NULL"),
        MySqlAddColumnIfMissing("payment_transactions", "ProviderSessionRef", "VARCHAR(200) NULL"),
        MySqlAddColumnIfMissing("payment_transactions", "ProviderChargeRef", "VARCHAR(200) NULL"),
        MySqlAddColumnIfMissing("payment_transactions", "FeeAmount", "DECIMAL(10,2) NULL"),
        MySqlAddColumnIfMissing("payment_transactions", "NetAmount", "DECIMAL(10,2) NULL"),
        MySqlAddColumnIfMissing("payment_transactions", "RawProviderStatus", "VARCHAR(60) NULL"),
        MySqlAddColumnIfMissing("payment_transactions", "FailureCode", "VARCHAR(100) NULL"),
        MySqlAddColumnIfMissing("payment_transactions", "FailureMessage", "VARCHAR(500) NULL"),
        MySqlAddColumnIfMissing("payment_transactions", "MetadataJson", "VARCHAR(4000) NULL"),
        MySqlCreateIndexIfMissing("payment_transactions", "IX_payment_transactions_OrderId", "`OrderId`"),
        MySqlCreateIndexIfMissing("payment_transactions", "IX_payment_transactions_ProviderRef", "`ProviderRef`"),
        MySqlCreateIndexIfMissing("payment_transactions", "IX_payment_transactions_ProviderSessionRef", "`ProviderSessionRef`"),
        """
        CREATE TABLE IF NOT EXISTS `pricing_policy_versions` (
            `Id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
            `VersionNo` INT NOT NULL,
            `XFactorPercent` DECIMAL(8,4) NOT NULL,
            `YFactorAmount` DECIMAL(10,2) NOT NULL,
            `DeliveryCharge` DECIMAL(10,2) NOT NULL,
            `IsActive` TINYINT(1) NOT NULL DEFAULT 1,
            `EffectiveFromUtc` DATETIME NOT NULL,
            `EffectiveToUtc` DATETIME NULL,
            `Reason` VARCHAR(500) NULL,
            `CreatedByUserId` INT NOT NULL,
            `CreatedAtUtc` DATETIME NOT NULL,
            UNIQUE KEY `UQ_pricing_policy_versions_VersionNo` (`VersionNo`),
            CONSTRAINT `FK_pricing_policy_versions_User` FOREIGN KEY (`CreatedByUserId`) REFERENCES `users` (`Id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """,
        MySqlCreateIndexIfMissing("pricing_policy_versions", "IX_pricing_policy_versions_IsActive_EffectiveFromUtc", "`IsActive`, `EffectiveFromUtc`"),
        """
        CREATE TABLE IF NOT EXISTS `pricing_policy_audit_events` (
            `Id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
            `PolicyVersionId` INT NOT NULL,
            `ActionType` VARCHAR(30) NOT NULL,
            `OldXFactorPercent` DECIMAL(8,4) NULL,
            `NewXFactorPercent` DECIMAL(8,4) NOT NULL,
            `OldYFactorAmount` DECIMAL(10,2) NULL,
            `NewYFactorAmount` DECIMAL(10,2) NOT NULL,
            `OldDeliveryCharge` DECIMAL(10,2) NULL,
            `NewDeliveryCharge` DECIMAL(10,2) NOT NULL,
            `ChangedByUserId` INT NOT NULL,
            `ChangedAtUtc` DATETIME NOT NULL,
            `CorrelationId` VARCHAR(64) NOT NULL,
            `MetadataJson` VARCHAR(4000) NULL,
            CONSTRAINT `FK_pricing_policy_audit_events_Version` FOREIGN KEY (`PolicyVersionId`) REFERENCES `pricing_policy_versions` (`Id`),
            CONSTRAINT `FK_pricing_policy_audit_events_User` FOREIGN KEY (`ChangedByUserId`) REFERENCES `users` (`Id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """,
        MySqlCreateIndexIfMissing("pricing_policy_audit_events", "IX_pricing_policy_audit_events_ChangedAtUtc", "`ChangedAtUtc`"),
        MySqlCreateIndexIfMissing("pricing_policy_audit_events", "IX_pricing_policy_audit_events_ChangedByUserId_ChangedAtUtc", "`ChangedByUserId`, `ChangedAtUtc`"),
        MySqlAddColumnIfMissing("payment_transactions", "Provider", "VARCHAR(50) NULL"),
        MySqlAddColumnIfMissing("payment_transactions", "PaymentType", "VARCHAR(30) NOT NULL DEFAULT 'UNKNOWN'"),
        MySqlAddColumnIfMissing("payment_transactions", "ProviderPaymentIntentRef", "VARCHAR(200) NULL"),
        MySqlAddColumnIfMissing("payment_transactions", "ProviderSessionRef", "VARCHAR(200) NULL"),
        MySqlAddColumnIfMissing("payment_transactions", "ProviderChargeRef", "VARCHAR(200) NULL"),
        MySqlAddColumnIfMissing("payment_transactions", "FeeAmount", "DECIMAL(10,2) NULL"),
        MySqlAddColumnIfMissing("payment_transactions", "NetAmount", "DECIMAL(10,2) NULL"),
        MySqlAddColumnIfMissing("payment_transactions", "RawProviderStatus", "VARCHAR(60) NULL"),
        MySqlAddColumnIfMissing("payment_transactions", "FailureCode", "VARCHAR(100) NULL"),
        MySqlAddColumnIfMissing("payment_transactions", "FailureMessage", "VARCHAR(500) NULL"),
        MySqlAddColumnIfMissing("payment_transactions", "MetadataJson", "VARCHAR(4000) NULL"),
        MySqlCreateIndexIfMissing("payment_transactions", "IX_payment_transactions_OrderId", "`OrderId`"),
        MySqlCreateIndexIfMissing("payment_transactions", "IX_payment_transactions_ProviderRef", "`ProviderRef`"),
        MySqlCreateIndexIfMissing("payment_transactions", "IX_payment_transactions_ProviderSessionRef", "`ProviderSessionRef`"),
        """
        CREATE TABLE IF NOT EXISTS `pricing_policy_versions` (
            `Id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
            `VersionNo` INT NOT NULL,
            `XFactorPercent` DECIMAL(8,4) NOT NULL,
            `YFactorAmount` DECIMAL(10,2) NOT NULL,
            `DeliveryCharge` DECIMAL(10,2) NOT NULL,
            `IsActive` TINYINT(1) NOT NULL DEFAULT 1,
            `EffectiveFromUtc` DATETIME NOT NULL,
            `EffectiveToUtc` DATETIME NULL,
            `Reason` VARCHAR(500) NULL,
            `CreatedByUserId` INT NOT NULL,
            `CreatedAtUtc` DATETIME NOT NULL,
            UNIQUE KEY `UQ_pricing_policy_versions_VersionNo` (`VersionNo`),
            CONSTRAINT `FK_pricing_policy_versions_User` FOREIGN KEY (`CreatedByUserId`) REFERENCES `users` (`Id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """,
        MySqlCreateIndexIfMissing("pricing_policy_versions", "IX_pricing_policy_versions_IsActive_EffectiveFromUtc", "`IsActive`, `EffectiveFromUtc`"),
        """
        CREATE TABLE IF NOT EXISTS `pricing_policy_audit_events` (
            `Id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
            `PolicyVersionId` INT NOT NULL,
            `ActionType` VARCHAR(30) NOT NULL,
            `OldXFactorPercent` DECIMAL(8,4) NULL,
            `NewXFactorPercent` DECIMAL(8,4) NOT NULL,
            `OldYFactorAmount` DECIMAL(10,2) NULL,
            `NewYFactorAmount` DECIMAL(10,2) NOT NULL,
            `OldDeliveryCharge` DECIMAL(10,2) NULL,
            `NewDeliveryCharge` DECIMAL(10,2) NOT NULL,
            `ChangedByUserId` INT NOT NULL,
            `ChangedAtUtc` DATETIME NOT NULL,
            `CorrelationId` VARCHAR(64) NOT NULL,
            `MetadataJson` VARCHAR(4000) NULL,
            CONSTRAINT `FK_pricing_policy_audit_events_Version` FOREIGN KEY (`PolicyVersionId`) REFERENCES `pricing_policy_versions` (`Id`),
            CONSTRAINT `FK_pricing_policy_audit_events_User` FOREIGN KEY (`ChangedByUserId`) REFERENCES `users` (`Id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """,
        MySqlCreateIndexIfMissing("pricing_policy_audit_events", "IX_pricing_policy_audit_events_ChangedAtUtc", "`ChangedAtUtc`"),
        MySqlCreateIndexIfMissing("pricing_policy_audit_events", "IX_pricing_policy_audit_events_ChangedByUserId_ChangedAtUtc", "`ChangedByUserId`, `ChangedAtUtc`"),
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
        ("api_order_items", "order_items"),
        ("api_payment_methods", "payment_methods"),
        ("api_payment_transactions", "payment_transactions"),
        ("api_order_status_history", "order_status_history")
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

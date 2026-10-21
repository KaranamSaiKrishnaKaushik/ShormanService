using Microsoft.EntityFrameworkCore;
using ShormanServicesBackend.Api.Persistence.Entities;

namespace ShormanServicesBackend.Api.Persistence;

public class ApiDbContext(DbContextOptions<ApiDbContext> options) : DbContext(options)
{
    public DbSet<ApiUser> Users => Set<ApiUser>();
    public DbSet<ApiRole> Roles => Set<ApiRole>();
    public DbSet<ApiUserRole> UserRoles => Set<ApiUserRole>();
    public DbSet<ApiRoleMenuPermission> RoleMenuPermissions => Set<ApiRoleMenuPermission>();
    public DbSet<ApiAddress> Addresses => Set<ApiAddress>();
    public DbSet<ApiCategory> Categories => Set<ApiCategory>();
    public DbSet<ApiSupermarket> Supermarkets => Set<ApiSupermarket>();
    public DbSet<ApiProduct> Products => Set<ApiProduct>();
    public DbSet<ApiProductUploadRun> ProductUploadRuns => Set<ApiProductUploadRun>();
    public DbSet<ApiProductHistoryData> ProductHistoryData => Set<ApiProductHistoryData>();
    public DbSet<ApiPricingPolicyVersion> PricingPolicyVersions => Set<ApiPricingPolicyVersion>();
    public DbSet<ApiPricingPolicyAuditEvent> PricingPolicyAuditEvents => Set<ApiPricingPolicyAuditEvent>();
    public DbSet<ApiCart> Carts => Set<ApiCart>();
    public DbSet<ApiCartItem> CartItems => Set<ApiCartItem>();
    public DbSet<ApiOrder> Orders => Set<ApiOrder>();
    public DbSet<ApiOrderItem> OrderItems => Set<ApiOrderItem>();
    public DbSet<ApiPaymentMethod> PaymentMethods => Set<ApiPaymentMethod>();
    public DbSet<ApiPaymentTransaction> PaymentTransactions => Set<ApiPaymentTransaction>();
    public DbSet<ApiOrderStatusHistory> OrderStatusHistory => Set<ApiOrderStatusHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApiUser>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.Email).HasMaxLength(256).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();
            entity.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(50);
            entity.Property(x => x.EmailVerificationCode).HasMaxLength(20);
            entity.Property(x => x.PasswordResetCode).HasMaxLength(20);
        });

        modelBuilder.Entity<ApiRole>(entity =>
        {
            entity.ToTable("roles");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Name).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<ApiRoleMenuPermission>(entity =>
        {
            entity.ToTable("role_menu_permissions");
            entity.HasKey(x => new { x.RoleId, x.MenuKey });
            entity.Property(x => x.MenuKey).HasMaxLength(100).IsRequired();
            entity.HasOne(x => x.Role).WithMany(x => x.MenuPermissions).HasForeignKey(x => x.RoleId);
        });

        modelBuilder.Entity<ApiUserRole>(entity =>
        {
            entity.ToTable("user_roles");
            entity.HasKey(x => new { x.UserId, x.RoleId });
            entity.HasOne(x => x.User).WithMany(x => x.UserRoles).HasForeignKey(x => x.UserId);
            entity.HasOne(x => x.Role).WithMany(x => x.UserRoles).HasForeignKey(x => x.RoleId);
        });

        modelBuilder.Entity<ApiAddress>(entity =>
        {
            entity.ToTable("addresses");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Label).HasMaxLength(100);
            entity.Property(x => x.Street).HasMaxLength(150).IsRequired();
            entity.Property(x => x.HouseNumber).HasMaxLength(30).IsRequired();
            entity.Property(x => x.PostalCode).HasMaxLength(20).IsRequired();
            entity.Property(x => x.City).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Country).HasMaxLength(100).IsRequired();
            entity.HasOne(x => x.User).WithMany(x => x.Addresses).HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<ApiCategory>(entity =>
        {
            entity.ToTable("categories");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Slug).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Icon).HasMaxLength(20);
        });

        modelBuilder.Entity<ApiSupermarket>(entity =>
        {
            entity.ToTable("supermarkets");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Slug).HasMaxLength(100).IsRequired();
            entity.Property(x => x.LogoUrl).HasMaxLength(1000);
            entity.Property(x => x.Color).HasMaxLength(20);
        });

        modelBuilder.Entity<ApiProduct>(entity =>
        {
            entity.ToTable("products");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ProductKey).HasMaxLength(64);
            entity.Property(x => x.Name).HasMaxLength(220).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.Price).HasPrecision(10, 2);
            entity.Property(x => x.ImageUrl).HasMaxLength(1000);
            entity.Property(x => x.Unit).HasMaxLength(50);
            entity.Property(x => x.DataSource).HasMaxLength(30).IsRequired();
            entity.HasOne(x => x.Category).WithMany(x => x.Products).HasForeignKey(x => x.CategoryId);
            entity.HasOne(x => x.Supermarket).WithMany(x => x.Products).HasForeignKey(x => x.SupermarketId);
        });

        modelBuilder.Entity<ApiProductUploadRun>(entity =>
        {
            entity.ToTable("product_upload_runs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.StoreSlug).HasMaxLength(100).IsRequired();
            entity.Property(x => x.OriginalFileName).HasMaxLength(260).IsRequired();
            entity.Property(x => x.StoredFilePath).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.ErrorMessage).HasMaxLength(1000);
            entity.HasIndex(x => x.StoreSlug);
            entity.HasIndex(x => x.UploadedAtUtc);
        });

        modelBuilder.Entity<ApiProductHistoryData>(entity =>
        {
            entity.ToTable("product_history_data");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ProductKey).HasMaxLength(64);
            entity.Property(x => x.Name).HasMaxLength(220).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.Price).HasPrecision(10, 2);
            entity.Property(x => x.ImageUrl).HasMaxLength(1000);
            entity.Property(x => x.Unit).HasMaxLength(50);
            entity.Property(x => x.DataSource).HasMaxLength(30).IsRequired();
            entity.Property(x => x.ChangeType).HasMaxLength(30).IsRequired();
            entity.HasIndex(x => x.ProductId);
            entity.HasIndex(x => x.SupermarketId);
            entity.HasIndex(x => x.ChangedAtUtc);
        });

        modelBuilder.Entity<ApiPricingPolicyVersion>(entity =>
        {
            entity.ToTable("pricing_policy_versions");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.VersionNo).IsUnique();
            entity.HasIndex(x => new { x.IsActive, x.EffectiveFromUtc });
            entity.Property(x => x.XFactorPercent).HasPrecision(8, 4);
            entity.Property(x => x.YFactorAmount).HasPrecision(10, 2);
            entity.Property(x => x.DeliveryCharge).HasPrecision(10, 2);
            entity.Property(x => x.Reason).HasMaxLength(500);
        });

        modelBuilder.Entity<ApiPricingPolicyAuditEvent>(entity =>
        {
            entity.ToTable("pricing_policy_audit_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ActionType).HasMaxLength(30).IsRequired();
            entity.Property(x => x.OldXFactorPercent).HasPrecision(8, 4);
            entity.Property(x => x.NewXFactorPercent).HasPrecision(8, 4);
            entity.Property(x => x.OldYFactorAmount).HasPrecision(10, 2);
            entity.Property(x => x.NewYFactorAmount).HasPrecision(10, 2);
            entity.Property(x => x.OldDeliveryCharge).HasPrecision(10, 2);
            entity.Property(x => x.NewDeliveryCharge).HasPrecision(10, 2);
            entity.Property(x => x.CorrelationId).HasMaxLength(64).IsRequired();
            entity.Property(x => x.MetadataJson).HasMaxLength(4000);
            entity.HasIndex(x => x.PolicyVersionId);
            entity.HasIndex(x => x.ChangedAtUtc);
            entity.HasIndex(x => new { x.ChangedByUserId, x.ChangedAtUtc });
        });

        modelBuilder.Entity<ApiCart>(entity =>
        {
            entity.ToTable("carts");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.HasOne(x => x.User).WithMany(x => x.Carts).HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<ApiCartItem>(entity =>
        {
            entity.ToTable("cart_items");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UnitPrice).HasPrecision(10, 2);
            entity.HasIndex(x => new { x.CartId, x.ProductId }).IsUnique();
            entity.HasOne(x => x.Cart).WithMany(x => x.Items).HasForeignKey(x => x.CartId);
            entity.HasOne(x => x.Product).WithMany(x => x.CartItems).HasForeignKey(x => x.ProductId);
        });

        modelBuilder.Entity<ApiOrder>(entity =>
        {
            entity.ToTable("orders");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CustomerNameSnapshot).HasMaxLength(201);
            entity.Property(x => x.CustomerEmailSnapshot).HasMaxLength(256);
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.PaymentMethod).HasMaxLength(30).IsRequired();
            entity.Property(x => x.PaymentStatus).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Subtotal).HasPrecision(10, 2);
            entity.Property(x => x.DeliveryFee).HasPrecision(10, 2);
            entity.Property(x => x.Total).HasPrecision(10, 2);
            entity.HasIndex(x => x.AssignedRiderId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.UserId, x.Status, x.CreatedAtUtc });
            entity.HasOne(x => x.User).WithMany(x => x.Orders).HasForeignKey(x => x.UserId);
            entity.HasOne(x => x.Address).WithMany().HasForeignKey(x => x.AddressId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.AssignedRider).WithMany(x => x.AssignedOrders).HasForeignKey(x => x.AssignedRiderId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ApiOrderItem>(entity =>
        {
            entity.ToTable("order_items");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ProductName).HasMaxLength(220).IsRequired();
            entity.Property(x => x.ProductImageUrl).HasMaxLength(1000);
            entity.Property(x => x.SupermarketName).HasMaxLength(100);
            entity.Property(x => x.UnitPrice).HasPrecision(10, 2);
            entity.Property(x => x.TotalPrice).HasPrecision(10, 2);
            entity.HasIndex(x => x.OrderId);
            entity.HasIndex(x => x.ProductId);
            entity.HasOne(x => x.Order).WithMany(x => x.Items).HasForeignKey(x => x.OrderId);
        });

        modelBuilder.Entity<ApiPaymentMethod>(entity =>
        {
            entity.ToTable("payment_methods");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Type).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Provider).HasMaxLength(50);
            entity.Property(x => x.ProviderPaymentMethodRef).HasMaxLength(200);
            entity.Property(x => x.DisplayLabel).HasMaxLength(120);
            entity.Property(x => x.Last4).HasMaxLength(4);
            entity.Property(x => x.Country).HasMaxLength(8);
            entity.Property(x => x.Fingerprint).HasMaxLength(120);
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => new { x.UserId, x.Provider, x.ProviderPaymentMethodRef }).IsUnique();
            entity.HasOne(x => x.User).WithMany(x => x.PaymentMethods).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApiPaymentTransaction>(entity =>
        {
            entity.ToTable("payment_transactions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Provider).HasMaxLength(50);
            entity.Property(x => x.PaymentType).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Amount).HasPrecision(10, 2);
            entity.Property(x => x.Currency).HasMaxLength(10).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.ProviderRef).HasMaxLength(200);
            entity.Property(x => x.ProviderPaymentIntentRef).HasMaxLength(200);
            entity.Property(x => x.ProviderSessionRef).HasMaxLength(200);
            entity.Property(x => x.ProviderChargeRef).HasMaxLength(200);
            entity.Property(x => x.FeeAmount).HasPrecision(10, 2);
            entity.Property(x => x.NetAmount).HasPrecision(10, 2);
            entity.Property(x => x.RawProviderStatus).HasMaxLength(60);
            entity.Property(x => x.FailureCode).HasMaxLength(100);
            entity.Property(x => x.FailureMessage).HasMaxLength(500);
            entity.Property(x => x.MetadataJson).HasMaxLength(4000);
            entity.HasIndex(x => x.OrderId);
            entity.HasIndex(x => x.ProviderRef);
            entity.HasIndex(x => x.ProviderSessionRef);
            entity.HasOne(x => x.Order).WithMany(x => x.PaymentTransactions).HasForeignKey(x => x.OrderId);
            entity.HasOne(x => x.PaymentMethod).WithMany(x => x.PaymentTransactions).HasForeignKey(x => x.PaymentMethodId);
        });

        modelBuilder.Entity<ApiOrderStatusHistory>(entity =>
        {
            entity.ToTable("order_status_history");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Note).HasMaxLength(500);
            entity.HasIndex(x => x.OrderId);
            entity.HasOne(x => x.Order).WithMany(x => x.StatusHistory).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApiProduct>()
            .HasIndex(x => x.ProductKey)
            .HasDatabaseName("IX_products_ProductKey")
            .HasFilter(null);

        modelBuilder.Entity<ApiProduct>()
            .HasIndex(x => new { x.SupermarketId, x.ProductKey })
            .HasFilter(null);
    }
}

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
    public DbSet<ApiCart> Carts => Set<ApiCart>();
    public DbSet<ApiCartItem> CartItems => Set<ApiCartItem>();
    public DbSet<ApiOrder> Orders => Set<ApiOrder>();
    public DbSet<ApiOrderItem> OrderItems => Set<ApiOrderItem>();

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
            entity.Property(x => x.Name).HasMaxLength(220).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.Price).HasPrecision(10, 2);
            entity.Property(x => x.ImageUrl).HasMaxLength(1000);
            entity.Property(x => x.Unit).HasMaxLength(50);
            entity.HasOne(x => x.Category).WithMany(x => x.Products).HasForeignKey(x => x.CategoryId);
            entity.HasOne(x => x.Supermarket).WithMany(x => x.Products).HasForeignKey(x => x.SupermarketId);
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
            entity.HasOne(x => x.Order).WithMany(x => x.Items).HasForeignKey(x => x.OrderId);
        });
    }
}

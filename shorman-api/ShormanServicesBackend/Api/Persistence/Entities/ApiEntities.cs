namespace ShormanServicesBackend.Api.Persistence.Entities;

public class ApiUser
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public bool IsEmailVerified { get; set; } = true;
    public string? EmailVerificationCode { get; set; }
    public DateTime? EmailVerificationExpiresAtUtc { get; set; }
    public string? PasswordResetCode { get; set; }
    public DateTime? PasswordResetExpiresAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<ApiAddress> Addresses { get; set; } = [];
    public ICollection<ApiCart> Carts { get; set; } = [];
    public ICollection<ApiOrder> Orders { get; set; } = [];
    public ICollection<ApiPaymentMethod> PaymentMethods { get; set; } = [];
    public ICollection<ApiOrder> AssignedOrders { get; set; } = [];
    public ICollection<ApiUserRole> UserRoles { get; set; } = [];
}

public class ApiRole
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<ApiUserRole> UserRoles { get; set; } = [];
    public ICollection<ApiRoleMenuPermission> MenuPermissions { get; set; } = [];
}

public class ApiRoleMenuPermission
{
    public int RoleId { get; set; }
    public string MenuKey { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }

    public ApiRole Role { get; set; } = null!;
}

public class ApiUserRole
{
    public int UserId { get; set; }
    public int RoleId { get; set; }

    public ApiUser User { get; set; } = null!;
    public ApiRole Role { get; set; } = null!;
}

public class ApiAddress
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string? Label { get; set; }
    public string Street { get; set; } = string.Empty;
    public string HouseNumber { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public bool IsDefault { get; set; }

    public ApiUser User { get; set; } = null!;
}

public class ApiCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Icon { get; set; }

    public ICollection<ApiProduct> Products { get; set; } = [];
}

public class ApiSupermarket
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? Color { get; set; }

    public ICollection<ApiProduct> Products { get; set; } = [];
}

public class ApiProduct
{
    public int Id { get; set; }
    public string? ProductKey { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public int CategoryId { get; set; }
    public int SupermarketId { get; set; }
    public string? Unit { get; set; }
    public int? Stock { get; set; }
    public bool IsAvailable { get; set; } = true;
    public string DataSource { get; set; } = "manual";
    public DateTime? UpdatedAtUtc { get; set; }
    public int? LastImportRunId { get; set; }

    public ApiCategory Category { get; set; } = null!;
    public ApiSupermarket Supermarket { get; set; } = null!;
    public ICollection<ApiCartItem> CartItems { get; set; } = [];
}

public class ApiProductUploadRun
{
    public int Id { get; set; }
    public string StoreSlug { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFilePath { get; set; } = string.Empty;
    public string Status { get; set; } = "processing";
    public int TotalRows { get; set; }
    public int InsertedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int UnchangedCount { get; set; }
    public int DeactivatedCount { get; set; }
    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    public int UploadedByUserId { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ApiProductHistoryData
{
    public int Id { get; set; }
    public int? ProductId { get; set; }
    public string? ProductKey { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public int CategoryId { get; set; }
    public int SupermarketId { get; set; }
    public string? Unit { get; set; }
    public int? Stock { get; set; }
    public bool IsAvailable { get; set; }
    public string DataSource { get; set; } = "manual";
    public string ChangeType { get; set; } = string.Empty;
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
    public int ChangedByUserId { get; set; }
    public int? ImportRunId { get; set; }
}

public class ApiPricingPolicyVersion
{
    public int Id { get; set; }
    public int VersionNo { get; set; }
    public decimal XFactorPercent { get; set; }
    public decimal YFactorAmount { get; set; }
    public decimal DeliveryCharge { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime EffectiveFromUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveToUtc { get; set; }
    public string? Reason { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class ApiPricingPolicyAuditEvent
{
    public int Id { get; set; }
    public int PolicyVersionId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public decimal? OldXFactorPercent { get; set; }
    public decimal NewXFactorPercent { get; set; }
    public decimal? OldYFactorAmount { get; set; }
    public decimal NewYFactorAmount { get; set; }
    public decimal? OldDeliveryCharge { get; set; }
    public decimal NewDeliveryCharge { get; set; }
    public int ChangedByUserId { get; set; }
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");
    public string? MetadataJson { get; set; }
}

public class ApiCart
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public ApiUser User { get; set; } = null!;
    public ICollection<ApiCartItem> Items { get; set; } = [];
}

public class ApiCartItem
{
    public int Id { get; set; }
    public int CartId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    public ApiCart Cart { get; set; } = null!;
    public ApiProduct Product { get; set; } = null!;
}

public class ApiOrder
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string? CustomerNameSnapshot { get; set; }
    public string? CustomerEmailSnapshot { get; set; }
    public string Status { get; set; } = "AWAITING_PICKUP";
    public string PaymentMethod { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = "PENDING";
    public int AddressId { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal Total { get; set; }
    public int? AssignedRiderId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public DateTime? AcceptedAtUtc { get; set; }
    public DateTime? PickedUpAtUtc { get; set; }
    public DateTime? OutForDeliveryAtUtc { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
    public DateTime? CashCollectedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public ApiUser User { get; set; } = null!;
    public ApiAddress Address { get; set; } = null!;
    public ApiUser? AssignedRider { get; set; }
    public ICollection<ApiOrderItem> Items { get; set; } = [];
    public ICollection<ApiPaymentTransaction> PaymentTransactions { get; set; } = [];
    public ICollection<ApiOrderStatusHistory> StatusHistory { get; set; } = [];
}

public class ApiOrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductImageUrl { get; set; }
    public string? SupermarketName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }

    public ApiOrder Order { get; set; } = null!;
}

public class ApiPaymentMethod
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string? ProviderPaymentMethodRef { get; set; }
    public string? DisplayLabel { get; set; }
    public string? Last4 { get; set; }
    public byte? ExpiryMonth { get; set; }
    public short? ExpiryYear { get; set; }
    public string? Country { get; set; }
    public string? Fingerprint { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public ApiUser User { get; set; } = null!;
    public ICollection<ApiPaymentTransaction> PaymentTransactions { get; set; } = [];
}

public class ApiPaymentTransaction
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int? PaymentMethodId { get; set; }
    public string? Provider { get; set; }
    public string PaymentType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public string Status { get; set; } = "PENDING";
    public string? ProviderRef { get; set; }
    public string? ProviderPaymentIntentRef { get; set; }
    public string? ProviderSessionRef { get; set; }
    public string? ProviderChargeRef { get; set; }
    public decimal? FeeAmount { get; set; }
    public decimal? NetAmount { get; set; }
    public string? RawProviderStatus { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
    public string? MetadataJson { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public ApiOrder Order { get; set; } = null!;
    public ApiPaymentMethod? PaymentMethod { get; set; }
}

public class ApiOrderStatusHistory
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;

    public ApiOrder Order { get; set; } = null!;
}

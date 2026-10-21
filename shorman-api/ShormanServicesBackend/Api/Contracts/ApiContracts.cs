namespace ShormanServicesBackend.Api.Contracts;

public record UserDto(int Id, string Email, string FirstName, string LastName, string? DisplayName, string? ThemePreference, string? Phone, string? CreatedAt, IReadOnlyCollection<string> Roles);
public record AuthResponse(string Token, UserDto User);
public record LoginRequest(string Email, string Password);
public record RegisterRequest(string FirstName, string LastName, string Email, string Password, string? Phone);
public record RegisterResponse(string Email, string Message, bool VerificationRequired, string? VerificationCode);
public record Auth0ExchangeRequest(string Email, string? FirstName, string? LastName);
public record UpdateCurrentUserProfileRequest(string? DisplayName, string? ThemePreference);
public record VerifyEmailRequest(string Email, string Code);
public record PasswordResetRequest(string Email);
public record PasswordResetConfirmRequest(string Email, string Code, string NewPassword);
public record PasswordResetRequestResponse(string Message, string? ResetCode);
public record UpdateUserRoleRequest(string Role);
public record DeleteUserResponse(int UserId, string Message);
public record AdminUserListItemDto(int Id, string Email, string FirstName, string LastName, string? Phone, string? CreatedAt, IReadOnlyCollection<string> Roles);
public record OrderAlertCountDto(int Count);
public record AdminOrderSummaryDto(
	int Id,
	int UserId,
	string CustomerName,
	string CustomerEmail,
	string Status,
	string PaymentMethod,
	string PaymentStatus,
	int AddressId,
	string? DeliveryAddress,
	IReadOnlyCollection<OrderItemDto> Items,
	decimal Subtotal,
	decimal DeliveryFee,
	decimal Total,
	int? AssignedRiderId,
	string? AssignedRiderName,
	string CreatedAt,
	string? UpdatedAt,
	string? AcceptedAt,
	string? PickedUpAt,
	string? OutForDeliveryAt,
	string? DeliveredAt,
	string? CashCollectedAt,
	string? CompletedAt);
public record RoleMenuPermissionDto(string Role, string MenuKey, bool IsEnabled);
public record RoleMenuPermissionsDto(string Role, IReadOnlyCollection<RoleMenuPermissionDto> Permissions);
public record UpdateRoleMenuPermissionsRequest(string Role, IReadOnlyCollection<string> EnabledMenuKeys);
public record CurrentMenuPermissionsDto(IReadOnlyCollection<string> EnabledMenuKeys);

public record AddressDto(int Id, int UserId, string? Label, string Street, string HouseNumber, string PostalCode, string City, string Country, bool IsDefault);
public record CreateAddressRequest(string? Label, string Street, string HouseNumber, string PostalCode, string City, string Country, bool IsDefault = false);
public record UpdateAddressRequest(string? Label, string Street, string HouseNumber, string PostalCode, string City, string Country, bool IsDefault = false);

public record CategoryDto(int Id, string Name, string Slug, string? Icon);
public record SupermarketDto(int Id, string Name, string Slug, string? LogoUrl, string? Color);
public record ProductDto(int Id, string Name, string? Description, decimal Price, string? ImageUrl, int CategoryId, CategoryDto? Category, int SupermarketId, SupermarketDto? Supermarket, string? Unit, int? Stock, bool IsAvailable);
public record ProductManagementProductDto(int Id, string? ProductKey, string Name, decimal Price, string? ImageUrl, int CategoryId, CategoryDto? Category, int SupermarketId, SupermarketDto? Supermarket, string? Unit, int? Stock, bool IsAvailable, string DataSource, string? UpdatedAt);
public record ProductUploadRunDto(int Id, string StoreSlug, string OriginalFileName, string StoredFileUrl, string Status, int TotalRows, int InsertedCount, int UpdatedCount, int UnchangedCount, int DeactivatedCount, string UploadedAt, string? CompletedAt, string? UploadedByName, string? ErrorMessage);
public record ProductHistoryDataDto(int Id, int? ProductId, string? ProductKey, string Name, decimal Price, string? ImageUrl, int CategoryId, CategoryDto? Category, int SupermarketId, SupermarketDto? Supermarket, string? Unit, int? Stock, bool IsAvailable, string DataSource, string ChangeType, string ChangedAt, string? ChangedByName, int? ImportRunId);
public record PricingPolicyVersionDto(int Id, int VersionNo, decimal XFactorPercent, decimal YFactorAmount, decimal DeliveryCharge, bool IsActive, string EffectiveFrom, string? EffectiveTo, string? Reason, int CreatedByUserId, string? CreatedByName, string CreatedAt);
public record PricingPolicyAuditEventDto(int Id, int PolicyVersionId, string ActionType, decimal? OldXFactorPercent, decimal NewXFactorPercent, decimal? OldYFactorAmount, decimal NewYFactorAmount, decimal? OldDeliveryCharge, decimal NewDeliveryCharge, int ChangedByUserId, string? ChangedByName, string ChangedAt, string CorrelationId, string? MetadataJson);
public record ApplyPricingPolicyRequest(decimal XFactorPercent, decimal YFactorAmount, decimal DeliveryCharge, string? Reason);
public record UpdateProductRequest(string Name, string? Description, decimal Price, string? ImageUrl, int CategoryId, int SupermarketId, string? Unit, int? Stock, bool IsAvailable);
public record PagedResultDto<T>(IReadOnlyCollection<T> Items, int TotalCount, int Page, int PageSize);

public record DeliveryCheckResult(bool Eligible, string Message, decimal? DeliveryFee);

public record CartItemDto(int Id, int ProductId, ProductDto Product, int Quantity, decimal UnitPrice, decimal LineTotal);
public record CartDto(int Id, int UserId, IReadOnlyCollection<CartItemDto> Items, decimal Total, int ItemCount, string CreatedAt, string? UpdatedAt);
public record AddCartItemRequest(int ProductId, int Quantity);
public record UpdateCartItemRequest(int Quantity);

public record CreateOrderItemRequest(int ProductId, int Quantity);
public record CreateOrderRequest(int AddressId, string PaymentMethod, IReadOnlyCollection<CreateOrderItemRequest> Items);
public record CheckoutSessionResponse(int OrderId, string PaymentMethod, string PaymentStatus, string CheckoutUrl, string SessionId);
public record OrderItemDto(int Id, int ProductId, string ProductName, string? ProductImageUrl, string? SupermarketName, int Quantity, decimal UnitPrice, decimal TotalPrice);
public record OrderDto(
	int Id,
	int UserId,
	string Status,
	string PaymentMethod,
	string PaymentStatus,
	int AddressId,
	string? DeliveryAddress,
	IReadOnlyCollection<OrderItemDto> Items,
	decimal Subtotal,
	decimal DeliveryFee,
	decimal Total,
	int? AssignedRiderId,
	string? AssignedRiderName,
	string CreatedAt,
	string? UpdatedAt,
	string? AcceptedAt,
	string? PickedUpAt,
	string? OutForDeliveryAt,
	string? DeliveredAt,
	string? CashCollectedAt,
	string? CompletedAt);

public record OrderInsightsStoreSpendDto(string Store, decimal Spend);
public record OrderInsightsMonthlyStoreSpendDto(string MonthKey, string MonthLabel, IReadOnlyCollection<OrderInsightsStoreSpendDto> Stores);
public record OrderInsightsCategoryDto(string Category, string Segment, decimal Spend, int OrderCount);
public record OrderInsightsHeatmapCellDto(string Store, string Category, string Segment, decimal Spend);
public record OrderInsightsReorderedProductDto(int ProductId, string ProductName, string? ProductImageUrl, string Store, int Quantity, int RepeatCount, decimal Spend);

public record OrderInsightsDto(
	decimal TotalSpend,
	int TotalOrders,
	decimal AverageBasket,
	decimal PreviousTotalSpend,
	int PreviousTotalOrders,
	decimal PreviousAverageBasket,
	string FavoriteStore,
	decimal FavoriteStoreSpend,
	decimal FavoriteStoreShare,
	string FavoriteCategory,
	decimal FavoriteCategorySpend,
	decimal FavoriteCategoryShare,
	decimal GroceryShare,
	decimal DrugstoreShare,
	IReadOnlyCollection<OrderInsightsStoreSpendDto> StoreSpend,
	IReadOnlyCollection<OrderInsightsMonthlyStoreSpendDto> MonthlySpend,
	IReadOnlyCollection<OrderInsightsCategoryDto> TopCategories,
	IReadOnlyCollection<OrderInsightsHeatmapCellDto> Heatmap,
	IReadOnlyCollection<OrderInsightsReorderedProductDto> TopReorderedProducts);

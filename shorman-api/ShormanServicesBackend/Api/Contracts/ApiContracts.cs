namespace ShormanServicesBackend.Api.Contracts;

public record UserDto(int Id, string Email, string FirstName, string LastName, string? Phone, string? CreatedAt, IReadOnlyCollection<string> Roles);
public record AuthResponse(string Token, UserDto User);
public record LoginRequest(string Email, string Password);
public record RegisterRequest(string FirstName, string LastName, string Email, string Password, string? Phone);
public record Auth0ExchangeRequest(string Email, string? FirstName, string? LastName);
public record UpdateUserRoleRequest(string Role);
public record AdminUserListItemDto(int Id, string Email, string FirstName, string LastName, string? Phone, string? CreatedAt, IReadOnlyCollection<string> Roles);
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
public record UpdateProductRequest(string Name, string? Description, decimal Price, string? ImageUrl, int CategoryId, int SupermarketId, string? Unit, int? Stock, bool IsAvailable);
public record PagedResultDto<T>(IReadOnlyCollection<T> Items, int TotalCount, int Page, int PageSize);

public record DeliveryCheckResult(bool Eligible, string Message, decimal? DeliveryFee);

public record CartItemDto(int Id, int ProductId, ProductDto Product, int Quantity, decimal UnitPrice, decimal LineTotal);
public record CartDto(int Id, int UserId, IReadOnlyCollection<CartItemDto> Items, decimal Total, int ItemCount, string CreatedAt, string? UpdatedAt);
public record AddCartItemRequest(int ProductId, int Quantity);
public record UpdateCartItemRequest(int Quantity);

public record CreateOrderItemRequest(int ProductId, int Quantity);
public record CreateOrderRequest(int AddressId, string PaymentMethod, IReadOnlyCollection<CreateOrderItemRequest> Items);
public record OrderItemDto(int Id, int ProductId, string ProductName, string? ProductImageUrl, int Quantity, decimal UnitPrice, decimal TotalPrice);
public record OrderDto(int Id, int UserId, string Status, string PaymentMethod, int AddressId, string? DeliveryAddress, IReadOnlyCollection<OrderItemDto> Items, decimal Subtotal, decimal DeliveryFee, decimal Total, string CreatedAt, string? UpdatedAt);

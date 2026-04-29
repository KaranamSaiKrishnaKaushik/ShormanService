namespace ShormanServicesBackend.Api.Persistence.Entities;

public class ApiUser
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<ApiAddress> Addresses { get; set; } = [];
    public ICollection<ApiCart> Carts { get; set; } = [];
    public ICollection<ApiOrder> Orders { get; set; } = [];
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
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public int CategoryId { get; set; }
    public int SupermarketId { get; set; }
    public string? Unit { get; set; }
    public int? Stock { get; set; }
    public bool IsAvailable { get; set; } = true;

    public ApiCategory Category { get; set; } = null!;
    public ApiSupermarket Supermarket { get; set; } = null!;
    public ICollection<ApiCartItem> CartItems { get; set; } = [];
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
    public string Status { get; set; } = "PENDING";
    public string PaymentMethod { get; set; } = string.Empty;
    public int AddressId { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public ApiUser User { get; set; } = null!;
    public ApiAddress Address { get; set; } = null!;
    public ICollection<ApiOrderItem> Items { get; set; } = [];
}

public class ApiOrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductImageUrl { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }

    public ApiOrder Order { get; set; } = null!;
}

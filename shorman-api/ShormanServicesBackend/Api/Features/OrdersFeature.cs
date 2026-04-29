using MediatR;
using Microsoft.EntityFrameworkCore;
using ShormanServicesBackend.Api.Contracts;
using ShormanServicesBackend.Api.Persistence;
using ShormanServicesBackend.Api.Persistence.Entities;

namespace ShormanServicesBackend.Api.Features;

public record GetOrdersQuery(int UserId) : IRequest<IReadOnlyCollection<OrderDto>>;
public record GetOrderByIdQuery(int UserId, int OrderId) : IRequest<OrderDto?>;
public record CreateOrderCommand(int UserId, CreateOrderRequest Request) : IRequest<OrderDto>;

public class GetOrdersQueryHandler(ApiDbContext dbContext) : IRequestHandler<GetOrdersQuery, IReadOnlyCollection<OrderDto>>
{
    public async Task<IReadOnlyCollection<OrderDto>> Handle(GetOrdersQuery request, CancellationToken cancellationToken)
    {
        var orders = await dbContext.Orders
            .AsNoTracking()
            .Include(x => x.Address)
            .Include(x => x.Items)
            .Where(x => x.UserId == request.UserId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return orders.Select(Map).ToList();
    }

    internal static OrderDto Map(ApiOrder order)
    {
        var address = $"{order.Address.Street} {order.Address.HouseNumber}, {order.Address.PostalCode} {order.Address.City}, {order.Address.Country}";
        return new OrderDto(
            order.Id,
            order.UserId,
            order.Status,
            order.PaymentMethod,
            order.AddressId,
            address,
            order.Items.Select(item => new OrderItemDto(item.Id, item.ProductId, item.ProductName, item.ProductImageUrl, item.Quantity, item.UnitPrice, item.TotalPrice)).ToList(),
            order.Subtotal,
            order.DeliveryFee,
            order.Total,
            order.CreatedAtUtc.ToString("O"),
            order.UpdatedAtUtc?.ToString("O"));
    }
}

public class GetOrderByIdQueryHandler(ApiDbContext dbContext) : IRequestHandler<GetOrderByIdQuery, OrderDto?>
{
    public async Task<OrderDto?> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders
            .AsNoTracking()
            .Include(x => x.Address)
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == request.OrderId && x.UserId == request.UserId, cancellationToken);

        return order is null ? null : GetOrdersQueryHandler.Map(order);
    }
}

public class CreateOrderCommandHandler(ApiDbContext dbContext) : IRequestHandler<CreateOrderCommand, OrderDto>
{
    public async Task<OrderDto> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        var address = await dbContext.Addresses.SingleOrDefaultAsync(x => x.Id == request.Request.AddressId && x.UserId == request.UserId, cancellationToken)
            ?? throw new InvalidOperationException("Address not found.");

        if (request.Request.Items.Count == 0)
        {
            throw new InvalidOperationException("Order must contain at least one item.");
        }

        var productIds = request.Request.Items.Select(x => x.ProductId).Distinct().ToList();
        var products = await dbContext.Products
            .Where(x => productIds.Contains(x.Id) && x.IsAvailable)
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var orderItems = new List<ApiOrderItem>();
        decimal subtotal = 0;

        foreach (var item in request.Request.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                throw new InvalidOperationException($"Product {item.ProductId} not found.");
            }

            var totalPrice = product.Price * item.Quantity;
            subtotal += totalPrice;
            orderItems.Add(new ApiOrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                ProductImageUrl = product.ImageUrl,
                Quantity = item.Quantity,
                UnitPrice = product.Price,
                TotalPrice = totalPrice
            });
        }

        const decimal deliveryFee = 2.99m;
        var order = new ApiOrder
        {
            UserId = request.UserId,
            Status = "PENDING",
            PaymentMethod = request.Request.PaymentMethod,
            AddressId = address.Id,
            Subtotal = subtotal,
            DeliveryFee = deliveryFee,
            Total = subtotal + deliveryFee,
            CreatedAtUtc = DateTime.UtcNow,
            Items = orderItems
        };

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);

        await dbContext.Entry(order).Reference(x => x.Address).LoadAsync(cancellationToken);
        await dbContext.Entry(order).Collection(x => x.Items).LoadAsync(cancellationToken);

        return GetOrdersQueryHandler.Map(order);
    }
}

using System.Net;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShormanServicesBackend.Api.Contracts;
using ShormanServicesBackend.Api.Persistence;
using ShormanServicesBackend.Api.Persistence.Entities;

namespace ShormanServicesBackend.Api.Features;

public record GetOrdersQuery(int UserId) : IRequest<IReadOnlyCollection<OrderDto>>;
public record GetOrderByIdQuery(int UserId, int OrderId) : IRequest<OrderDto?>;
public record CreateOrderCommand(int UserId, CreateOrderRequest Request) : IRequest<OrderDto>;
public record GetAvailableRiderOrdersQuery() : IRequest<IReadOnlyCollection<OrderDto>>;
public record GetAssignedRiderOrdersQuery(int RiderUserId) : IRequest<IReadOnlyCollection<OrderDto>>;
public record GetRiderOrderByIdQuery(int RiderUserId, int OrderId) : IRequest<OrderDto?>;
public record AcceptRiderOrderCommand(int RiderUserId, int OrderId) : IRequest<OrderDto>;
public record MarkRiderOrderPickedUpCommand(int RiderUserId, int OrderId) : IRequest<OrderDto>;
public record MarkRiderOrderOutForDeliveryCommand(int RiderUserId, int OrderId) : IRequest<OrderDto>;
public record MarkRiderOrderDeliveredCommand(int RiderUserId, int OrderId) : IRequest<OrderDto>;
public record MarkRiderOrderCashCollectedCommand(int RiderUserId, int OrderId) : IRequest<OrderDto>;
public record CompleteRiderOrderCommand(int RiderUserId, int OrderId) : IRequest<OrderDto>;

internal static class OrderStatuses
{
    public const string AwaitingPickup = "AWAITING_PICKUP";
    public const string AssignedToRider = "ASSIGNED_TO_RIDER";
    public const string PickedUp = "PICKED_UP";
    public const string OutForDelivery = "OUT_FOR_DELIVERY";
    public const string Delivered = "DELIVERED";
    public const string Completed = "COMPLETED";
    public const string Cancelled = "CANCELLED";
}

internal static class OrderPaymentStatuses
{
    public const string Pending = "PENDING";
    public const string Paid = "PAID";
    public const string CashPending = "CASH_PENDING";
    public const string CashCollected = "CASH_COLLECTED";
}

public class GetOrdersQueryHandler(ApiDbContext dbContext) : IRequestHandler<GetOrdersQuery, IReadOnlyCollection<OrderDto>>
{
    public async Task<IReadOnlyCollection<OrderDto>> Handle(GetOrdersQuery request, CancellationToken cancellationToken)
    {
        var orders = await dbContext.Orders
            .AsNoTracking()
            .Include(x => x.Address)
            .Include(x => x.AssignedRider)
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
            order.PaymentStatus,
            order.AddressId,
            address,
            order.Items.Select(item => new OrderItemDto(item.Id, item.ProductId, WebUtility.HtmlDecode(item.ProductName), item.ProductImageUrl, item.SupermarketName, item.Quantity, item.UnitPrice, item.TotalPrice)).ToList(),
            order.Subtotal,
            order.DeliveryFee,
            order.Total,
            order.AssignedRiderId,
            order.AssignedRider is null ? null : $"{order.AssignedRider.FirstName} {order.AssignedRider.LastName}".Trim(),
            order.CreatedAtUtc.ToString("O"),
            order.UpdatedAtUtc?.ToString("O"),
            order.AcceptedAtUtc?.ToString("O"),
            order.PickedUpAtUtc?.ToString("O"),
            order.OutForDeliveryAtUtc?.ToString("O"),
            order.DeliveredAtUtc?.ToString("O"),
            order.CashCollectedAtUtc?.ToString("O"),
            order.CompletedAtUtc?.ToString("O"));
    }
}

public class GetOrderByIdQueryHandler(ApiDbContext dbContext) : IRequestHandler<GetOrderByIdQuery, OrderDto?>
{
    public async Task<OrderDto?> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders
            .AsNoTracking()
            .Include(x => x.Address)
            .Include(x => x.AssignedRider)
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == request.OrderId && x.UserId == request.UserId, cancellationToken);

        return order is null ? null : GetOrdersQueryHandler.Map(order);
    }
}

public class CreateOrderCommandHandler(ApiDbContext dbContext, IMediator mediator) : IRequestHandler<CreateOrderCommand, OrderDto>
{
    public async Task<OrderDto> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .SingleAsync(x => x.Id == request.UserId, cancellationToken);

        var address = await dbContext.Addresses.SingleOrDefaultAsync(x => x.Id == request.Request.AddressId && x.UserId == request.UserId, cancellationToken)
            ?? throw new InvalidOperationException("Address not found.");

        var deliveryCheck = await mediator.Send(
            new CheckDeliveryQuery(address.PostalCode, address.City, address.Street, address.HouseNumber, address.Country),
            cancellationToken);
        if (!deliveryCheck.Eligible)
        {
            throw new InvalidOperationException(deliveryCheck.Message);
        }

        if (request.Request.Items.Count == 0)
        {
            throw new InvalidOperationException("Order must contain at least one item.");
        }

        var productIds = request.Request.Items.Select(x => x.ProductId).Distinct().ToList();
        var products = await dbContext.Products
            .Include(x => x.Supermarket)
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
                ProductName = WebUtility.HtmlDecode(product.Name),
                ProductImageUrl = product.ImageUrl,
                SupermarketName = product.Supermarket?.Name,
                Quantity = item.Quantity,
                UnitPrice = product.Price,
                TotalPrice = totalPrice
            });
        }

        var order = new ApiOrder
        {
            UserId = request.UserId,
            CustomerNameSnapshot = $"{user.FirstName} {user.LastName}".Trim(),
            CustomerEmailSnapshot = user.Email,
            Status = OrderStatuses.AwaitingPickup,
            PaymentMethod = request.Request.PaymentMethod,
            PaymentStatus = string.Equals(request.Request.PaymentMethod, "CASH_ON_DELIVERY", StringComparison.OrdinalIgnoreCase)
                ? OrderPaymentStatuses.CashPending
                : OrderPaymentStatuses.Paid,
            AddressId = address.Id,
            Subtotal = subtotal,
            DeliveryFee = deliveryCheck.DeliveryFee ?? 2.99m,
            Total = subtotal + (deliveryCheck.DeliveryFee ?? 2.99m),
            CreatedAtUtc = DateTime.UtcNow,
            Items = orderItems
        };

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);

        await dbContext.Entry(order).Reference(x => x.Address).LoadAsync(cancellationToken);
        await dbContext.Entry(order).Reference(x => x.AssignedRider).LoadAsync(cancellationToken);
        await dbContext.Entry(order).Collection(x => x.Items).LoadAsync(cancellationToken);

        return GetOrdersQueryHandler.Map(order);
    }
}

public class GetAvailableRiderOrdersQueryHandler(ApiDbContext dbContext) : IRequestHandler<GetAvailableRiderOrdersQuery, IReadOnlyCollection<OrderDto>>
{
    public async Task<IReadOnlyCollection<OrderDto>> Handle(GetAvailableRiderOrdersQuery request, CancellationToken cancellationToken)
    {
        var orders = await dbContext.Orders
            .AsNoTracking()
            .Include(x => x.Address)
            .Include(x => x.AssignedRider)
            .Include(x => x.Items)
            .Where(x => x.Status == OrderStatuses.AwaitingPickup)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return orders.Select(GetOrdersQueryHandler.Map).ToList();
    }
}

public class GetAssignedRiderOrdersQueryHandler(ApiDbContext dbContext) : IRequestHandler<GetAssignedRiderOrdersQuery, IReadOnlyCollection<OrderDto>>
{
    public async Task<IReadOnlyCollection<OrderDto>> Handle(GetAssignedRiderOrdersQuery request, CancellationToken cancellationToken)
    {
        var orders = await dbContext.Orders
            .AsNoTracking()
            .Include(x => x.Address)
            .Include(x => x.AssignedRider)
            .Include(x => x.Items)
            .Where(x => x.AssignedRiderId == request.RiderUserId)
            .OrderByDescending(x => x.CompletedAtUtc ?? x.AcceptedAtUtc ?? x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return orders.Select(GetOrdersQueryHandler.Map).ToList();
    }
}

public class GetRiderOrderByIdQueryHandler(ApiDbContext dbContext) : IRequestHandler<GetRiderOrderByIdQuery, OrderDto?>
{
    public async Task<OrderDto?> Handle(GetRiderOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders
            .AsNoTracking()
            .Include(x => x.Address)
            .Include(x => x.AssignedRider)
            .Include(x => x.Items)
            .SingleOrDefaultAsync(
                x => x.Id == request.OrderId && (x.AssignedRiderId == request.RiderUserId || x.Status == OrderStatuses.AwaitingPickup),
                cancellationToken);

        return order is null ? null : GetOrdersQueryHandler.Map(order);
    }
}

public class AcceptRiderOrderCommandHandler(ApiDbContext dbContext) : IRequestHandler<AcceptRiderOrderCommand, OrderDto>
{
    public async Task<OrderDto> Handle(AcceptRiderOrderCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var updatedCount = await dbContext.Orders
            .Where(x => x.Id == request.OrderId && x.Status == OrderStatuses.AwaitingPickup && x.AssignedRiderId == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.AssignedRiderId, request.RiderUserId)
                .SetProperty(x => x.Status, OrderStatuses.AssignedToRider)
                .SetProperty(x => x.AcceptedAtUtc, now)
                .SetProperty(x => x.UpdatedAtUtc, now), cancellationToken);

        if (updatedCount == 0)
        {
            throw new InvalidOperationException("This order has already been accepted by another rider.");
        }

        return await RiderOrderLoads.LoadAssignedOrderAsync(dbContext, request.RiderUserId, request.OrderId, cancellationToken);
    }
}

public class MarkRiderOrderPickedUpCommandHandler(ApiDbContext dbContext) : IRequestHandler<MarkRiderOrderPickedUpCommand, OrderDto>
{
    public Task<OrderDto> Handle(MarkRiderOrderPickedUpCommand request, CancellationToken cancellationToken) =>
        RiderOrderStateTransitions.UpdateAssignedOrderAsync(
            dbContext,
            request.RiderUserId,
            request.OrderId,
            OrderStatuses.AssignedToRider,
            OrderStatuses.PickedUp,
            (order, now) => order.PickedUpAtUtc = now,
            cancellationToken);
}

public class MarkRiderOrderOutForDeliveryCommandHandler(ApiDbContext dbContext) : IRequestHandler<MarkRiderOrderOutForDeliveryCommand, OrderDto>
{
    public Task<OrderDto> Handle(MarkRiderOrderOutForDeliveryCommand request, CancellationToken cancellationToken) =>
        RiderOrderStateTransitions.UpdateAssignedOrderAsync(
            dbContext,
            request.RiderUserId,
            request.OrderId,
            OrderStatuses.PickedUp,
            OrderStatuses.OutForDelivery,
            (order, now) => order.OutForDeliveryAtUtc = now,
            cancellationToken);
}

public class MarkRiderOrderDeliveredCommandHandler(ApiDbContext dbContext) : IRequestHandler<MarkRiderOrderDeliveredCommand, OrderDto>
{
    public Task<OrderDto> Handle(MarkRiderOrderDeliveredCommand request, CancellationToken cancellationToken) =>
        RiderOrderStateTransitions.UpdateAssignedOrderAsync(
            dbContext,
            request.RiderUserId,
            request.OrderId,
            OrderStatuses.OutForDelivery,
            OrderStatuses.Delivered,
            (order, now) =>
            {
                order.DeliveredAtUtc = now;
            },
            cancellationToken);
}

public class MarkRiderOrderCashCollectedCommandHandler(ApiDbContext dbContext) : IRequestHandler<MarkRiderOrderCashCollectedCommand, OrderDto>
{
    public async Task<OrderDto> Handle(MarkRiderOrderCashCollectedCommand request, CancellationToken cancellationToken)
    {
        var order = await RiderOrderStateTransitions.LoadOwnedOrderForUpdateAsync(dbContext, request.RiderUserId, request.OrderId, cancellationToken);
        if (order.Status != OrderStatuses.Delivered)
        {
            throw new InvalidOperationException("Cash can only be collected after the order is delivered.");
        }

        if (!string.Equals(order.PaymentMethod, "CASH_ON_DELIVERY", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cash collection is only required for cash on delivery orders.");
        }

        if (order.PaymentStatus == OrderPaymentStatuses.CashCollected)
        {
            return GetOrdersQueryHandler.Map(await RiderOrderStateTransitions.LoadOwnedOrderAsync(dbContext, request.RiderUserId, request.OrderId, cancellationToken));
        }

        order.PaymentStatus = OrderPaymentStatuses.CashCollected;
        order.CashCollectedAtUtc = DateTime.UtcNow;
        order.UpdatedAtUtc = order.CashCollectedAtUtc;
        await dbContext.SaveChangesAsync(cancellationToken);

        return GetOrdersQueryHandler.Map(await RiderOrderStateTransitions.LoadOwnedOrderAsync(dbContext, request.RiderUserId, request.OrderId, cancellationToken));
    }
}

public class CompleteRiderOrderCommandHandler(ApiDbContext dbContext) : IRequestHandler<CompleteRiderOrderCommand, OrderDto>
{
    public async Task<OrderDto> Handle(CompleteRiderOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await RiderOrderStateTransitions.LoadOwnedOrderForUpdateAsync(dbContext, request.RiderUserId, request.OrderId, cancellationToken);
        if (order.Status != OrderStatuses.Delivered)
        {
            throw new InvalidOperationException("Only delivered orders can be completed.");
        }

        if (string.Equals(order.PaymentMethod, "CASH_ON_DELIVERY", StringComparison.OrdinalIgnoreCase)
            && order.PaymentStatus != OrderPaymentStatuses.CashCollected)
        {
            throw new InvalidOperationException("Cash on delivery orders must be marked as cash collected before completion.");
        }

        var now = DateTime.UtcNow;
        order.Status = OrderStatuses.Completed;
        order.CompletedAtUtc = now;
        order.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return GetOrdersQueryHandler.Map(await RiderOrderStateTransitions.LoadOwnedOrderAsync(dbContext, request.RiderUserId, request.OrderId, cancellationToken));
    }
}

internal static class RiderOrderStateTransitions
{
    internal static async Task<OrderDto> UpdateAssignedOrderAsync(
        ApiDbContext dbContext,
        int riderUserId,
        int orderId,
        string expectedStatus,
        string nextStatus,
        Action<ApiOrder, DateTime> applyMutation,
        CancellationToken cancellationToken)
    {
        var order = await LoadOwnedOrderForUpdateAsync(dbContext, riderUserId, orderId, cancellationToken);
        if (order.Status != expectedStatus)
        {
            throw new InvalidOperationException($"Order must be in status {expectedStatus} before moving to {nextStatus}.");
        }

        var now = DateTime.UtcNow;
        order.Status = nextStatus;
        order.UpdatedAtUtc = now;
        applyMutation(order, now);
        await dbContext.SaveChangesAsync(cancellationToken);

        return GetOrdersQueryHandler.Map(await LoadOwnedOrderAsync(dbContext, riderUserId, orderId, cancellationToken));
    }

    internal static async Task<ApiOrder> LoadOwnedOrderForUpdateAsync(ApiDbContext dbContext, int riderUserId, int orderId, CancellationToken cancellationToken)
    {
        return await dbContext.Orders
            .Include(x => x.Address)
            .Include(x => x.AssignedRider)
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == orderId && x.AssignedRiderId == riderUserId, cancellationToken)
            ?? throw new InvalidOperationException("Order not found for this rider.");
    }

    internal static async Task<ApiOrder> LoadOwnedOrderAsync(ApiDbContext dbContext, int riderUserId, int orderId, CancellationToken cancellationToken)
    {
        return await dbContext.Orders
            .AsNoTracking()
            .Include(x => x.Address)
            .Include(x => x.AssignedRider)
            .Include(x => x.Items)
            .SingleAsync(x => x.Id == orderId && x.AssignedRiderId == riderUserId, cancellationToken);
    }
}

internal static class RiderOrderLoads
{
    internal static async Task<OrderDto> LoadAssignedOrderAsync(ApiDbContext dbContext, int riderUserId, int orderId, CancellationToken cancellationToken)
    {
        var order = await RiderOrderStateTransitions.LoadOwnedOrderAsync(dbContext, riderUserId, orderId, cancellationToken);
        return GetOrdersQueryHandler.Map(order);
    }
}

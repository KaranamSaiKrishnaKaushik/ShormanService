using System.Globalization;
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
public record GetOrderInsightsQuery(int UserId, string Range) : IRequest<OrderInsightsDto>;

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

public class GetOrderInsightsQueryHandler(ApiDbContext dbContext) : IRequestHandler<GetOrderInsightsQuery, OrderInsightsDto>
{
    private sealed record InsightRow(
        int OrderId,
        DateTime CreatedAtUtc,
        int ProductId,
        string ProductName,
        string? ProductImageUrl,
        string Store,
        string Category,
        int Quantity,
        decimal Spend);

    public async Task<OrderInsightsDto> Handle(GetOrderInsightsQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var periodStart = ResolvePeriodStart(request.Range, now);
        var periodEnd = now;
        var periodDays = Math.Max(1, (int)Math.Ceiling((periodEnd - periodStart).TotalDays));
        var previousPeriodEnd = periodStart;
        var previousPeriodStart = previousPeriodEnd.AddDays(-periodDays);

        var currentOrders = await dbContext.Orders
            .AsNoTracking()
            .Where(x => x.UserId == request.UserId
                && x.Status != OrderStatuses.Cancelled
                && x.CreatedAtUtc >= periodStart
                && x.CreatedAtUtc <= periodEnd)
            .Select(x => new { x.Id, x.Total, x.CreatedAtUtc })
            .ToListAsync(cancellationToken);

        var previousOrders = await dbContext.Orders
            .AsNoTracking()
            .Where(x => x.UserId == request.UserId
                && x.Status != OrderStatuses.Cancelled
                && x.CreatedAtUtc >= previousPeriodStart
                && x.CreatedAtUtc < previousPeriodEnd)
            .Select(x => x.Total)
            .ToListAsync(cancellationToken);

        var rows = await (
            from order in dbContext.Orders.AsNoTracking()
            where order.UserId == request.UserId
                && order.Status != OrderStatuses.Cancelled
                && order.CreatedAtUtc >= periodStart
                && order.CreatedAtUtc <= periodEnd
            join item in dbContext.OrderItems.AsNoTracking() on order.Id equals item.OrderId
            join product in dbContext.Products.AsNoTracking() on item.ProductId equals product.Id into productGroup
            from product in productGroup.DefaultIfEmpty()
            join category in dbContext.Categories.AsNoTracking() on product.CategoryId equals category.Id into categoryGroup
            from category in categoryGroup.DefaultIfEmpty()
            select new InsightRow(
                order.Id,
                order.CreatedAtUtc,
                item.ProductId,
                item.ProductName,
                item.ProductImageUrl,
                string.IsNullOrWhiteSpace(item.SupermarketName) ? "Unknown" : item.SupermarketName!,
                category != null && !string.IsNullOrWhiteSpace(category.Name) ? category.Name : "Uncategorized",
                item.Quantity,
                item.TotalPrice))
            .ToListAsync(cancellationToken);

        var totalSpend = currentOrders.Sum(x => x.Total);
        var totalOrders = currentOrders.Count;
        var averageBasket = totalOrders > 0 ? totalSpend / totalOrders : 0m;

        var previousTotalSpend = previousOrders.Sum();
        var previousTotalOrders = previousOrders.Count;
        var previousAverageBasket = previousTotalOrders > 0 ? previousTotalSpend / previousTotalOrders : 0m;

        var storeSpend = rows
            .GroupBy(x => x.Store, StringComparer.OrdinalIgnoreCase)
            .Select(group => new OrderInsightsStoreSpendDto(group.Key, Math.Round(group.Sum(x => x.Spend), 2)))
            .OrderByDescending(x => x.Spend)
            .ToArray();

        var months = BuildMonthBuckets(periodStart, periodEnd);
        var monthlySpend = months
            .Select(month =>
            {
                var monthStores = rows
                    .Where(x => x.CreatedAtUtc >= month.Start && x.CreatedAtUtc < month.End)
                    .GroupBy(x => x.Store, StringComparer.OrdinalIgnoreCase)
                    .Select(group => new OrderInsightsStoreSpendDto(group.Key, Math.Round(group.Sum(x => x.Spend), 2)))
                    .OrderByDescending(x => x.Spend)
                    .ToArray();

                return new OrderInsightsMonthlyStoreSpendDto(month.Key, month.Label, monthStores);
            })
            .ToArray();

        var topCategories = rows
            .GroupBy(x => x.Category, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var spend = group.Sum(x => x.Spend);
                var orderCount = group.Select(x => x.OrderId).Distinct().Count();
                var segment = ResolveSegment(group.First().Store);
                return new OrderInsightsCategoryDto(group.Key, segment, Math.Round(spend, 2), orderCount);
            })
            .OrderByDescending(x => x.Spend)
            .ToArray();

        var heatmap = rows
            .GroupBy(x => new
            {
                Store = x.Store.Trim(),
                StoreKey = x.Store.Trim().ToLowerInvariant(),
                Category = x.Category.Trim(),
                CategoryKey = x.Category.Trim().ToLowerInvariant()
            })
            .Select(group => new OrderInsightsHeatmapCellDto(
                group.Key.Store,
                group.Key.Category,
                ResolveSegment(group.Key.Store),
                Math.Round(group.Sum(x => x.Spend), 2)))
            .OrderBy(x => x.Store)
            .ThenByDescending(x => x.Spend)
            .ToArray();

        var topReorderedProducts = rows
            .GroupBy(x => new
            {
                x.ProductId,
                ProductName = x.ProductName.Trim(),
                ProductNameKey = x.ProductName.Trim().ToLowerInvariant(),
                x.ProductImageUrl,
                Store = x.Store.Trim(),
                StoreKey = x.Store.Trim().ToLowerInvariant()
            })
            .Select(group => new OrderInsightsReorderedProductDto(
                group.Key.ProductId,
                group.Key.ProductName,
                group.Key.ProductImageUrl,
                group.Key.Store,
                group.Sum(x => x.Quantity),
                group.Select(x => x.OrderId).Distinct().Count(),
                Math.Round(group.Sum(x => x.Spend), 2)))
            .OrderByDescending(x => x.RepeatCount)
            .ThenByDescending(x => x.Quantity)
            .Take(10)
            .ToArray();

        var favoriteStore = storeSpend.FirstOrDefault();
        var favoriteCategory = topCategories.FirstOrDefault();

        var grocerySpend = storeSpend.Where(x => ResolveSegment(x.Store) == "grocery").Sum(x => x.Spend);
        var drugstoreSpend = storeSpend.Where(x => ResolveSegment(x.Store) == "drugstore").Sum(x => x.Spend);

        var favoriteStoreShare = totalSpend > 0m && favoriteStore is not null ? (favoriteStore.Spend / totalSpend) * 100m : 0m;
        var favoriteCategoryShare = totalSpend > 0m && favoriteCategory is not null ? (favoriteCategory.Spend / totalSpend) * 100m : 0m;
        var groceryShare = totalSpend > 0m ? (grocerySpend / totalSpend) * 100m : 0m;
        var drugstoreShare = totalSpend > 0m ? (drugstoreSpend / totalSpend) * 100m : 0m;

        return new OrderInsightsDto(
            Math.Round(totalSpend, 2),
            totalOrders,
            Math.Round(averageBasket, 2),
            Math.Round(previousTotalSpend, 2),
            previousTotalOrders,
            Math.Round(previousAverageBasket, 2),
            favoriteStore?.Store ?? "N/A",
            Math.Round(favoriteStore?.Spend ?? 0m, 2),
            Math.Round(favoriteStoreShare, 2),
            favoriteCategory?.Category ?? "N/A",
            Math.Round(favoriteCategory?.Spend ?? 0m, 2),
            Math.Round(favoriteCategoryShare, 2),
            Math.Round(groceryShare, 2),
            Math.Round(drugstoreShare, 2),
            storeSpend,
            monthlySpend,
            topCategories,
            heatmap,
            topReorderedProducts);
    }

    private static string ResolveSegment(string store)
    {
        var normalized = store.Trim().ToLowerInvariant();
        return normalized is "dm" or "rossmann" ? "drugstore" : "grocery";
    }

    private static DateTime ResolvePeriodStart(string? range, DateTime now)
    {
        var normalized = (range ?? "1y").Trim().ToLowerInvariant();
        return normalized switch
        {
            "30d" => now.AddDays(-30),
            "6m" => now.AddMonths(-6),
            "all" => now.AddYears(-5),
            _ => now.AddYears(-1)
        };
    }

    private static IReadOnlyCollection<(DateTime Start, DateTime End, string Key, string Label)> BuildMonthBuckets(DateTime start, DateTime end)
    {
        var first = new DateTime(start.Year, start.Month, 1);
        var last = new DateTime(end.Year, end.Month, 1);
        var months = new List<(DateTime Start, DateTime End, string Key, string Label)>();

        while (first <= last)
        {
            var next = first.AddMonths(1);
            months.Add((
                first,
                next,
                first.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                first.ToString("MMM", CultureInfo.InvariantCulture)));
            first = next;
        }

        if (months.Count == 1)
        {
            var previous = months[0].Start.AddMonths(-1);
            months.Insert(0, (
                previous,
                months[0].Start,
                previous.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                previous.ToString("MMM", CultureInfo.InvariantCulture)));
        }

        return months;
    }
}

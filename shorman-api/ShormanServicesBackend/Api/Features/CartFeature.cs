using MediatR;
using Microsoft.EntityFrameworkCore;
using ShormanServicesBackend.Api.Contracts;
using ShormanServicesBackend.Api.Persistence;
using ShormanServicesBackend.Api.Persistence.Entities;

namespace ShormanServicesBackend.Api.Features;

public record GetCartQuery(int UserId) : IRequest<CartDto>;
public record AddCartItemCommand(int UserId, AddCartItemRequest Request) : IRequest<CartDto>;
public record UpdateCartItemCommand(int UserId, int ItemId, UpdateCartItemRequest Request) : IRequest<CartDto>;
public record RemoveCartItemCommand(int UserId, int ItemId) : IRequest<CartDto>;
public record ClearCartCommand(int UserId) : IRequest<CartDto>;

public class GetCartQueryHandler(ApiDbContext dbContext) : IRequestHandler<GetCartQuery, CartDto>
{
    public async Task<CartDto> Handle(GetCartQuery request, CancellationToken cancellationToken)
    {
        var cart = await CartFeatureShared.GetOrCreateCartAsync(dbContext, request.UserId, cancellationToken);
        return CartFeatureShared.MapCart(cart);
    }
}

public class AddCartItemCommandHandler(ApiDbContext dbContext) : IRequestHandler<AddCartItemCommand, CartDto>
{
    public async Task<CartDto> Handle(AddCartItemCommand request, CancellationToken cancellationToken)
    {
        if (request.Request.Quantity <= 0)
        {
            throw new InvalidOperationException("Quantity must be greater than zero.");
        }

        var product = await dbContext.Products
            .Include(x => x.Category)
            .Include(x => x.Supermarket)
            .SingleOrDefaultAsync(x => x.Id == request.Request.ProductId && x.IsAvailable, cancellationToken)
            ?? throw new InvalidOperationException("Product not found.");

        var cart = await CartFeatureShared.GetOrCreateCartAsync(dbContext, request.UserId, cancellationToken);
        var item = cart.Items.SingleOrDefault(x => x.ProductId == request.Request.ProductId);

        if (item is null)
        {
            cart.Items.Add(new ApiCartItem
            {
                ProductId = product.Id,
                Quantity = request.Request.Quantity,
                UnitPrice = product.Price,
                Product = product
            });
        }
        else
        {
            item.Quantity += request.Request.Quantity;
            item.UnitPrice = product.Price;
        }

        cart.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return CartFeatureShared.MapCart(cart);
    }
}

public class UpdateCartItemCommandHandler(ApiDbContext dbContext) : IRequestHandler<UpdateCartItemCommand, CartDto>
{
    public async Task<CartDto> Handle(UpdateCartItemCommand request, CancellationToken cancellationToken)
    {
        var cart = await CartFeatureShared.GetOrCreateCartAsync(dbContext, request.UserId, cancellationToken);
        var item = cart.Items.SingleOrDefault(x => x.Id == request.ItemId)
            ?? throw new InvalidOperationException("Cart item not found.");

        if (request.Request.Quantity <= 0)
        {
            dbContext.CartItems.Remove(item);
        }
        else
        {
            item.Quantity = request.Request.Quantity;
            item.UnitPrice = item.Product.Price;
        }

        cart.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        cart = await CartFeatureShared.GetOrCreateCartAsync(dbContext, request.UserId, cancellationToken);
        return CartFeatureShared.MapCart(cart);
    }
}

public class RemoveCartItemCommandHandler(ApiDbContext dbContext) : IRequestHandler<RemoveCartItemCommand, CartDto>
{
    public async Task<CartDto> Handle(RemoveCartItemCommand request, CancellationToken cancellationToken)
    {
        var cart = await CartFeatureShared.GetOrCreateCartAsync(dbContext, request.UserId, cancellationToken);
        var item = cart.Items.SingleOrDefault(x => x.Id == request.ItemId)
            ?? throw new InvalidOperationException("Cart item not found.");

        dbContext.CartItems.Remove(item);
        cart.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        cart = await CartFeatureShared.GetOrCreateCartAsync(dbContext, request.UserId, cancellationToken);
        return CartFeatureShared.MapCart(cart);
    }
}

public class ClearCartCommandHandler(ApiDbContext dbContext) : IRequestHandler<ClearCartCommand, CartDto>
{
    public async Task<CartDto> Handle(ClearCartCommand request, CancellationToken cancellationToken)
    {
        var cart = await CartFeatureShared.GetOrCreateCartAsync(dbContext, request.UserId, cancellationToken);
        if (cart.Items.Count > 0)
        {
            dbContext.CartItems.RemoveRange(cart.Items);
            cart.UpdatedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        cart = await CartFeatureShared.GetOrCreateCartAsync(dbContext, request.UserId, cancellationToken);
        return CartFeatureShared.MapCart(cart);
    }
}

internal static class CartFeatureShared
{
    public static async Task<ApiCart> GetOrCreateCartAsync(ApiDbContext dbContext, int userId, CancellationToken cancellationToken)
    {
        var cart = await LoadCartQuery(dbContext, userId).SingleOrDefaultAsync(cancellationToken);
        if (cart is not null)
        {
            var invalidItems = cart.Items
                .Where(item => item.Product is null || !item.Product.IsAvailable)
                .ToList();

            if (invalidItems.Count > 0)
            {
                dbContext.CartItems.RemoveRange(invalidItems);
                cart.UpdatedAtUtc = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
                return await LoadCartQuery(dbContext, userId).SingleAsync(cancellationToken);
            }

            return cart;
        }

        cart = new ApiCart
        {
            UserId = userId,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Carts.Add(cart);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await LoadCartQuery(dbContext, userId).SingleAsync(cancellationToken);
    }

    public static CartDto MapCart(ApiCart cart)
    {
        var items = cart.Items
            .OrderBy(x => x.Id)
            .Select(item =>
            {
                var product = item.Product;
                var productDto = new ProductDto(
                    product.Id,
                    product.Name,
                    product.Description,
                    product.Price,
                    product.ImageUrl,
                    product.CategoryId,
                    product.Category is null ? null : new CategoryDto(product.Category.Id, product.Category.Name, product.Category.Slug, product.Category.Icon),
                    product.SupermarketId,
                    product.Supermarket is null ? null : new SupermarketDto(product.Supermarket.Id, product.Supermarket.Name, product.Supermarket.Slug, product.Supermarket.LogoUrl, product.Supermarket.Color),
                    product.Unit,
                    product.Stock,
                    product.IsAvailable);

                return new CartItemDto(
                    item.Id,
                    item.ProductId,
                    productDto,
                    item.Quantity,
                    item.UnitPrice,
                    item.UnitPrice * item.Quantity);
            })
            .ToList();

        return new CartDto(
            cart.Id,
            cart.UserId,
            items,
            items.Sum(x => x.LineTotal),
            items.Sum(x => x.Quantity),
            cart.CreatedAtUtc.ToString("O"),
            cart.UpdatedAtUtc?.ToString("O"));
    }

    private static IQueryable<ApiCart> LoadCartQuery(ApiDbContext dbContext, int userId) =>
        dbContext.Carts
            .Include(x => x.Items)
                .ThenInclude(x => x.Product)
                    .ThenInclude(x => x.Category)
            .Include(x => x.Items)
                .ThenInclude(x => x.Product)
                    .ThenInclude(x => x.Supermarket)
            .Where(x => x.UserId == userId);
}
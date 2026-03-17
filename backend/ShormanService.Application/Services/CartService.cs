using Microsoft.Extensions.Logging;
using ShormanService.Application.DTOs;
using ShormanService.Application.Interfaces;
using ShormanService.Domain.Entities;
using ShormanService.Domain.Interfaces;

namespace ShormanService.Application.Services;

public class CartService : ICartService
{
    private readonly ICartRepository _cartRepository;
    private readonly IProductRepository _productRepository;
    private readonly ILogger<CartService> _logger;

    public CartService(ICartRepository cartRepository, IProductRepository productRepository, ILogger<CartService> logger)
    {
        _cartRepository = cartRepository;
        _productRepository = productRepository;
        _logger = logger;
    }

    public async Task<CartDto> GetCartAsync(Guid userId)
    {
        var cart = await _cartRepository.GetByUserIdAsync(userId);
        if (cart == null)
        {
            cart = await _cartRepository.CreateAsync(new Cart { UserId = userId });
        }
        return MapCartToDto(cart);
    }

    public async Task<CartDto> AddItemAsync(Guid userId, AddToCartRequest request)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId)
            ?? throw new KeyNotFoundException("Product not found.");

        if (!product.IsAvailable || product.StockQuantity < request.Quantity)
            throw new InvalidOperationException("Product not available in requested quantity.");

        var cart = await _cartRepository.GetByUserIdAsync(userId)
            ?? await _cartRepository.CreateAsync(new Cart { UserId = userId });

        var existing = cart.Items.FirstOrDefault(i => i.ProductId == request.ProductId);
        if (existing != null)
        {
            existing.Quantity += request.Quantity;
        }
        else
        {
            cart.Items.Add(new CartItem
            {
                CartId = cart.Id,
                ProductId = request.ProductId,
                Quantity = request.Quantity,
                Product = product
            });
        }

        var updated = await _cartRepository.UpdateAsync(cart);
        return MapCartToDto(updated);
    }

    public async Task<CartDto> UpdateItemAsync(Guid userId, Guid cartItemId, UpdateCartItemRequest request)
    {
        var cartItem = await _cartRepository.GetCartItemAsync(cartItemId)
            ?? throw new KeyNotFoundException("Cart item not found.");

        var cart = await _cartRepository.GetByUserIdAsync(userId)
            ?? throw new KeyNotFoundException("Cart not found.");

        if (cart.Items.All(i => i.Id != cartItemId))
            throw new UnauthorizedAccessException("Cart item does not belong to user.");

        if (request.Quantity <= 0)
        {
            await _cartRepository.DeleteCartItemAsync(cartItemId);
        }
        else
        {
            cartItem.Quantity = request.Quantity;
            await _cartRepository.UpdateAsync(cart);
        }

        var updatedCart = await _cartRepository.GetByUserIdAsync(userId) ?? new Cart { UserId = userId };
        return MapCartToDto(updatedCart);
    }

    public async Task<CartDto> RemoveItemAsync(Guid userId, Guid cartItemId)
    {
        var cart = await _cartRepository.GetByUserIdAsync(userId)
            ?? throw new KeyNotFoundException("Cart not found.");

        if (cart.Items.All(i => i.Id != cartItemId))
            throw new UnauthorizedAccessException("Cart item does not belong to user.");

        await _cartRepository.DeleteCartItemAsync(cartItemId);

        var updatedCart = await _cartRepository.GetByUserIdAsync(userId) ?? new Cart { UserId = userId };
        return MapCartToDto(updatedCart);
    }

    public async Task ClearCartAsync(Guid userId)
    {
        var cart = await _cartRepository.GetByUserIdAsync(userId);
        if (cart != null)
            await _cartRepository.ClearCartAsync(cart.Id);
    }

    private static CartDto MapCartToDto(Cart cart) => new CartDto
    {
        Id = cart.Id,
        UserId = cart.UserId,
        Items = cart.Items.Select(i => new CartItemDto
        {
            Id = i.Id,
            ProductId = i.ProductId,
            ProductName = i.Product?.Name ?? string.Empty,
            ProductImageUrl = i.Product?.ImageUrl ?? string.Empty,
            UnitPrice = i.Product?.Price ?? 0,
            Quantity = i.Quantity
        }).ToList()
    };
}

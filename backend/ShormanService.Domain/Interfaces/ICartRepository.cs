using ShormanService.Domain.Entities;

namespace ShormanService.Domain.Interfaces;

public interface ICartRepository
{
    Task<Cart?> GetByUserIdAsync(Guid userId);
    Task<Cart> CreateAsync(Cart cart);
    Task<Cart> UpdateAsync(Cart cart);
    Task<CartItem?> GetCartItemAsync(Guid cartItemId);
    Task DeleteCartItemAsync(Guid cartItemId);
    Task ClearCartAsync(Guid cartId);
}

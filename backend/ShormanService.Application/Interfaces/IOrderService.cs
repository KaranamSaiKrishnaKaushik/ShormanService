using ShormanService.Application.DTOs;

namespace ShormanService.Application.Interfaces;

public interface IOrderService
{
    Task<IEnumerable<OrderDto>> GetUserOrdersAsync(Guid userId);
    Task<OrderDto?> GetByIdAsync(Guid orderId, Guid userId);
    Task<OrderDto> CreateOrderAsync(Guid userId, CreateOrderRequest request);
}

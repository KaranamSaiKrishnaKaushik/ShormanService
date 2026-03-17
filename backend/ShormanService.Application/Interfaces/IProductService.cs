using ShormanService.Application.DTOs;

namespace ShormanService.Application.Interfaces;

public interface IProductService
{
    Task<IEnumerable<ProductListDto>> GetAllAsync(int? supermarketId, int? categoryId, string? search);
    Task<ProductDto?> GetByIdAsync(Guid id);
}

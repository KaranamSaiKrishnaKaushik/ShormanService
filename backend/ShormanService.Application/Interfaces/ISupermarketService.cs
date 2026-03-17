using ShormanService.Application.DTOs;

namespace ShormanService.Application.Interfaces;

public interface ISupermarketService
{
    Task<IEnumerable<SupermarketDto>> GetAllAsync();
}

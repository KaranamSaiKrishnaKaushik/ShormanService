using ShormanService.Domain.Entities;

namespace ShormanService.Domain.Interfaces;

public interface ISupermarketRepository
{
    Task<IEnumerable<Supermarket>> GetAllAsync();
    Task<Supermarket?> GetByIdAsync(int id);
}

using ShormanService.Domain.Entities;

namespace ShormanService.Domain.Interfaces;

public interface IAddressRepository
{
    Task<IEnumerable<Address>> GetByUserIdAsync(Guid userId);
    Task<Address?> GetByIdAsync(Guid id);
    Task<Address> CreateAsync(Address address);
    Task<Address> UpdateAsync(Address address);
    Task DeleteAsync(Guid id);
    Task ClearDefaultAsync(Guid userId);
}

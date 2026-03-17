using ShormanService.Application.DTOs;

namespace ShormanService.Application.Interfaces;

public interface IAddressService
{
    Task<IEnumerable<AddressDto>> GetAddressesAsync(Guid userId);
    Task<AddressDto> CreateAddressAsync(Guid userId, CreateAddressRequest request);
    Task<AddressDto> UpdateAddressAsync(Guid userId, Guid addressId, UpdateAddressRequest request);
    Task DeleteAddressAsync(Guid userId, Guid addressId);
    Task<AddressDto> SetDefaultAsync(Guid userId, Guid addressId);
}

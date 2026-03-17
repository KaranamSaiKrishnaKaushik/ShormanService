using AutoMapper;
using ShormanService.Application.DTOs;
using ShormanService.Application.Interfaces;
using ShormanService.Domain.Entities;
using ShormanService.Domain.Interfaces;

namespace ShormanService.Application.Services;

public class AddressService : IAddressService
{
    private readonly IAddressRepository _addressRepository;
    private readonly IMapper _mapper;

    public AddressService(IAddressRepository addressRepository, IMapper mapper)
    {
        _addressRepository = addressRepository;
        _mapper = mapper;
    }

    public async Task<IEnumerable<AddressDto>> GetAddressesAsync(Guid userId)
    {
        var addresses = await _addressRepository.GetByUserIdAsync(userId);
        return _mapper.Map<IEnumerable<AddressDto>>(addresses);
    }

    public async Task<AddressDto> CreateAddressAsync(Guid userId, CreateAddressRequest request)
    {
        if (request.IsDefault)
            await _addressRepository.ClearDefaultAsync(userId);

        var address = new Address
        {
            UserId = userId,
            Street = request.Street,
            City = request.City,
            PostalCode = request.PostalCode,
            Country = request.Country,
            IsDefault = request.IsDefault
        };

        var created = await _addressRepository.CreateAsync(address);
        return _mapper.Map<AddressDto>(created);
    }

    public async Task<AddressDto> UpdateAddressAsync(Guid userId, Guid addressId, UpdateAddressRequest request)
    {
        var address = await _addressRepository.GetByIdAsync(addressId)
            ?? throw new KeyNotFoundException("Address not found.");

        if (address.UserId != userId)
            throw new UnauthorizedAccessException("Address does not belong to user.");

        address.Street = request.Street;
        address.City = request.City;
        address.PostalCode = request.PostalCode;
        address.Country = request.Country;

        var updated = await _addressRepository.UpdateAsync(address);
        return _mapper.Map<AddressDto>(updated);
    }

    public async Task DeleteAddressAsync(Guid userId, Guid addressId)
    {
        var address = await _addressRepository.GetByIdAsync(addressId)
            ?? throw new KeyNotFoundException("Address not found.");

        if (address.UserId != userId)
            throw new UnauthorizedAccessException("Address does not belong to user.");

        await _addressRepository.DeleteAsync(addressId);
    }

    public async Task<AddressDto> SetDefaultAsync(Guid userId, Guid addressId)
    {
        var address = await _addressRepository.GetByIdAsync(addressId)
            ?? throw new KeyNotFoundException("Address not found.");

        if (address.UserId != userId)
            throw new UnauthorizedAccessException("Address does not belong to user.");

        await _addressRepository.ClearDefaultAsync(userId);
        address.IsDefault = true;
        var updated = await _addressRepository.UpdateAsync(address);
        return _mapper.Map<AddressDto>(updated);
    }
}

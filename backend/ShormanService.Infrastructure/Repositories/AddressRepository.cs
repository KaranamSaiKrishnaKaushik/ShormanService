using Microsoft.EntityFrameworkCore;
using ShormanService.Domain.Entities;
using ShormanService.Domain.Interfaces;
using ShormanService.Infrastructure.Data;

namespace ShormanService.Infrastructure.Repositories;

public class AddressRepository : IAddressRepository
{
    private readonly AppDbContext _context;

    public AddressRepository(AppDbContext context) => _context = context;

    public async Task<IEnumerable<Address>> GetByUserIdAsync(Guid userId) =>
        await _context.Addresses.Where(a => a.UserId == userId).ToListAsync();

    public async Task<Address?> GetByIdAsync(Guid id) =>
        await _context.Addresses.FindAsync(id);

    public async Task<Address> CreateAsync(Address address)
    {
        _context.Addresses.Add(address);
        await _context.SaveChangesAsync();
        return address;
    }

    public async Task<Address> UpdateAsync(Address address)
    {
        _context.Addresses.Update(address);
        await _context.SaveChangesAsync();
        return address;
    }

    public async Task DeleteAsync(Guid id)
    {
        var address = await _context.Addresses.FindAsync(id);
        if (address != null)
        {
            _context.Addresses.Remove(address);
            await _context.SaveChangesAsync();
        }
    }

    public async Task ClearDefaultAsync(Guid userId)
    {
        var defaultAddresses = await _context.Addresses
            .Where(a => a.UserId == userId && a.IsDefault)
            .ToListAsync();

        foreach (var addr in defaultAddresses)
            addr.IsDefault = false;

        await _context.SaveChangesAsync();
    }
}

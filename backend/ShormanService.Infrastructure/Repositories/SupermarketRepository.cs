using Microsoft.EntityFrameworkCore;
using ShormanService.Domain.Entities;
using ShormanService.Domain.Interfaces;
using ShormanService.Infrastructure.Data;

namespace ShormanService.Infrastructure.Repositories;

public class SupermarketRepository : ISupermarketRepository
{
    private readonly AppDbContext _context;

    public SupermarketRepository(AppDbContext context) => _context = context;

    public async Task<IEnumerable<Supermarket>> GetAllAsync() =>
        await _context.Supermarkets.ToListAsync();

    public async Task<Supermarket?> GetByIdAsync(int id) =>
        await _context.Supermarkets.FindAsync(id);
}

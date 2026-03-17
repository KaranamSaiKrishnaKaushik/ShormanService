using Microsoft.EntityFrameworkCore;
using ShormanService.Domain.Entities;
using ShormanService.Domain.Interfaces;
using ShormanService.Infrastructure.Data;

namespace ShormanService.Infrastructure.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly AppDbContext _context;

    public CategoryRepository(AppDbContext context) => _context = context;

    public async Task<IEnumerable<Category>> GetAllAsync() =>
        await _context.Categories.ToListAsync();

    public async Task<Category?> GetByIdAsync(int id) =>
        await _context.Categories.FindAsync(id);
}

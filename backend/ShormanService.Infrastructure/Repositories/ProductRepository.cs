using Microsoft.EntityFrameworkCore;
using ShormanService.Domain.Entities;
using ShormanService.Domain.Interfaces;
using ShormanService.Infrastructure.Data;

namespace ShormanService.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly AppDbContext _context;

    public ProductRepository(AppDbContext context) => _context = context;

    public async Task<IEnumerable<Product>> GetAllAsync(int? supermarketId, int? categoryId, string? search)
    {
        var query = _context.Products
            .Include(p => p.Supermarket)
            .Include(p => p.Category)
            .AsQueryable();

        if (supermarketId.HasValue)
            query = query.Where(p => p.SupermarketId == supermarketId.Value);

        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId.Value);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.Contains(search) || (p.Description != null && p.Description.Contains(search)));

        return await query.ToListAsync();
    }

    public async Task<Product?> GetByIdAsync(Guid id) =>
        await _context.Products
            .Include(p => p.Supermarket)
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id);

    public async Task<Product> CreateAsync(Product product)
    {
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        return product;
    }

    public async Task<Product> UpdateAsync(Product product)
    {
        _context.Products.Update(product);
        await _context.SaveChangesAsync();
        return product;
    }
}

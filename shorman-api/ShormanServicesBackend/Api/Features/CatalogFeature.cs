using MediatR;
using Microsoft.EntityFrameworkCore;
using ShormanServicesBackend.Api.Contracts;
using ShormanServicesBackend.Api.Persistence;

namespace ShormanServicesBackend.Api.Features;

public record GetProductsQuery(string? Search, int? CategoryId, int? SupermarketId) : IRequest<IReadOnlyCollection<ProductDto>>;
public record GetProductByIdQuery(int Id) : IRequest<ProductDto?>;
public record GetCategoriesQuery() : IRequest<IReadOnlyCollection<CategoryDto>>;
public record GetSupermarketsQuery() : IRequest<IReadOnlyCollection<SupermarketDto>>;

public class GetProductsQueryHandler(ApiDbContext dbContext) : IRequestHandler<GetProductsQuery, IReadOnlyCollection<ProductDto>>
{
    public async Task<IReadOnlyCollection<ProductDto>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Products
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Supermarket)
            .Where(x => x.IsAvailable)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(search));
        }

        if (request.CategoryId.HasValue)
        {
            query = query.Where(x => x.CategoryId == request.CategoryId.Value);
        }

        if (request.SupermarketId.HasValue)
        {
            query = query.Where(x => x.SupermarketId == request.SupermarketId.Value);
        }

        var items = await query.OrderBy(x => x.Name).ToListAsync(cancellationToken);
        return items.Select(Map).ToList();
    }

    internal static ProductDto Map(Persistence.Entities.ApiProduct product) =>
        new(
            product.Id,
            product.Name,
            product.Description,
            product.Price,
            product.ImageUrl,
            product.CategoryId,
            new CategoryDto(product.Category.Id, product.Category.Name, product.Category.Slug, product.Category.Icon),
            product.SupermarketId,
            new SupermarketDto(product.Supermarket.Id, product.Supermarket.Name, product.Supermarket.Slug, product.Supermarket.LogoUrl, product.Supermarket.Color),
            product.Unit,
            product.Stock,
            product.IsAvailable);
}

public class GetProductByIdQueryHandler(ApiDbContext dbContext) : IRequestHandler<GetProductByIdQuery, ProductDto?>
{
    public async Task<ProductDto?> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Supermarket)
            .SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        return product is null ? null : GetProductsQueryHandler.Map(product);
    }
}

public class GetCategoriesQueryHandler(ApiDbContext dbContext) : IRequestHandler<GetCategoriesQuery, IReadOnlyCollection<CategoryDto>>
{
    public async Task<IReadOnlyCollection<CategoryDto>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        return await dbContext.Categories
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new CategoryDto(x.Id, x.Name, x.Slug, x.Icon))
            .ToListAsync(cancellationToken);
    }
}

public class GetSupermarketsQueryHandler(ApiDbContext dbContext) : IRequestHandler<GetSupermarketsQuery, IReadOnlyCollection<SupermarketDto>>
{
    public async Task<IReadOnlyCollection<SupermarketDto>> Handle(GetSupermarketsQuery request, CancellationToken cancellationToken)
    {
        return await dbContext.Supermarkets
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new SupermarketDto(x.Id, x.Name, x.Slug, x.LogoUrl, x.Color))
            .ToListAsync(cancellationToken);
    }
}

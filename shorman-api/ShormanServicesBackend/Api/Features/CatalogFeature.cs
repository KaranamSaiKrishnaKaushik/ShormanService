using MediatR;
using Microsoft.EntityFrameworkCore;
using ShormanServicesBackend.Api.Contracts;
using ShormanServicesBackend.Api.Persistence;

namespace ShormanServicesBackend.Api.Features;

public record GetProductsQuery(string? Search, int? CategoryId, int? SupermarketId, IReadOnlyCollection<int>? SupermarketIds, int Page = 1, int PageSize = 30) : IRequest<PagedResultDto<ProductDto>>;
public record GetProductByIdQuery(int Id) : IRequest<ProductDto?>;
public record GetCategoriesQuery() : IRequest<IReadOnlyCollection<CategoryDto>>;
public record GetSupermarketsQuery() : IRequest<IReadOnlyCollection<SupermarketDto>>;
public record GetAdminProductsQuery(string? Search, int? CategoryId, int? SupermarketId, IReadOnlyCollection<int>? SupermarketIds, int Page = 1, int PageSize = 50) : IRequest<PagedResultDto<ProductDto>>;
public record UpdateProductCommand(int Id, UpdateProductRequest Request) : IRequest<ProductDto?>;
public record DeleteProductCommand(int Id) : IRequest<bool>;

public class GetProductsQueryHandler(ApiDbContext dbContext) : IRequestHandler<GetProductsQuery, PagedResultDto<ProductDto>>
{
    public async Task<PagedResultDto<ProductDto>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize switch
        {
            < 1 => 30,
            > 120 => 120,
            _ => request.PageSize
        };

        var query = dbContext.Products
            .AsNoTracking()
            .Where(x => x.IsAvailable)
            .Where(x => x.ImageUrl != null && x.ImageUrl != "")
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
        else if (request.SupermarketIds is { Count: > 0 })
        {
            query = query.Where(x => request.SupermarketIds.Contains(x.SupermarketId));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(x => x.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(product => new ProductDto(
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
                product.IsAvailable))
            .ToListAsync(cancellationToken);

        return new PagedResultDto<ProductDto>(items, totalCount, page, pageSize);
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
            .Where(x => x.Id == request.Id)
            .Select(item => new ProductDto(
                item.Id,
                item.Name,
                item.Description,
                item.Price,
                item.ImageUrl,
                item.CategoryId,
                new CategoryDto(item.Category.Id, item.Category.Name, item.Category.Slug, item.Category.Icon),
                item.SupermarketId,
                new SupermarketDto(item.Supermarket.Id, item.Supermarket.Name, item.Supermarket.Slug, item.Supermarket.LogoUrl, item.Supermarket.Color),
                item.Unit,
                item.Stock,
                item.IsAvailable))
            .SingleOrDefaultAsync(cancellationToken);

        return product;
    }
}

public class GetAdminProductsQueryHandler(ApiDbContext dbContext) : IRequestHandler<GetAdminProductsQuery, PagedResultDto<ProductDto>>
{
    public async Task<PagedResultDto<ProductDto>> Handle(GetAdminProductsQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize switch
        {
            < 1 => 50,
            > 200 => 200,
            _ => request.PageSize
        };

        var query = dbContext.Products
            .AsNoTracking()
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
        else if (request.SupermarketIds is { Count: > 0 })
        {
            query = query.Where(x => request.SupermarketIds.Contains(x.SupermarketId));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(CatalogFeatureMappings.MapProjection())
            .ToListAsync(cancellationToken);

        return new PagedResultDto<ProductDto>(items, totalCount, page, pageSize);
    }
}

public class UpdateProductCommandHandler(ApiDbContext dbContext) : IRequestHandler<UpdateProductCommand, ProductDto?>
{
    public async Task<ProductDto?> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .Include(x => x.Category)
            .Include(x => x.Supermarket)
            .SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (product is null)
        {
            return null;
        }

        var categoryExists = await dbContext.Categories.AnyAsync(x => x.Id == request.Request.CategoryId, cancellationToken);
        var supermarketExists = await dbContext.Supermarkets.AnyAsync(x => x.Id == request.Request.SupermarketId, cancellationToken);

        if (!categoryExists || !supermarketExists)
        {
            throw new InvalidOperationException("Invalid category or supermarket selection.");
        }

        product.Name = request.Request.Name.Trim();
        product.Description = string.IsNullOrWhiteSpace(request.Request.Description) ? null : request.Request.Description.Trim();
        product.Price = request.Request.Price;
        product.ImageUrl = string.IsNullOrWhiteSpace(request.Request.ImageUrl) ? null : request.Request.ImageUrl.Trim();
        product.CategoryId = request.Request.CategoryId;
        product.SupermarketId = request.Request.SupermarketId;
        product.Unit = string.IsNullOrWhiteSpace(request.Request.Unit) ? null : request.Request.Unit.Trim();
        product.Stock = request.Request.Stock;
        product.IsAvailable = request.Request.IsAvailable;

        await dbContext.SaveChangesAsync(cancellationToken);

        return await dbContext.Products
            .AsNoTracking()
            .Where(x => x.Id == request.Id)
            .Select(CatalogFeatureMappings.MapProjection())
            .SingleAsync(cancellationToken);
    }
}

public class DeleteProductCommandHandler(ApiDbContext dbContext) : IRequestHandler<DeleteProductCommand, bool>
{
    public async Task<bool> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (product is null)
        {
            return false;
        }

        var cartItems = await dbContext.CartItems.Where(x => x.ProductId == request.Id).ToListAsync(cancellationToken);
        if (cartItems.Count > 0)
        {
            dbContext.CartItems.RemoveRange(cartItems);
        }

        dbContext.Products.Remove(product);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public class GetCategoriesQueryHandler(ApiDbContext dbContext) : IRequestHandler<GetCategoriesQuery, IReadOnlyCollection<CategoryDto>>
{
    public async Task<IReadOnlyCollection<CategoryDto>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        return await dbContext.Categories
            .AsNoTracking()
            .Where(x => x.Products.Any(product => product.IsAvailable && product.ImageUrl != null && product.ImageUrl != ""))
            .OrderBy(x => x.Name)
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

internal static class CatalogFeatureMappings
{
    internal static System.Linq.Expressions.Expression<Func<Persistence.Entities.ApiProduct, ProductDto>> MapProjection() =>
        item => new ProductDto(
            item.Id,
            item.Name,
            item.Description,
            item.Price,
            item.ImageUrl,
            item.CategoryId,
            new CategoryDto(item.Category.Id, item.Category.Name, item.Category.Slug, item.Category.Icon),
            item.SupermarketId,
            new SupermarketDto(item.Supermarket.Id, item.Supermarket.Name, item.Supermarket.Slug, item.Supermarket.LogoUrl, item.Supermarket.Color),
            item.Unit,
            item.Stock,
            item.IsAvailable);
}

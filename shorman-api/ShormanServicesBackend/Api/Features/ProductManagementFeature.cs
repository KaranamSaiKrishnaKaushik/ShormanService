using System.Globalization;
using ClosedXML.Excel;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShormanServicesBackend.Api.Contracts;
using ShormanServicesBackend.Api.Persistence;
using ShormanServicesBackend.Api.Persistence.Entities;

namespace ShormanServicesBackend.Api.Features;

public record GetManagedProductsQuery(string? Search, int? CategoryId, int? SupermarketId, int Page = 1, int PageSize = 50) : IRequest<PagedResultDto<ProductManagementProductDto>>;
public record GetProductHistoryQuery(string? Search, int? SupermarketId, int Page = 1, int PageSize = 25) : IRequest<PagedResultDto<ProductHistoryDataDto>>;
public record GetProductUploadRunsQuery(string? StoreSlug, int Page = 1, int PageSize = 25) : IRequest<PagedResultDto<ProductUploadRunDto>>;

internal static class ProductManagementConstants
{
    public const string ExcelUploadSource = "excel-upload";
    public const string RunStatusProcessing = "processing";
    public const string RunStatusCompleted = "completed";
    public const string RunStatusFailed = "failed";
    public const string ChangeTypeUpdated = "updated";
    public const string ChangeTypeDeactivated = "deactivated";
}

public sealed class GetManagedProductsQueryHandler(ApiDbContext dbContext) : IRequestHandler<GetManagedProductsQuery, PagedResultDto<ProductManagementProductDto>>
{
    public async Task<PagedResultDto<ProductManagementProductDto>> Handle(GetManagedProductsQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize switch
        {
            < 1 => 50,
            > 200 => 200,
            _ => request.PageSize
        };

        var query = dbContext.Products.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Name.ToLower().Contains(search) || (x.ProductKey != null && x.ProductKey.ToLower().Contains(search)));
        }

        if (request.CategoryId.HasValue)
        {
            query = query.Where(x => x.CategoryId == request.CategoryId.Value);
        }

        if (request.SupermarketId.HasValue)
        {
            query = query.Where(x => x.SupermarketId == request.SupermarketId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.Supermarket.Name)
            .ThenBy(x => x.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(product => new ProductManagementProductDto(
                product.Id,
                product.ProductKey,
                product.Name,
                product.Price,
                product.ImageUrl,
                product.CategoryId,
                new CategoryDto(product.Category.Id, product.Category.Name, product.Category.Slug, product.Category.Icon),
                product.SupermarketId,
                new SupermarketDto(product.Supermarket.Id, product.Supermarket.Name, product.Supermarket.Slug, product.Supermarket.LogoUrl, product.Supermarket.Color),
                product.Unit,
                product.Stock,
                product.IsAvailable,
                product.DataSource,
                product.UpdatedAtUtc.HasValue ? product.UpdatedAtUtc.Value.ToString("O") : null))
            .ToListAsync(cancellationToken);

        return new PagedResultDto<ProductManagementProductDto>(items, totalCount, page, pageSize);
    }
}

public sealed class GetProductHistoryQueryHandler(ApiDbContext dbContext) : IRequestHandler<GetProductHistoryQuery, PagedResultDto<ProductHistoryDataDto>>
{
    public async Task<PagedResultDto<ProductHistoryDataDto>> Handle(GetProductHistoryQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize switch
        {
            < 1 => 25,
            > 200 => 200,
            _ => request.PageSize
        };

        var history = dbContext.ProductHistoryData.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLowerInvariant();
            history = history.Where(x => x.Name.ToLower().Contains(search) || (x.ProductKey != null && x.ProductKey.ToLower().Contains(search)));
        }

        if (request.SupermarketId.HasValue)
        {
            history = history.Where(x => x.SupermarketId == request.SupermarketId.Value);
        }

        var totalCount = await history.CountAsync(cancellationToken);
        var items = await (
                from entry in history
                join category in dbContext.Categories.AsNoTracking() on entry.CategoryId equals category.Id
                join supermarket in dbContext.Supermarkets.AsNoTracking() on entry.SupermarketId equals supermarket.Id
                join user in dbContext.Users.AsNoTracking() on entry.ChangedByUserId equals user.Id into changedByUsers
                from changedBy in changedByUsers.DefaultIfEmpty()
                orderby entry.ChangedAtUtc descending, entry.Id descending
                select new ProductHistoryDataDto(
                    entry.Id,
                    entry.ProductId,
                    entry.ProductKey,
                    entry.Name,
                    entry.Price,
                    entry.ImageUrl,
                    entry.CategoryId,
                    new CategoryDto(category.Id, category.Name, category.Slug, category.Icon),
                    entry.SupermarketId,
                    new SupermarketDto(supermarket.Id, supermarket.Name, supermarket.Slug, supermarket.LogoUrl, supermarket.Color),
                    entry.Unit,
                    entry.Stock,
                    entry.IsAvailable,
                    entry.DataSource,
                    entry.ChangeType,
                    entry.ChangedAtUtc.ToString("O"),
                    changedBy == null ? null : $"{changedBy.FirstName} {changedBy.LastName}".Trim(),
                    entry.ImportRunId))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResultDto<ProductHistoryDataDto>(items, totalCount, page, pageSize);
    }
}

public sealed class GetProductUploadRunsQueryHandler(ApiDbContext dbContext, IOptions<ProductManagementOptions> options) : IRequestHandler<GetProductUploadRunsQuery, PagedResultDto<ProductUploadRunDto>>
{
    private readonly ProductManagementOptions productManagementOptions = options.Value;

    public async Task<PagedResultDto<ProductUploadRunDto>> Handle(GetProductUploadRunsQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize switch
        {
            < 1 => 25,
            > 200 => 200,
            _ => request.PageSize
        };

        var runs = dbContext.ProductUploadRuns.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.StoreSlug))
        {
            var storeSlug = request.StoreSlug.Trim().ToLowerInvariant();
            runs = runs.Where(x => x.StoreSlug.ToLower() == storeSlug);
        }

        var totalCount = await runs.CountAsync(cancellationToken);
        var publicBasePath = NormalizePublicBasePath(productManagementOptions.PublicBasePath);

        var items = await (
                from run in runs
                join user in dbContext.Users.AsNoTracking() on run.UploadedByUserId equals user.Id into runUsers
                from uploadedBy in runUsers.DefaultIfEmpty()
                orderby run.UploadedAtUtc descending, run.Id descending
                select new ProductUploadRunDto(
                    run.Id,
                    run.StoreSlug,
                    run.OriginalFileName,
                    CombinePublicPath(publicBasePath, run.StoredFilePath),
                    run.Status,
                    run.TotalRows,
                    run.InsertedCount,
                    run.UpdatedCount,
                    run.UnchangedCount,
                    run.DeactivatedCount,
                    run.UploadedAtUtc.ToString("O"),
                    run.CompletedAtUtc.HasValue ? run.CompletedAtUtc.Value.ToString("O") : null,
                    uploadedBy == null ? null : $"{uploadedBy.FirstName} {uploadedBy.LastName}".Trim(),
                    run.ErrorMessage))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResultDto<ProductUploadRunDto>(items, totalCount, page, pageSize);
    }

    internal static string NormalizePublicBasePath(string publicBasePath)
    {
        if (string.IsNullOrWhiteSpace(publicBasePath))
        {
            return "/product-management-assets";
        }

        return publicBasePath.StartsWith('/') ? publicBasePath.TrimEnd('/') : "/" + publicBasePath.Trim('/');
    }

    internal static string CombinePublicPath(string publicBasePath, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return publicBasePath;
        }

        return $"{publicBasePath}/{relativePath.Replace('\\', '/').TrimStart('/')}";
    }
}

public interface IProductManagementImportService
{
    Task<ProductUploadRunDto> UploadAsync(int userId, IFormFile file, CancellationToken cancellationToken);
}

public sealed class ProductManagementImportService(ApiDbContext dbContext, IOptions<ProductManagementOptions> options) : IProductManagementImportService
{
    private readonly ProductManagementOptions productManagementOptions = options.Value;

    public async Task<ProductUploadRunDto> UploadAsync(int userId, IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length <= 0)
        {
            throw new InvalidOperationException("The uploaded Excel file is empty.");
        }

        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only .xlsx Excel files are supported.");
        }

        var now = DateTime.UtcNow;
        var storageRoot = ResolveStorageRoot(productManagementOptions.StorageRootPath);
        var importFolder = Path.Combine(storageRoot, "imports", now.ToString("yyyyMMdd"));
        Directory.CreateDirectory(importFolder);

        var safeOriginalName = Path.GetFileName(file.FileName);
        var storedFileName = $"{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}.xlsx";
        var storedAbsolutePath = Path.Combine(importFolder, storedFileName);
        var storedRelativePath = Path.GetRelativePath(storageRoot, storedAbsolutePath).Replace('\\', '/');

        await using (var stream = File.Create(storedAbsolutePath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var run = new ApiProductUploadRun
        {
            StoreSlug = string.Empty,
            OriginalFileName = safeOriginalName,
            StoredFilePath = storedRelativePath,
            UploadedAtUtc = now,
            UploadedByUserId = userId,
            Status = ProductManagementConstants.RunStatusProcessing
        };

        dbContext.ProductUploadRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var rows = await ParseWorkbookAsync(storedAbsolutePath, cancellationToken);
            if (rows.Count == 0)
            {
                throw new InvalidOperationException("The uploaded sheet does not contain any product rows.");
            }

            var storeSlugs = rows.Select(x => x.StoreSlug).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (storeSlugs.Length != 1)
            {
                throw new InvalidOperationException("Each upload must contain rows for exactly one store_slug.");
            }

            run.StoreSlug = storeSlugs[0];
            run.TotalRows = rows.Count;

            var categories = await dbContext.Categories.AsNoTracking().ToDictionaryAsync(x => x.Slug, StringComparer.OrdinalIgnoreCase, cancellationToken);
            var supermarkets = await dbContext.Supermarkets.AsNoTracking().ToDictionaryAsync(x => x.Slug, StringComparer.OrdinalIgnoreCase, cancellationToken);

            if (!supermarkets.TryGetValue(run.StoreSlug, out var supermarket))
            {
                throw new InvalidOperationException($"Unknown store_slug '{run.StoreSlug}'. Add the supermarket first.");
            }

            var missingCategory = rows.Select(x => x.CategorySlug).Distinct(StringComparer.OrdinalIgnoreCase).FirstOrDefault(slug => !categories.ContainsKey(slug));
            if (missingCategory != null)
            {
                throw new InvalidOperationException($"Unknown category_slug '{missingCategory}'.");
            }

            var existingProducts = (await dbContext.Products
                    .Where(x => x.SupermarketId == supermarket.Id && x.ProductKey != null)
                    .OrderByDescending(x => x.UpdatedAtUtc)
                    .ThenByDescending(x => x.Id)
                    .ToListAsync(cancellationToken))
                .GroupBy(x => x.ProductKey!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            var touchedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                if (!touchedKeys.Add(row.ProductKey))
                {
                    throw new InvalidOperationException($"Duplicate product_key '{row.ProductKey}' found in the uploaded sheet.");
                }

                var category = categories[row.CategorySlug];
                if (!existingProducts.TryGetValue(row.ProductKey, out var existingProduct))
                {
                    var created = new ApiProduct
                    {
                        ProductKey = row.ProductKey,
                        Name = row.ProductName,
                        Price = row.Price,
                        ImageUrl = row.ImageUrl,
                        CategoryId = category.Id,
                        SupermarketId = supermarket.Id,
                        Unit = row.Unit,
                        Stock = row.Stock,
                        IsAvailable = row.IsActive,
                        DataSource = ProductManagementConstants.ExcelUploadSource,
                        UpdatedAtUtc = now,
                        LastImportRunId = run.Id
                    };

                    dbContext.Products.Add(created);
                    existingProducts[row.ProductKey] = created;
                    run.InsertedCount++;
                    continue;
                }

                var changed = HasProductChanged(existingProduct, row, category.Id);
                if (changed)
                {
                    dbContext.ProductHistoryData.Add(CreateHistoryEntry(existingProduct, userId, run.Id, ProductManagementConstants.ChangeTypeUpdated, now));
                    existingProduct.Name = row.ProductName;
                    existingProduct.Price = row.Price;
                    existingProduct.ImageUrl = row.ImageUrl;
                    existingProduct.CategoryId = category.Id;
                    existingProduct.Unit = row.Unit;
                    existingProduct.Stock = row.Stock;
                    existingProduct.IsAvailable = row.IsActive;
                    run.UpdatedCount++;
                }
                else
                {
                    run.UnchangedCount++;
                }

                existingProduct.DataSource = ProductManagementConstants.ExcelUploadSource;
                existingProduct.UpdatedAtUtc = now;
                existingProduct.LastImportRunId = run.Id;
            }

            var staleProducts = await dbContext.Products
                .Where(x => x.SupermarketId == supermarket.Id)
                .Where(x => x.DataSource == ProductManagementConstants.ExcelUploadSource)
                .Where(x => x.ProductKey != null && !touchedKeys.Contains(x.ProductKey))
                .Where(x => x.IsAvailable)
                .ToListAsync(cancellationToken);

            foreach (var staleProduct in staleProducts)
            {
                dbContext.ProductHistoryData.Add(CreateHistoryEntry(staleProduct, userId, run.Id, ProductManagementConstants.ChangeTypeDeactivated, now));
                staleProduct.IsAvailable = false;
                staleProduct.UpdatedAtUtc = now;
                staleProduct.LastImportRunId = run.Id;
                run.DeactivatedCount++;
            }

            run.Status = ProductManagementConstants.RunStatusCompleted;
            run.CompletedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            var uploader = await dbContext.Users.AsNoTracking()
                .Where(x => x.Id == userId)
                .Select(x => new { x.FirstName, x.LastName })
                .FirstOrDefaultAsync(cancellationToken);

            return new ProductUploadRunDto(
                run.Id,
                run.StoreSlug,
                run.OriginalFileName,
                GetProductUploadRunsQueryHandler.CombinePublicPath(GetProductUploadRunsQueryHandler.NormalizePublicBasePath(productManagementOptions.PublicBasePath), run.StoredFilePath),
                run.Status,
                run.TotalRows,
                run.InsertedCount,
                run.UpdatedCount,
                run.UnchangedCount,
                run.DeactivatedCount,
                run.UploadedAtUtc.ToString("O"),
                run.CompletedAtUtc?.ToString("O"),
                uploader == null ? null : $"{uploader.FirstName} {uploader.LastName}".Trim(),
                run.ErrorMessage);
        }
        catch (Exception exception)
        {
            run.Status = ProductManagementConstants.RunStatusFailed;
            run.ErrorMessage = exception.Message;
            run.CompletedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private static bool HasProductChanged(ApiProduct existingProduct, ProductImportRow row, int categoryId) =>
        !string.Equals(existingProduct.Name, row.ProductName, StringComparison.Ordinal) ||
        existingProduct.Price != row.Price ||
        !string.Equals(existingProduct.ImageUrl ?? string.Empty, row.ImageUrl ?? string.Empty, StringComparison.Ordinal) ||
        existingProduct.CategoryId != categoryId ||
        !string.Equals(existingProduct.Unit ?? string.Empty, row.Unit ?? string.Empty, StringComparison.Ordinal) ||
        existingProduct.Stock != row.Stock ||
        existingProduct.IsAvailable != row.IsActive;

    private static ApiProductHistoryData CreateHistoryEntry(ApiProduct product, int userId, int runId, string changeType, DateTime changedAtUtc) =>
        new()
        {
            ProductId = product.Id,
            ProductKey = product.ProductKey,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            ImageUrl = product.ImageUrl,
            CategoryId = product.CategoryId,
            SupermarketId = product.SupermarketId,
            Unit = product.Unit,
            Stock = product.Stock,
            IsAvailable = product.IsAvailable,
            DataSource = product.DataSource,
            ChangeType = changeType,
            ChangedAtUtc = changedAtUtc,
            ChangedByUserId = userId,
            ImportRunId = runId
        };

    private async Task<List<ProductImportRow>> ParseWorkbookAsync(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault() ?? throw new InvalidOperationException("The workbook does not contain any worksheets.");
        var headerRow = worksheet.Row(1);
        var lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        if (lastColumn == 0)
        {
            throw new InvalidOperationException("The workbook does not contain a header row.");
        }

        var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var column = 1; column <= lastColumn; column++)
        {
            var header = headerRow.Cell(column).GetString().Trim();
            if (!string.IsNullOrWhiteSpace(header))
            {
                headerMap[header] = column;
            }
        }

        var requiredHeaders = new[] { "product_key", "store_slug", "product_name", "category_slug", "price" };
        var missingHeaders = requiredHeaders.Where(header => !headerMap.ContainsKey(header)).ToArray();
        if (missingHeaders.Length > 0)
        {
            throw new InvalidOperationException($"Missing required header(s): {string.Join(", ", missingHeaders)}.");
        }

        var rows = new List<ProductImportRow>();
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = worksheet.Row(rowNumber);
            var productName = ReadCell(row, headerMap, "product_name");
            var productKey = ReadCell(row, headerMap, "product_key");
            var storeSlug = ReadCell(row, headerMap, "store_slug");
            var categorySlug = ReadCell(row, headerMap, "category_slug");
            var priceText = ReadCell(row, headerMap, "price");
            var imageUrl = EmptyToNull(ReadCell(row, headerMap, "image_url"));
            var unit = EmptyToNull(ReadCell(row, headerMap, "unit"));
            var stockText = EmptyToNull(ReadCell(row, headerMap, "stock"));
            var isActiveText = EmptyToNull(ReadCell(row, headerMap, "is_active"));

            if (string.IsNullOrWhiteSpace(productName) && string.IsNullOrWhiteSpace(productKey) && string.IsNullOrWhiteSpace(priceText))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(productKey) || string.IsNullOrWhiteSpace(storeSlug) || string.IsNullOrWhiteSpace(productName) || string.IsNullOrWhiteSpace(categorySlug) || string.IsNullOrWhiteSpace(priceText))
            {
                throw new InvalidOperationException($"Row {rowNumber} is missing one or more required values.");
            }

            if (!TryParsePrice(priceText, out var price))
            {
                throw new InvalidOperationException($"Row {rowNumber} contains an invalid price '{priceText}'.");
            }

            if (price < 0)
            {
                throw new InvalidOperationException($"Row {rowNumber} contains a negative price.");
            }

            int? stock = null;
            if (!string.IsNullOrWhiteSpace(stockText))
            {
                if (!int.TryParse(stockText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedStock)
                    && !int.TryParse(stockText, NumberStyles.Integer, CultureInfo.CurrentCulture, out parsedStock))
                {
                    throw new InvalidOperationException($"Row {rowNumber} contains an invalid stock value '{stockText}'.");
                }

                stock = parsedStock;
            }

            rows.Add(new ProductImportRow(
                productKey.Trim(),
                storeSlug.Trim().ToLowerInvariant(),
                productName.Trim(),
                categorySlug.Trim().ToLowerInvariant(),
                price,
                imageUrl,
                unit,
                stock,
                ParseIsActive(isActiveText)));
        }

        return rows;
    }

    private static string ReadCell(IXLRow row, IReadOnlyDictionary<string, int> headerMap, string header)
    {
        if (!headerMap.TryGetValue(header, out var column))
        {
            return string.Empty;
        }

        return row.Cell(column).GetFormattedString().Trim();
    }

    private static bool TryParsePrice(string value, out decimal price)
    {
        var sanitized = value
            .Trim()
            .Replace("EUR", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("€", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("\u00A0", string.Empty, StringComparison.Ordinal);

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            price = default;
            return false;
        }

        var hasComma = sanitized.Contains(',');
        var hasDot = sanitized.Contains('.');

        if (hasComma && hasDot)
        {
            // Interpret the right-most separator as decimal separator and remove the other as grouping.
            var commaIndex = sanitized.LastIndexOf(',');
            var dotIndex = sanitized.LastIndexOf('.');
            if (commaIndex > dotIndex)
            {
                var normalized = sanitized.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.');
                return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out price);
            }

            var normalizedDot = sanitized.Replace(",", string.Empty, StringComparison.Ordinal);
            return decimal.TryParse(normalizedDot, NumberStyles.Number, CultureInfo.InvariantCulture, out price);
        }

        if (hasComma)
        {
            // Prefer comma-decimal interpretation for values like 1,99.
            if (decimal.TryParse(sanitized, NumberStyles.Number, CultureInfo.GetCultureInfo("de-DE"), out price))
            {
                return true;
            }

            var normalized = sanitized.Replace(',', '.');
            return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out price);
        }

        return decimal.TryParse(sanitized, NumberStyles.Number, CultureInfo.InvariantCulture, out price)
               || decimal.TryParse(sanitized, NumberStyles.Number, CultureInfo.CurrentCulture, out price);
    }

    private static bool ParseIsActive(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "yes" or "y" or "active" => true,
            "0" or "false" or "no" or "n" or "inactive" => false,
            _ => throw new InvalidOperationException($"Unsupported is_active value '{value}'.")
        };
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private string ResolveStorageRoot(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredPath));
    }

    private sealed record ProductImportRow(string ProductKey, string StoreSlug, string ProductName, string CategorySlug, decimal Price, string? ImageUrl, string? Unit, int? Stock, bool IsActive);
}
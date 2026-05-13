using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShormanServicesBackend.Api.Contracts;
using ShormanServicesBackend.Api.Features;
using ShormanServicesBackend.Api.Security;

namespace ShormanServicesBackend.Controllers;

[ApiController]
[Route("api/product-management")]
[Authorize(Roles = $"{RoleNames.SuperAdmin},{RoleNames.Admin}")]
public class ProductManagementController(IMediator mediator, IProductManagementImportService importService) : ControllerBase
{
    [HttpGet("products")]
    public Task<PagedResultDto<ProductManagementProductDto>> GetProducts(
        [FromQuery] string? search,
        [FromQuery] int? categoryId,
        [FromQuery] int? supermarketId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        mediator.Send(new GetManagedProductsQuery(search, categoryId, supermarketId, page, pageSize), cancellationToken);

    [HttpGet("history")]
    public Task<PagedResultDto<ProductHistoryDataDto>> GetHistory(
        [FromQuery] string? search,
        [FromQuery] int? supermarketId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default) =>
        mediator.Send(new GetProductHistoryQuery(search, supermarketId, page, pageSize), cancellationToken);

    [HttpGet("uploads")]
    public Task<PagedResultDto<ProductUploadRunDto>> GetUploads(
        [FromQuery] string? storeSlug,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default) =>
        mediator.Send(new GetProductUploadRunsQuery(storeSlug, page, pageSize), cancellationToken);

    [HttpPost("upload")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<ProductUploadRunDto>> Upload([FromForm] ProductSheetUploadForm form, CancellationToken cancellationToken)
    {
        if (form.File is null)
        {
            return BadRequest(new { message = "Attach an Excel .xlsx file before uploading." });
        }

        try
        {
            return Ok(await importService.UploadAsync(GetUserId(), form.File, cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

public sealed class ProductSheetUploadForm
{
    public IFormFile? File { get; set; }
}
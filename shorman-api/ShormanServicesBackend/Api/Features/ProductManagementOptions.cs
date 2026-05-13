namespace ShormanServicesBackend.Api.Features;

public sealed class ProductManagementOptions
{
    public const string SectionName = "ProductManagement";

    public string StorageRootPath { get; set; } = "storage/product-management";
    public string PublicBasePath { get; set; } = "/product-management-assets";
}
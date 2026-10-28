namespace ShormanServicesBackend.Api.Invoicing;

public sealed class InvoiceOptions
{
    public const string SectionName = "Invoice";

    /// <summary>Sequence number used for the very first invoice. Later invoices are max(sequence) + 1.</summary>
    public long StartingNumber { get; set; } = 1000;
    public string NumberPrefix { get; set; } = "RE-";
    public int NumberPadding { get; set; } = 6;

    /// <summary>Folder for invoice PDFs. Used by the local file storage; replace the storage implementation to move to Azure Blob.</summary>
    public string StorageRootPath { get; set; } = "storage/invoices";

    /// <summary>"Local" (StorageRootPath) or "AzureBlob".</summary>
    public string StorageProvider { get; set; } = "Local";

    /// <summary>Blob endpoint, e.g. https://&lt;account&gt;.blob.core.windows.net. Uses managed identity (needs "Storage Blob Data Contributor").</summary>
    public string BlobServiceUri { get; set; } = string.Empty;

    /// <summary>Alternative to BlobServiceUri: a full connection string (keep it in Key Vault / secrets).</summary>
    public string BlobConnectionString { get; set; } = string.Empty;

    public string BlobContainerName { get; set; } = "invoices";

    public decimal ItemVatRatePercent { get; set; } = 7m;
    public decimal DeliveryFeeVatRatePercent { get; set; } = 19m;

    public string CompanyName { get; set; } = "Shorman Service";
    public string OwnerLine { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string PostalCodeCity { get; set; } = string.Empty;
    public string Email { get; set; } = "support@shorman.shop";
    public string Website { get; set; } = "www.shorman.shop";

    public string TaxNumber { get; set; } = string.Empty;
    public string VatId { get; set; } = string.Empty;
    public string TaxOffice { get; set; } = string.Empty;

    public string BankName { get; set; } = string.Empty;
    public string Iban { get; set; } = string.Empty;
    public string Bic { get; set; } = string.Empty;
}

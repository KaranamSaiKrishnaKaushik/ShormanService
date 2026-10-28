using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;

namespace ShormanServicesBackend.Api.Invoicing;

/// <summary>Abstraction over where invoice PDFs live. Swap the implementation for Azure Blob Storage later.</summary>
public interface IInvoiceStorage
{
    /// <returns>A storage key (relative path / blob name) to persist alongside the invoice.</returns>
    Task<string> SaveAsync(string fileName, byte[] content, CancellationToken cancellationToken);

    Task<byte[]?> ReadAsync(string storageKey, CancellationToken cancellationToken);
}

public sealed class BlobInvoiceStorage : IInvoiceStorage
{
    private readonly BlobContainerClient container;
    private int ensured;

    public BlobInvoiceStorage(IOptions<InvoiceOptions> options)
    {
        var o = options.Value;
        var service = !string.IsNullOrWhiteSpace(o.BlobConnectionString)
            ? new BlobServiceClient(o.BlobConnectionString)
            : new BlobServiceClient(new Uri(o.BlobServiceUri), new DefaultAzureCredential());
        container = service.GetBlobContainerClient(o.BlobContainerName);
    }

    public async Task<string> SaveAsync(string fileName, byte[] content, CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref ensured, 1) == 0)
        {
            await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        }

        var name = Path.GetFileName(fileName);
        await container.GetBlobClient(name).UploadAsync(new BinaryData(content), overwrite: true, cancellationToken);
        return name;
    }

    public async Task<byte[]?> ReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        var blob = container.GetBlobClient(Path.GetFileName(storageKey));
        if (!await blob.ExistsAsync(cancellationToken))
        {
            return null;
        }

        var result = await blob.DownloadContentAsync(cancellationToken);
        return result.Value.Content.ToArray();
    }
}

public sealed class LocalInvoiceStorage(IOptions<InvoiceOptions> options, IHostEnvironment environment) : IInvoiceStorage
{
    private string RootPath
    {
        get
        {
            var configured = options.Value.StorageRootPath;
            var root = Path.IsPathRooted(configured) ? configured : Path.Combine(environment.ContentRootPath, configured);
            return Path.GetFullPath(root);
        }
    }

    public async Task<string> SaveAsync(string fileName, byte[] content, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(RootPath);
        var safeName = Path.GetFileName(fileName);
        await File.WriteAllBytesAsync(Path.Combine(RootPath, safeName), content, cancellationToken);
        return safeName;
    }

    public async Task<byte[]?> ReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        var path = Path.Combine(RootPath, Path.GetFileName(storageKey));
        return File.Exists(path) ? await File.ReadAllBytesAsync(path, cancellationToken) : null;
    }
}

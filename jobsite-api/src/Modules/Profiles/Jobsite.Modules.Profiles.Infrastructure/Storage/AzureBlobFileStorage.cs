using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Jobsite.Modules.Profiles.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Jobsite.Modules.Profiles.Infrastructure.Storage;

/// <summary>
/// Stores uploaded files in Azure Blob Storage.
/// Container name configurable via <c>App:FileStorage:Azure:ContainerName</c>.
/// Returns relative blob paths (e.g. <c>resumes/{guid}_{filename}</c>).
/// </summary>
public sealed class AzureBlobFileStorage : IFileStorage
{
    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<AzureBlobFileStorage> _logger;

    public AzureBlobFileStorage(IConfiguration configuration, ILogger<AzureBlobFileStorage> logger)
    {
        string connectionString = configuration["App:FileStorage:Azure:ConnectionString"]
            ?? throw new InvalidOperationException("App:FileStorage:Azure:ConnectionString is required");

        string containerName = configuration["App:FileStorage:Azure:ContainerName"] ?? "uploads";

        _containerClient = new BlobContainerClient(connectionString, containerName);
        _logger = logger;
    }

    public async Task<string> UploadAsync(
        Stream file, string fileName, string contentType, CancellationToken ct = default)
    {
        await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);

        string sanitizedFileName = SanitizeFileName(fileName);
        string blobName = $"resumes/{Guid.NewGuid():N}_{sanitizedFileName}";

        BlobClient blobClient = _containerClient.GetBlobClient(blobName);

        BlobUploadOptions options = new()
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        };

        await blobClient.UploadAsync(file, options, ct);

        _logger.LogInformation("Uploaded blob {BlobName} to Azure Blob Storage", blobName);

        return blobName;
    }

    public async Task DeleteAsync(string fileUrl, CancellationToken ct = default)
    {
        BlobClient blobClient = _containerClient.GetBlobClient(fileUrl);
        bool deleted = await blobClient.DeleteIfExistsAsync(cancellationToken: ct);

        if (deleted)
            _logger.LogInformation("Deleted blob {BlobName} from Azure Blob Storage", fileUrl);
    }

    private static string SanitizeFileName(string fileName)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        string sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        return sanitized.Length > 200 ? sanitized[..200] : sanitized;
    }
}

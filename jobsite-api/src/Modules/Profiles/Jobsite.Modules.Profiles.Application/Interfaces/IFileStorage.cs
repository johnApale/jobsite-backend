namespace Jobsite.Modules.Profiles.Application.Interfaces;

/// <summary>
/// Abstraction for file storage (local filesystem, Azure Blob, S3, etc.).
/// </summary>
public interface IFileStorage
{
    /// <summary>Store a file and return the relative URL path.</summary>
    Task<string> UploadAsync(
        Stream file, string fileName, string contentType, CancellationToken ct = default);

    /// <summary>Delete a file by its relative URL path.</summary>
    Task DeleteAsync(string fileUrl, CancellationToken ct = default);
}

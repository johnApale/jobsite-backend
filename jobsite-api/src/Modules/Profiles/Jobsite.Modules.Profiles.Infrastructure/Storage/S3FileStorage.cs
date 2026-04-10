using Amazon.S3;
using Amazon.S3.Model;
using Jobsite.Modules.Profiles.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Jobsite.Modules.Profiles.Infrastructure.Storage;

/// <summary>
/// Stores uploaded files in any S3-compatible object storage (AWS S3, Cloudflare R2, MinIO, etc.).
/// Configured via <c>App:FileStorage:S3:*</c> settings.
/// Returns relative object keys (e.g. <c>resumes/{guid}_{filename}</c>).
/// </summary>
public sealed class S3FileStorage : IFileStorage, IDisposable
{
    private readonly AmazonS3Client _s3Client;
    private readonly string _bucketName;
    private readonly ILogger<S3FileStorage> _logger;

    public S3FileStorage(IConfiguration configuration, ILogger<S3FileStorage> logger)
    {
        string serviceUrl = configuration["App:FileStorage:S3:ServiceUrl"]
            ?? throw new InvalidOperationException("App:FileStorage:S3:ServiceUrl is required");

        string accessKey = configuration["App:FileStorage:S3:AccessKey"]
            ?? throw new InvalidOperationException("App:FileStorage:S3:AccessKey is required");

        string secretKey = configuration["App:FileStorage:S3:SecretKey"]
            ?? throw new InvalidOperationException("App:FileStorage:S3:SecretKey is required");

        _bucketName = configuration["App:FileStorage:S3:BucketName"] ?? "uploads";

        AmazonS3Config s3Config = new()
        {
            ServiceURL = serviceUrl,
            ForcePathStyle = true,
        };

        _s3Client = new AmazonS3Client(accessKey, secretKey, s3Config);
        _logger = logger;
    }

    public async Task<string> UploadAsync(
        Stream file, string fileName, string contentType, CancellationToken ct = default)
    {
        string sanitizedFileName = SanitizeFileName(fileName);
        string objectKey = $"resumes/{Guid.NewGuid():N}_{sanitizedFileName}";

        PutObjectRequest request = new()
        {
            BucketName = _bucketName,
            Key = objectKey,
            InputStream = file,
            ContentType = contentType,
        };

        await _s3Client.PutObjectAsync(request, ct);

        _logger.LogInformation("Uploaded object {ObjectKey} to S3-compatible storage", objectKey);

        return objectKey;
    }

    public async Task DeleteAsync(string fileUrl, CancellationToken ct = default)
    {
        DeleteObjectRequest request = new()
        {
            BucketName = _bucketName,
            Key = fileUrl,
        };

        await _s3Client.DeleteObjectAsync(request, ct);

        _logger.LogInformation("Deleted object {ObjectKey} from S3-compatible storage", fileUrl);
    }

    private static string SanitizeFileName(string fileName)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        string sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        return sanitized.Length > 200 ? sanitized[..200] : sanitized;
    }

    public void Dispose()
    {
        _s3Client.Dispose();
    }
}

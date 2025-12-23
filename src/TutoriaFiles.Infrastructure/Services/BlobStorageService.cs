using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TutoriaFiles.Core.Interfaces;

namespace TutoriaFiles.Infrastructure.Services;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;
    private readonly ILogger<BlobStorageService> _logger;

    public BlobStorageService(IConfiguration configuration, ILogger<BlobStorageService> logger)
    {
        _logger = logger;

        _logger.LogInformation("[BlobStorageService] Initializing...");

        var connectionString = configuration["AzureStorage:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogError("[BlobStorageService] Azure Storage connection string is NOT configured!");
            throw new InvalidOperationException("Azure Storage connection string not configured");
        }

        // Log account name (safe, not the key)
        var accountNameMatch = System.Text.RegularExpressions.Regex.Match(connectionString, @"AccountName=([^;]+)");
        if (accountNameMatch.Success)
        {
            _logger.LogInformation("[BlobStorageService] Using storage account: {AccountName}", accountNameMatch.Groups[1].Value);
        }

        _containerName = configuration["AzureStorage:ContainerName"] ?? "tutoria-files";
        _logger.LogInformation("[BlobStorageService] Container name: {ContainerName}", _containerName);

        try
        {
            _logger.LogInformation("[BlobStorageService] Creating BlobServiceClient...");
            _blobServiceClient = new BlobServiceClient(connectionString);
            _logger.LogInformation("[BlobStorageService] BlobServiceClient created successfully");

            // Don't block on container check - do it lazily or skip in constructor
            // EnsureContainerExistsAsync().Wait(); // This can cause issues!
            _logger.LogInformation("[BlobStorageService] Initialization complete (container check deferred)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BlobStorageService] Failed to initialize: {Message}", ex.Message);
            throw new InvalidOperationException($"Invalid Azure Storage configuration: {ex.Message}", ex);
        }
    }

    private async Task EnsureContainerExistsAsync()
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);

            // Check if container exists
            if (!await containerClient.ExistsAsync())
            {
                // Create container if it doesn't exist
                await _blobServiceClient.CreateBlobContainerAsync(_containerName);
                _logger.LogInformation("Created container: {ContainerName}", _containerName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure container exists: {ContainerName}", _containerName);
            throw;
        }
    }

    public string GenerateBlobPath(int universityId, int courseId, int moduleId, string filename)
    {
        // Generate unique filename to avoid conflicts
        var extension = Path.GetExtension(filename);
        var uniqueFilename = $"{Guid.NewGuid()}{extension}";

        return $"universities/{universityId}/courses/{courseId}/modules/{moduleId}/{uniqueFilename}";
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string blobPath, string contentType)
    {
        try
        {
            // Ensure container exists on first upload
            await EnsureContainerExistsAsync();

            var blobClient = _blobServiceClient.GetBlobContainerClient(_containerName).GetBlobClient(blobPath);

            // Set content type
            var blobHttpHeaders = new BlobHttpHeaders
            {
                ContentType = contentType ?? "application/octet-stream"
            };

            // Upload with proper content settings
            await blobClient.UploadAsync(fileStream, new BlobUploadOptions
            {
                HttpHeaders = blobHttpHeaders
            });

            // Return the blob URL
            return blobClient.Uri.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file to blob storage: {BlobPath}", blobPath);
            throw new InvalidOperationException("Failed to upload file", ex);
        }
    }

    public async Task<bool> DeleteFileAsync(string blobPath)
    {
        try
        {
            var blobClient = _blobServiceClient.GetBlobContainerClient(_containerName).GetBlobClient(blobPath);

            var response = await blobClient.DeleteIfExistsAsync();

            if (!response.Value)
            {
                _logger.LogWarning("Blob not found for deletion: {BlobPath}", blobPath);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete blob: {BlobPath}", blobPath);
            throw new InvalidOperationException("Failed to delete file", ex);
        }
    }

    public string GetDownloadUrl(string blobPath, int expiresInHours = 1)
    {
        try
        {
            var blobClient = _blobServiceClient.GetBlobContainerClient(_containerName).GetBlobClient(blobPath);

            // Check if the blob client can generate SAS URI
            if (blobClient.CanGenerateSasUri)
            {
                // Generate SAS token
                var sasBuilder = new BlobSasBuilder
                {
                    BlobContainerName = _containerName,
                    BlobName = blobPath,
                    Resource = "b",
                    ExpiresOn = DateTimeOffset.UtcNow.AddHours(expiresInHours)
                };

                sasBuilder.SetPermissions(BlobSasPermissions.Read);

                var sasUri = blobClient.GenerateSasUri(sasBuilder);
                return sasUri.ToString();
            }
            else
            {
                // If SAS generation is not possible, return the direct URL
                // (This happens when using connection strings without account key)
                _logger.LogWarning("Cannot generate SAS URL, returning direct blob URL");
                return blobClient.Uri.ToString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate download URL for blob: {BlobPath}", blobPath);
            throw new InvalidOperationException("Failed to generate download URL", ex);
        }
    }

    public async Task<byte[]?> GetFileContentAsync(string blobPath)
    {
        try
        {
            var blobClient = _blobServiceClient.GetBlobContainerClient(_containerName).GetBlobClient(blobPath);

            if (!await blobClient.ExistsAsync())
            {
                _logger.LogWarning("Blob not found: {BlobPath}", blobPath);
                return null;
            }

            var response = await blobClient.DownloadContentAsync();
            return response.Value.Content.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get file content: {BlobPath}", blobPath);
            return null;
        }
    }
}

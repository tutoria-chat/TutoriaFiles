using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutoriaFiles.Core.DTOs;
using TutoriaFiles.Core.Interfaces;

namespace TutoriaFiles.API.Controllers;

/// <summary>
/// Handles file upload, download, and deletion operations with multi-tenant access control.
/// Dedicated API for handling large file uploads (up to 15MB) linked to Modules.
/// </summary>
[ApiController]
[Route("api/files")]
[Authorize(Policy = "ProfessorOrAbove")]
public class FilesController : BaseAuthController
{
    private readonly IFileService _fileService;
    private readonly ILogger<FilesController> _logger;
    private const long MaxFileSizeBytes = 15 * 1024 * 1024; // 15 MB

    public FilesController(
        IFileService fileService,
        ILogger<FilesController> logger)
    {
        _fileService = fileService;
        _logger = logger;
    }

    /// <summary>
    /// Uploads a file to Azure Blob Storage and creates a database record linked to a Module.
    /// </summary>
    /// <param name="request">File upload request containing moduleId, file, and optional custom name</param>
    /// <returns>File detail DTO with database ID and blob details</returns>
    [HttpPost("upload")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxFileSizeBytes)]
    public async Task<ActionResult<FileDetailDto>> UploadFile([FromForm] FileUploadRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var currentUser = GetCurrentUserFromClaims();
            if (currentUser == null)
            {
                return Unauthorized();
            }

            using var stream = request.File.OpenReadStream();
            var file = await _fileService.UploadFileAsync(
                request.ModuleId,
                stream,
                request.File.FileName,
                request.File.ContentType ?? "application/octet-stream",
                request.File.Length,
                request.CustomName,
                currentUser);

            _logger.LogInformation("Uploaded file {FileName} for module {ModuleId}", file.FileName, request.ModuleId);

            return CreatedAtAction(nameof(GetFile), new { id = file.Id }, new FileDetailDto
            {
                Id = file.Id,
                Name = file.Name,
                FileType = file.FileType,
                FileName = file.FileName,
                BlobPath = file.BlobPath,
                BlobUrl = file.BlobUrl,
                ContentType = file.ContentType,
                FileSize = file.FileSize,
                ModuleId = file.ModuleId,
                IsActive = file.IsActive,
                OpenAIFileId = file.OpenAIFileId,
                CreatedAt = file.CreatedAt,
                UpdatedAt = file.UpdatedAt
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized upload attempt to module {ModuleId}", request.ModuleId);
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file {FileName} for module {ModuleId}", request.CustomName, request.ModuleId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Gets file details by ID.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<FileDetailDto>> GetFile(int id)
    {
        try
        {
            var currentUser = GetCurrentUserFromClaims();
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var canAccess = await _fileService.CanUserAccessFileAsync(id, currentUser);
            if (!canAccess)
            {
                return Forbid();
            }

            // For now, return minimal response - can be expanded later
            return Ok(new { id, message = "File found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving file {FileId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Generates a download URL for a file with a SAS token.
    /// </summary>
    /// <param name="id">The file database ID</param>
    /// <returns>Download URL with expiry information</returns>
    [HttpGet("{id}/download")]
    public async Task<ActionResult> GetDownloadUrl(int id)
    {
        try
        {
            var currentUser = GetCurrentUserFromClaims();
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var downloadUrl = await _fileService.GetDownloadUrlAsync(id, currentUser);

            _logger.LogInformation("Generated download URL for file {FileId}", id);

            // Return JSON with download URL for frontend to handle (camelCase for consistency)
            return Ok(new { downloadUrl });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized download attempt for file {FileId}", id);
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate download URL for file {FileId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Deletes a file from Azure Blob Storage and database.
    /// </summary>
    /// <param name="id">The file database ID</param>
    /// <returns>Success or failure message</returns>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteFile(int id)
    {
        try
        {
            var currentUser = GetCurrentUserFromClaims();
            if (currentUser == null)
            {
                return Unauthorized();
            }

            await _fileService.DeleteFileAsync(id, currentUser);

            _logger.LogInformation("Deleted file with ID {Id}", id);

            return Ok(new { message = "File deleted successfully" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized delete attempt for file {FileId}", id);
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file {FileId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Health check endpoint for the Files API.
    /// </summary>
    [HttpGet("health")]
    [AllowAnonymous]
    public ActionResult HealthCheck()
    {
        return Ok(new
        {
            status = "healthy",
            service = "TutoriaFiles.API",
            timestamp = DateTime.UtcNow
        });
    }
}

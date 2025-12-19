using TutoriaFiles.Core.Entities;
using TutoriaFiles.Core.Interfaces;
using TutoriaFiles.Infrastructure.Helpers;
using FileEntity = TutoriaFiles.Core.Entities.File;

namespace TutoriaFiles.Infrastructure.Services;

public class FileService : IFileService
{
    private readonly IFileRepository _fileRepository;
    private readonly IModuleRepository _moduleRepository;
    private readonly IBlobStorageService _blobStorageService;
    private readonly AccessControlHelper _accessControl;

    public FileService(
        IFileRepository fileRepository,
        IModuleRepository moduleRepository,
        IBlobStorageService blobStorageService,
        AccessControlHelper accessControl)
    {
        _fileRepository = fileRepository;
        _moduleRepository = moduleRepository;
        _blobStorageService = blobStorageService;
        _accessControl = accessControl;
    }

    public async Task<List<int>> GetAccessibleModuleIdsAsync(User user)
    {
        if (user.UserType == "super_admin")
        {
            // Super admins can access all modules - would need to query all, but for files API we'll require explicit moduleId
            return new List<int>();
        }

        if (user.UserType == "professor")
        {
            if (user.IsAdmin ?? false)
            {
                // Admin professors can access all modules in their university
                // For now, return empty list - they need to specify moduleId
                return new List<int>();
            }
            else
            {
                // Regular professors can only access modules from assigned courses
                var courseIds = await _accessControl.GetProfessorCourseIdsAsync(user.UserId);
                // For simplicity, we'll check access at upload time
                return new List<int>();
            }
        }

        return new List<int>();
    }

    public async Task<FileEntity> UploadFileAsync(
        int moduleId,
        Stream fileStream,
        string originalFileName,
        string contentType,
        long fileSize,
        string? customName,
        User currentUser)
    {
        // Check if module exists and get with details
        var module = await _moduleRepository.GetWithDetailsAsync(moduleId);
        if (module == null)
        {
            throw new KeyNotFoundException("Module not found");
        }

        // Access control: Check if user can upload to this module
        var canAccess = await CanUserAccessModuleAsync(moduleId, currentUser);
        if (!canAccess)
        {
            throw new UnauthorizedAccessException("You do not have access to upload files to this module");
        }

        // Validate file size (15MB limit)
        if (fileSize > 15 * 1024 * 1024)
        {
            throw new InvalidOperationException("File size exceeds 15MB limit");
        }

        // Sanitize filename
        var sanitizedFilename = FileHelper.SanitizeFilename(originalFileName);
        if (string.IsNullOrWhiteSpace(sanitizedFilename))
        {
            throw new InvalidOperationException("Invalid filename");
        }

        // Sanitize display name
        var sanitizedName = string.IsNullOrWhiteSpace(customName)
            ? sanitizedFilename
            : FileHelper.SanitizeFilename(customName);

        // Generate blob path
        var blobPath = _blobStorageService.GenerateBlobPath(
            module.Course.UniversityId,
            module.CourseId,
            moduleId,
            sanitizedFilename
        );

        // Upload to blob storage
        var blobUrl = await _blobStorageService.UploadFileAsync(
            fileStream,
            blobPath,
            contentType
        );

        // Create file record in database
        var fileEntity = new FileEntity
        {
            Name = sanitizedName,
            FileType = "upload", // Default file type
            FileName = sanitizedName,
            BlobPath = blobPath,
            BlobUrl = blobUrl,
            ContentType = contentType,
            FileSize = fileSize,
            ModuleId = moduleId,
            IsActive = true
        };

        return await _fileRepository.AddAsync(fileEntity);
    }

    public async Task<string> GetDownloadUrlAsync(int id, User currentUser)
    {
        var file = await _fileRepository.GetByIdAsync(id);
        if (file == null)
        {
            throw new KeyNotFoundException("File not found");
        }

        // Access control
        var canAccess = await CanUserAccessFileAsync(id, currentUser);
        if (!canAccess)
        {
            throw new UnauthorizedAccessException("You do not have access to this file");
        }

        // Generate SAS token for download (1 hour expiry)
        return _blobStorageService.GetDownloadUrl(file.BlobPath ?? file.FileName ?? "", expiresInHours: 1);
    }

    public async Task DeleteFileAsync(int id, User currentUser)
    {
        var file = await _fileRepository.GetByIdAsync(id);
        if (file == null)
        {
            throw new KeyNotFoundException("File not found");
        }

        // Access control
        var canAccess = await CanUserAccessFileAsync(id, currentUser);
        if (!canAccess)
        {
            throw new UnauthorizedAccessException("You do not have access to delete this file");
        }

        // Delete from blob storage
        await _blobStorageService.DeleteFileAsync(file.BlobPath ?? file.FileName ?? "");

        // Delete from database
        await _fileRepository.DeleteAsync(file);
    }

    public async Task<bool> CanUserAccessFileAsync(int fileId, User user)
    {
        var file = await _fileRepository.GetByIdAsync(fileId);
        if (file == null)
        {
            return false;
        }

        return await CanUserAccessModuleAsync(file.ModuleId, user);
    }

    private async Task<bool> CanUserAccessModuleAsync(int moduleId, User user)
    {
        // Super admins can access everything
        if (user.UserType == "super_admin")
        {
            return true;
        }

        // Get module's university
        var moduleUniversityId = await _accessControl.GetModuleUniversityIdAsync(moduleId);
        if (moduleUniversityId == null)
        {
            return false;
        }

        // Admin professors can access all modules in their university
        if (user.UserType == "professor" && (user.IsAdmin ?? false))
        {
            return user.UniversityId == moduleUniversityId;
        }

        // Regular professors can only access modules from assigned courses
        if (user.UserType == "professor")
        {
            var professorCourseIds = await _accessControl.GetProfessorCourseIdsAsync(user.UserId);
            var module = await _moduleRepository.GetByIdAsync(moduleId);
            return module != null && professorCourseIds.Contains(module.CourseId);
        }

        return false;
    }
}

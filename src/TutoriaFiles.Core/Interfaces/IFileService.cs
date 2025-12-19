using TutoriaFiles.Core.Entities;
using FileEntity = TutoriaFiles.Core.Entities.File;

namespace TutoriaFiles.Core.Interfaces;

public interface IFileService
{
    Task<FileEntity> UploadFileAsync(
        int moduleId,
        Stream fileStream,
        string originalFileName,
        string contentType,
        long fileSize,
        string? customName,
        User currentUser);

    Task<string> GetDownloadUrlAsync(int id, User currentUser);

    Task DeleteFileAsync(int id, User currentUser);

    Task<bool> CanUserAccessFileAsync(int fileId, User user);

    Task<List<int>> GetAccessibleModuleIdsAsync(User user);
}

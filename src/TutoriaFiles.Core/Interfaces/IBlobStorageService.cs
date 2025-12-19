namespace TutoriaFiles.Core.Interfaces;

public interface IBlobStorageService
{
    string GenerateBlobPath(int universityId, int courseId, int moduleId, string filename);
    Task<string> UploadFileAsync(Stream fileStream, string blobPath, string contentType);
    Task<bool> DeleteFileAsync(string blobPath);
    string GetDownloadUrl(string blobPath, int expiresInHours = 1);
    Task<byte[]?> GetFileContentAsync(string blobPath);
}

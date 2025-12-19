using FileEntity = TutoriaFiles.Core.Entities.File;

namespace TutoriaFiles.Core.Interfaces;

public interface IFileRepository
{
    Task<FileEntity?> GetByIdAsync(int id);
    Task<FileEntity?> GetWithModuleAsync(int id);
    Task<FileEntity> AddAsync(FileEntity file);
    Task UpdateAsync(FileEntity file);
    Task DeleteAsync(FileEntity file);
}

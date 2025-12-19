using TutoriaFiles.Core.Entities;

namespace TutoriaFiles.Core.Interfaces;

public interface IModuleRepository
{
    Task<Module?> GetByIdAsync(int id);
    Task<Module?> GetWithDetailsAsync(int id);
}

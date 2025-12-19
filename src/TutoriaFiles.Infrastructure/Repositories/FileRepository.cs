using Microsoft.EntityFrameworkCore;
using TutoriaFiles.Core.Interfaces;
using TutoriaFiles.Infrastructure.Data;
using FileEntity = TutoriaFiles.Core.Entities.File;

namespace TutoriaFiles.Infrastructure.Repositories;

public class FileRepository : IFileRepository
{
    private readonly TutoriaDbContext _context;

    public FileRepository(TutoriaDbContext context)
    {
        _context = context;
    }

    public async Task<FileEntity?> GetByIdAsync(int id)
    {
        return await _context.Files.FindAsync(id);
    }

    public async Task<FileEntity?> GetWithModuleAsync(int id)
    {
        return await _context.Files
            .Include(f => f.Module)
                .ThenInclude(m => m.Course)
            .FirstOrDefaultAsync(f => f.Id == id);
    }

    public async Task<FileEntity> AddAsync(FileEntity file)
    {
        file.CreatedAt = DateTime.UtcNow;
        file.UpdatedAt = DateTime.UtcNow;

        _context.Files.Add(file);
        await _context.SaveChangesAsync();
        return file;
    }

    public async Task UpdateAsync(FileEntity file)
    {
        file.UpdatedAt = DateTime.UtcNow;
        _context.Files.Update(file);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(FileEntity file)
    {
        _context.Files.Remove(file);
        await _context.SaveChangesAsync();
    }
}

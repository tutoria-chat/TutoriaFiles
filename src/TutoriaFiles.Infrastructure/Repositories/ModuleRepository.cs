using Microsoft.EntityFrameworkCore;
using TutoriaFiles.Core.Entities;
using TutoriaFiles.Core.Interfaces;
using TutoriaFiles.Infrastructure.Data;

namespace TutoriaFiles.Infrastructure.Repositories;

public class ModuleRepository : IModuleRepository
{
    private readonly TutoriaDbContext _context;

    public ModuleRepository(TutoriaDbContext context)
    {
        _context = context;
    }

    public async Task<Module?> GetByIdAsync(int id)
    {
        return await _context.Modules.FindAsync(id);
    }

    public async Task<Module?> GetWithDetailsAsync(int id)
    {
        return await _context.Modules
            .Include(m => m.Course)
            .FirstOrDefaultAsync(m => m.Id == id);
    }
}

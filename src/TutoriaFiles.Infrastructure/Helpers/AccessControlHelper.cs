using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TutoriaFiles.Infrastructure.Data;

namespace TutoriaFiles.Infrastructure.Helpers;

/// <summary>
/// Helper methods for access control checks across the application.
/// Implements multi-tenant security rules for professors and admin professors.
/// </summary>
public class AccessControlHelper
{
    private readonly TutoriaDbContext _context;
    private readonly ILogger<AccessControlHelper> _logger;
    private const int MaxCourseAssignments = 1000; // Reasonable limit for professor course assignments

    public AccessControlHelper(TutoriaDbContext context, ILogger<AccessControlHelper> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Gets all course IDs assigned to a professor (limited to prevent unbounded queries).
    /// </summary>
    public async Task<List<int>> GetProfessorCourseIdsAsync(int professorId)
    {
        var courseIds = await _context.Set<Core.Entities.ProfessorCourse>()
            .Where(pc => pc.ProfessorId == professorId)
            .Select(pc => pc.CourseId)
            .Take(MaxCourseAssignments)
            .ToListAsync();

        if (courseIds.Count == MaxCourseAssignments)
        {
            _logger.LogWarning(
                "Professor {ProfessorId} has reached the maximum course assignment limit of {MaxLimit}",
                professorId,
                MaxCourseAssignments);
        }

        return courseIds;
    }

    /// <summary>
    /// Gets the university ID for a module.
    /// </summary>
    public async Task<int?> GetModuleUniversityIdAsync(int moduleId)
    {
        var module = await _context.Modules
            .Include(m => m.Course)
            .FirstOrDefaultAsync(m => m.Id == moduleId);

        return module?.Course?.UniversityId;
    }
}

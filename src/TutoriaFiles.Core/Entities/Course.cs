namespace TutoriaFiles.Core.Entities;

/// <summary>
/// Minimal Course entity for access control.
/// Full Course entity exists in TutoriaApi.
/// </summary>
public class Course : BaseEntity
{
    public required string Name { get; set; }
    public int UniversityId { get; set; }

    // Navigation properties
    public ICollection<Module> Modules { get; set; } = new List<Module>();
}

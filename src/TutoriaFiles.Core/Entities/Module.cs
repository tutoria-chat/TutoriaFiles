namespace TutoriaFiles.Core.Entities;

/// <summary>
/// Minimal Module entity for file association.
/// Full Module entity exists in TutoriaApi.
/// </summary>
public class Module : BaseEntity
{
    public required string Name { get; set; }
    public int CourseId { get; set; }

    // Navigation properties
    public Course Course { get; set; } = null!;
    public ICollection<File> Files { get; set; } = new List<File>();
}

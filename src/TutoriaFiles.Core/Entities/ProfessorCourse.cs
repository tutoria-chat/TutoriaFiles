namespace TutoriaFiles.Core.Entities;

/// <summary>
/// Many-to-many relationship between professors and courses.
/// </summary>
public class ProfessorCourse
{
    public int ProfessorId { get; set; }
    public int CourseId { get; set; }
}

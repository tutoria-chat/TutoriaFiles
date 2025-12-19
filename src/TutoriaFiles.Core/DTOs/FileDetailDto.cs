namespace TutoriaFiles.Core.DTOs;

public class FileDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string? BlobPath { get; set; }
    public string? BlobUrl { get; set; }
    public string? BlobContainer { get; set; }
    public string? ContentType { get; set; }
    public long? FileSize { get; set; }
    public int ModuleId { get; set; }
    public string? ModuleName { get; set; }
    public int? CourseId { get; set; }
    public string? CourseName { get; set; }
    public int? UniversityId { get; set; }
    public string? UniversityName { get; set; }
    public bool IsActive { get; set; }
    public string? OpenAIFileId { get; set; }
    public string? AnthropicFileId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

namespace TutoriaFiles.Core.Entities;

public class File : BaseEntity
{
    public required string Name { get; set; }
    public required string FileType { get; set; }
    public string? FileName { get; set; }
    public string? BlobUrl { get; set; }
    public string? BlobContainer { get; set; }
    public string? BlobPath { get; set; }
    public long? FileSize { get; set; }
    public string? ContentType { get; set; }
    public int ModuleId { get; set; }
    public bool IsActive { get; set; } = true;
    public string? OpenAIFileId { get; set; }
    public string? AnthropicFileId { get; set; }

    // Navigation properties
    public Module Module { get; set; } = null!;
}

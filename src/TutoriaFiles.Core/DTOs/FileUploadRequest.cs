using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace TutoriaFiles.Core.DTOs;

public class FileUploadRequest
{
    [Required(ErrorMessage = "Module ID is required")]
    public int ModuleId { get; set; }

    [Required(ErrorMessage = "File is required")]
    public IFormFile File { get; set; } = null!;

    [MaxLength(255, ErrorMessage = "Custom name cannot exceed 255 characters")]
    public string? CustomName { get; set; }
}

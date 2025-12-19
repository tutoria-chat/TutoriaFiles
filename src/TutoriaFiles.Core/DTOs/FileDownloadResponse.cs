namespace TutoriaFiles.Core.DTOs;

public class FileDownloadResponse
{
    public string DownloadUrl { get; set; } = string.Empty;
    public int ExpiresInHours { get; set; }
}

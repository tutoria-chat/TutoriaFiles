using System.Text.RegularExpressions;

namespace TutoriaFiles.Infrastructure.Helpers;

/// <summary>
/// Helper methods for file operations and filename handling.
/// </summary>
public static class FileHelper
{
    /// <summary>
    /// Sanitizes a filename by removing or replacing unsafe characters.
    /// Handles spaces, special characters, and ensures safe filesystem usage.
    /// </summary>
    /// <param name="filename">The original filename to sanitize</param>
    /// <returns>A sanitized filename safe for filesystem usage</returns>
    public static string SanitizeFilename(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            return string.Empty;
        }

        // Get file extension separately
        var extension = Path.GetExtension(filename);
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(filename);

        // Replace spaces with underscores
        nameWithoutExtension = nameWithoutExtension.Replace(" ", "_");

        // Remove invalid characters (keeping only alphanumeric, underscore, hyphen, and dots)
        nameWithoutExtension = Regex.Replace(nameWithoutExtension, @"[^a-zA-Z0-9_\-\.]", "");

        // Ensure the filename isn't too long (max 255 characters total including extension)
        var maxLength = 255 - extension.Length;
        if (nameWithoutExtension.Length > maxLength)
        {
            nameWithoutExtension = nameWithoutExtension.Substring(0, maxLength);
        }

        // Rebuild filename with sanitized name and original extension
        return nameWithoutExtension + extension;
    }

    /// <summary>
    /// Validates if a file type is allowed for upload.
    /// </summary>
    /// <param name="contentType">The MIME type of the file</param>
    /// <returns>True if the file type is allowed</returns>
    public static bool IsAllowedFileType(string contentType)
    {
        var allowedTypes = new[]
        {
            "application/pdf",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.ms-powerpoint",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            "text/plain",
            "text/markdown",
            "application/vnd.ms-excel",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "image/jpeg",
            "image/png",
            "image/gif",
            "video/mp4",
            "video/mpeg",
            "audio/mpeg",
            "audio/wav"
        };

        return allowedTypes.Contains(contentType?.ToLower());
    }

    /// <summary>
    /// Gets the file extension from a filename.
    /// </summary>
    /// <param name="filename">The filename</param>
    /// <returns>The file extension in lowercase, or "unknown" if not found</returns>
    public static string GetFileExtension(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            return "unknown";
        }

        var extension = Path.GetExtension(filename);
        return string.IsNullOrWhiteSpace(extension)
            ? "unknown"
            : extension.TrimStart('.').ToLower();
    }
}

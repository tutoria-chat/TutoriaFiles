namespace TutoriaFiles.Core.Entities;

/// <summary>
/// Minimal User entity for access control.
/// Used to reconstruct user from JWT claims.
/// </summary>
public class User
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string UserType { get; set; } = string.Empty; // "professor", "super_admin"
    public int? UniversityId { get; set; }
    public bool? IsAdmin { get; set; }
}

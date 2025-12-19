using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TutoriaFiles.Core.Entities;

namespace TutoriaFiles.API.Controllers;

/// <summary>
/// Base controller providing common authentication and claims parsing functionality.
/// All authenticated API controllers should inherit from this class to access current user information.
/// </summary>
public abstract class BaseAuthController : ControllerBase
{
    /// <summary>
    /// Extracts the current user ID from JWT claims.
    /// </summary>
    /// <returns>User ID if found, otherwise 0</returns>
    protected int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : 0;
    }

    /// <summary>
    /// Constructs a User entity from JWT claims.
    /// This is used by service layers for access control and filtering.
    /// </summary>
    /// <returns>User entity with claims data, or null if user ID is invalid</returns>
    protected User? GetCurrentUserFromClaims()
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return null;

        return new User
        {
            UserId = userId,
            Username = User.Identity?.Name ?? "unknown",
            Email = User.FindFirst(ClaimTypes.Email)?.Value ?? "unknown@example.com",
            UserType = User.FindFirst(ClaimTypes.Role)?.Value ?? "professor", // Use standard ClaimTypes.Role
            UniversityId = int.TryParse(User.FindFirst("UniversityId")?.Value, out var uniId) ? uniId : null,
            IsAdmin = bool.TryParse(User.FindFirst("isAdmin")?.Value, out var isAdmin) && isAdmin // JWT uses lowercase "isAdmin"
        };
    }

    /// <summary>
    /// Gets the current user's type from JWT claims.
    /// </summary>
    /// <returns>User type (super_admin, professor, student) or "unknown" if not found</returns>
    protected string GetCurrentUserType()
    {
        return User.FindFirst(ClaimTypes.Role)?.Value ?? "unknown";
    }

    /// <summary>
    /// Checks if the current user is a super admin.
    /// </summary>
    protected bool IsSuperAdmin()
    {
        return User.FindFirst(ClaimTypes.Role)?.Value == "super_admin";
    }

    /// <summary>
    /// Checks if the current user is an admin professor.
    /// </summary>
    protected bool IsAdminProfessor()
    {
        return User.FindFirst(ClaimTypes.Role)?.Value == "professor" &&
               bool.TryParse(User.FindFirst("isAdmin")?.Value, out var isAdmin) && isAdmin;
    }

    /// <summary>
    /// Checks if the current user has admin privileges (SuperAdmin or AdminProfessor).
    /// </summary>
    protected bool HasAdminPrivileges()
    {
        return IsSuperAdmin() || IsAdminProfessor();
    }
}

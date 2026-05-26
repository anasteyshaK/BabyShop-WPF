namespace BabyShop.Models;

public sealed class AppUser
{
    public int UserId { get; init; }
    public required string Username { get; init; }
    public required string RoleName { get; init; }
    public bool IsActive { get; init; } = true;
    public IReadOnlyCollection<string> PermissionCodes { get; init; } = Array.Empty<string>();

    public bool IsAdmin => RoleName.Equals("admin", StringComparison.OrdinalIgnoreCase);

    public bool HasPermission(string permissionCode)
    {
        return PermissionCodes.Contains(permissionCode, StringComparer.OrdinalIgnoreCase);
    }

    public bool HasAnyPermission(params string[] permissionCodes)
    {
        return permissionCodes.Any(HasPermission);
    }
}

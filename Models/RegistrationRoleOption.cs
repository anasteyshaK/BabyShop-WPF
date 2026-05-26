using BabyShop.Localization;

namespace BabyShop.Models;

public sealed class RegistrationRoleOption
{
    public required string RoleName { get; init; }
    public required string RoleDescription { get; init; }
    public required string PermissionSummary { get; init; }

    public string LocalizedRoleDescription
    {
        get
        {
            var key = $"{RoleName} {RoleDescription}".ToLowerInvariant();

            var isAdministrator = key.Contains("admin");
            var isRegularUser = key.Contains("regular") || key.Contains("limited") || key.Contains("user");

            if (LanguageManager.CurrentLanguage == AppLanguage.Russian)
            {
                if (isAdministrator)
                {
                    return "Администратор с полным доступом";
                }

                if (isRegularUser)
                {
                    return "Обычный пользователь с ограниченным доступом";
                }
            }
            else
            {
                if (isAdministrator)
                {
                    return "Administrator with full access";
                }

                if (isRegularUser)
                {
                    return "Regular user with limited access";
                }
            }

            return RoleDescription;
        }
    }

    public override string ToString()
    {
        return LocalizedRoleDescription;
    }
}

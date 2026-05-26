using System.Data;
using BabyShop.Infrastructure;
using BabyShop.Models;
using MySql.Data.MySqlClient;

namespace BabyShop.Repositories;

public sealed class AuthRepository
{
    private readonly DbHelper _dbHelper;

    public AuthRepository(DbHelper dbHelper)
    {
        _dbHelper = dbHelper;
    }

    public async Task<AppUser?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand("LoginUser", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.AddWithValue("@p_username", username.Trim());
        command.Parameters.AddWithValue("@p_password", password);

        try
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("The login procedure did not return a result.");
            }

            var success = Convert.ToInt32(reader["success"]) == 1;
            var message = Convert.ToString(reader["message"]) ?? string.Empty;

            if (!success)
            {
                await reader.CloseAsync();

                if (IsInvalidCredentialsMessage(message))
                {
                    var compatibilityUser = await TryAuthenticateCompatibilityAsync(
                        connection,
                        username,
                        password,
                        cancellationToken);

                    if (compatibilityUser is not null)
                    {
                        return compatibilityUser;
                    }
                }

                throw new InvalidOperationException(message);
            }

            var userId = reader["user_id"] is DBNull ? 0 : Convert.ToInt32(reader["user_id"]);
            var normalizedUsername = Convert.ToString(reader["username"]) ?? string.Empty;
            var roleName = Convert.ToString(reader["role_name"]) ?? string.Empty;

            await reader.CloseAsync();
            return await BuildAuthenticatedUserAsync(connection, userId, normalizedUsername, roleName, cancellationToken);
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException($"Could not authorize the user. {exception.Message}", exception);
        }
    }

    public async Task RegisterUserAsync(
        string username,
        string password,
        string roleName,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand("RegisterUser", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.AddWithValue("@p_username", username.Trim());
        command.Parameters.AddWithValue("@p_password", password);
        command.Parameters.AddWithValue("@p_role_name", roleName.Trim());

        try
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("The registration procedure did not return a result.");
            }

            var success = Convert.ToInt32(reader["success"]) == 1;
            var message = Convert.ToString(reader["message"]) ?? string.Empty;
            if (!success)
            {
                throw new InvalidOperationException(message);
            }
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException($"Could not register the user. {exception.Message}", exception);
        }
    }

    public async Task<IReadOnlyList<RegistrationRoleOption>> GetRegistrationRolesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand(
            """
            SELECT
                ur.role_name,
                ur.role_description,
                COALESCE(GROUP_CONCAT(rp.permission_code ORDER BY rp.permission_code SEPARATOR ', '), '') AS permission_summary
            FROM user_role ur
            LEFT JOIN role_permission rp ON rp.role_name = ur.role_name
            GROUP BY ur.role_name, ur.role_description
            ORDER BY ur.role_name;
            """,
            connection);

        var roles = new List<RegistrationRoleOption>();

        try
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                roles.Add(new RegistrationRoleOption
                {
                    RoleName = Convert.ToString(reader["role_name"]) ?? string.Empty,
                    RoleDescription = Convert.ToString(reader["role_description"]) ?? string.Empty,
                    PermissionSummary = Convert.ToString(reader["permission_summary"]) ?? string.Empty
                });
            }
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException($"Could not load registration roles. {exception.Message}", exception);
        }

        return roles;
    }

    private static async Task<IReadOnlyCollection<string>> GetUserPermissionCodesAsync(
        MySqlConnection connection,
        int userId,
        CancellationToken cancellationToken)
    {
        using var command = new MySqlCommand("GetUserPermissions", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("@p_user_id", userId);

        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var permissionCode = Convert.ToString(reader["permission_code"])?.Trim();
            if (!string.IsNullOrWhiteSpace(permissionCode))
            {
                permissions.Add(permissionCode);
            }
        }

        return permissions.ToArray();
    }

    private static bool IsInvalidCredentialsMessage(string message)
    {
        return message.Contains("Invalid username or password", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<AppUser> BuildAuthenticatedUserAsync(
        MySqlConnection connection,
        int userId,
        string username,
        string roleName,
        CancellationToken cancellationToken)
    {
        var permissions = userId > 0
            ? await GetUserPermissionCodesAsync(connection, userId, cancellationToken)
            : Array.Empty<string>();

        return new AppUser
        {
            UserId = userId,
            Username = username,
            RoleName = roleName,
            IsActive = true,
            PermissionCodes = permissions
        };
    }

    private static async Task<AppUser?> TryAuthenticateCompatibilityAsync(
        MySqlConnection connection,
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        using var command = new MySqlCommand(
            """
            SELECT
                user_id,
                username,
                role_name,
                is_active,
                password_hash = @password AS uses_plain_password
            FROM app_user
            WHERE username = @username
              AND (password_hash = SHA2(@password, 256) OR password_hash = @password)
            LIMIT 1;
            """,
            connection);

        command.Parameters.AddWithValue("@username", username.Trim());
        command.Parameters.AddWithValue("@password", password);

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var userId = Convert.ToInt32(reader["user_id"]);
        var normalizedUsername = Convert.ToString(reader["username"]) ?? string.Empty;
        var roleName = Convert.ToString(reader["role_name"]) ?? string.Empty;
        var isActive = reader["is_active"] is not DBNull && Convert.ToInt32(reader["is_active"]) == 1;
        var usesPlainPassword = reader["uses_plain_password"] is not DBNull && Convert.ToInt32(reader["uses_plain_password"]) == 1;

        await reader.CloseAsync();

        if (!isActive)
        {
            throw new InvalidOperationException("ERROR: User account is inactive.");
        }

        if (usesPlainPassword)
        {
            using var normalizePasswordCommand = new MySqlCommand(
                """
                UPDATE app_user
                SET password_hash = SHA2(@password, 256)
                WHERE user_id = @userId;
                """,
                connection);
            normalizePasswordCommand.Parameters.AddWithValue("@password", password);
            normalizePasswordCommand.Parameters.AddWithValue("@userId", userId);
            await normalizePasswordCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        using var updateLoginCommand = new MySqlCommand(
            """
            UPDATE app_user
            SET last_login_at = NOW()
            WHERE user_id = @userId;
            """,
            connection);
        updateLoginCommand.Parameters.AddWithValue("@userId", userId);
        await updateLoginCommand.ExecuteNonQueryAsync(cancellationToken);

        return await BuildAuthenticatedUserAsync(connection, userId, normalizedUsername, roleName, cancellationToken);
    }
}

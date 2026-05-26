using MySql.Data.MySqlClient;

namespace BabyShop.Infrastructure;

public sealed class DbHelper
{
    private readonly string _connectionString;

    public DbHelper(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<MySqlConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new MySqlConnection(_connectionString);

        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch (MySqlException exception)
        {
            connection.Dispose();
            throw new InvalidOperationException("Could not connect to the MySQL database. Check whether XAMPP is running and verify the server, database, username, and password.", exception);
        }
        catch (Exception exception)
        {
            connection.Dispose();
            throw new InvalidOperationException("The application could not establish a database connection.", exception);
        }
    }

    public async Task TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await CreateOpenConnectionAsync(cancellationToken);
        await connection.CloseAsync();
    }
}

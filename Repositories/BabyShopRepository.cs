using System.Data;
using System.Globalization;
using System.IO;
using BabyShop.Infrastructure;
using BabyShop.Localization;
using BabyShop.Models;
using BabyShop.ViewModels;
using MySql.Data.MySqlClient;

namespace BabyShop.Repositories;

public sealed class BabyShopRepository
{
    private sealed record ProcedureDefinition(string ProcedureName, IReadOnlyList<string> ParameterNames);
    private sealed record DashboardOrderLine(
        int OrderId,
        string ClientName,
        string DeliveryAddress,
        DateTime? StartDate,
        DateTime? EndDate,
        string OrderStatus,
        string ProductTitle,
        string FabricType,
        string Color,
        int ProductCount,
        decimal PricePerM,
        decimal FabricAmount,
        decimal LineTotal,
        decimal TotalCost);
    private sealed record DashboardOrderAggregate(
        int OrderId,
        string ClientName,
        string DeliveryAddress,
        DateTime? StartDate,
        DateTime? EndDate,
        string OrderStatus,
        decimal TotalCost,
        IReadOnlyList<DashboardOrderLine> Lines);
    public sealed record FilteredReportSnapshot(DataTable Data, DashboardSummary Summary);
    public sealed record AuditSummarySnapshot(
        int TotalActions,
        int SuccessfulLogins,
        int FailedLogins,
        int Registrations,
        int ActiveUsers);
    public sealed record AuditReportSnapshot(DataTable Data, AuditSummarySnapshot Summary);
    public sealed record AnalyticsReportSnapshot(
        DataTable Data,
        DashboardSummary Summary,
        IReadOnlyList<DashboardSeriesPoint> StatusPoints,
        IReadOnlyList<DashboardSeriesPoint> ProductPoints);

    private static readonly IReadOnlyDictionary<string, string> TableDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["CUSTOMER"] = "Customers",
        ["FABRIC"] = "Fabrics",
        ["CATEGORY"] = "Categories",
        ["CUSTOMERVIEW"] = "Customers",
        ["FABRICVIEW"] = "Fabrics",
        ["PRODUCTT"] = "Products",
        ["CUSTOMER_ORDER"] = "Customer Orders",
        ["ORDER_PRODUCT"] = "Order Items",
        ["PRODUCTFABRICVIEW"] = "Products",
        ["CUSTOMERORDERSVIEW"] = "Customer Orders",
        ["ORDERPRODUCTVIEW"] = "Order Items",
        ["USERSVIEW"] = "Users",
        ["AUDIT_REPORT_VIEW"] = "Audit Log"
    };

    private static readonly IReadOnlyDictionary<string, string> FriendlyColumnNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["NO."] = "№",
        ["CUSTOMER_ID"] = "ID �������",
        ["C_FULLNAME"] = "��� �������",
        ["CUSTOMER_NAME"] = "��� �������",
        ["NAME"] = "��������",
        ["PHONE"] = "�������",
        ["PHONE_NUMBER"] = "�������",
        ["C_PHONE_NUMBER"] = "����� ��������",
        ["EMAIL"] = "��. �����",
        ["FABRIC_ID"] = "ID �����",
        ["FABRIC_NAME"] = "�������� �����",
        ["FABRIC_TYPE"] = "��� �����",
        ["PRICE_PER_M"] = "���� �� ����",
        ["COLOR"] = "����",
        ["PRODUCT_ID"] = "ID ������",
        ["PRODUCT_NAME"] = "�������� ������",
        ["PRODUCT_TITLE"] = "�������� ������",
        ["FABRIC_AMOUNT"] = "������ ����� (�)",
        ["PRICE"] = "����",
        ["QUANTITY"] = "����������",
        ["PRODUCT_COUNT"] = "���������� ������",
        ["STOCK"] = "�������",
        ["STOCK_QUANTITY"] = "���������� �� ������",
        ["ORDER_ID"] = "ID ������",
        ["ORDER_PRODUCT_ID"] = "ID ������� ������",
        ["ORDER_DATE"] = "���� ������",
        ["START_DATE"] = "���� ����������",
        ["END_DATE"] = "���� ����������",
        ["DELIVERY_ADDRESS"] = "����� ��������",
        ["ORDER_STATUS"] = "������ ������",
        ["TOTAL"] = "�����",
        ["TOTAL_PRICE"] = "�������� �����",
        ["TOTAL_COST"] = "����� ���������",
        ["UNIT_PRICE"] = "���� �� �������",
        ["AMOUNT"] = "����������"
    };

    private static readonly IReadOnlyDictionary<string, ProcedureDefinition> InsertProcedures =
        new Dictionary<string, ProcedureDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["CUSTOMER"] = new("AddCustomer", ["p_id", "p_name", "p_phone"]),
            ["FABRIC"] = new("AddFabric", ["pl_N_fabric_id", "pl_S_fabric_type", "pl_Price_per_m", "pl_S_color"]),
            ["PRODUCTT"] = new("AddProduct", ["pl_N_product_id", "pl_S_product_title", "pl_N_category_id", "pl_N_fabric_amount", "pl_N_fabric_id", "pl_N_price_per_m", "pl_S_color", "pl_S_image_path"]),
            ["CUSTOMER_ORDER"] = new("AddCustomerOrder", ["pl_N_Order_id", "pl_N_Customer_id", "pl_S_Delivery_address", "pl_D_Start_date", "pl_D_End_date", "pl_S_Order_status", "pl_N_Total_cost"]),
            ["ORDER_PRODUCT"] = new("AddOrderProduct", ["pl_N_order_product_id", "pl_N_order_id", "pl_N_product_id", "pl_N_product_count"])
        };

    private static readonly IReadOnlyDictionary<string, ProcedureDefinition> UpdateProcedures =
        new Dictionary<string, ProcedureDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["CUSTOMER"] = new("UpdateCustomer", ["pl_N_customer_id", "pl_S_c_fullname", "pl_S_c_phone_number"]),
            ["FABRIC"] = new("UpdateFabric", ["pl_N_fabric_id", "pl_Fabric_type", "pl_Price_per_m", "pl_Color"]),
            ["PRODUCTT"] = new("UpdateProduct", ["pl_N_product_id", "pl_S_product_title", "pl_N_category_id", "pl_Fabric_amount", "pl_N_fabric_id", "pl_Price_per_m", "pl_S_color", "pl_S_image_path"]),
            ["CUSTOMER_ORDER"] = new("UpdateCustomerOrder", ["pl_N_order_id", "pl_N_customer_id", "pl_S_delivery_address", "pl_D_start_date", "pl_D_end_date", "pl_S_order_status", "pl_N_total_cost"]),
            ["ORDER_PRODUCT"] = new("UpdateOrderProduct", ["pl_N_order_product_id", "pl_N_product_count"])
        };

    private static readonly IReadOnlyDictionary<string, ProcedureDefinition> DeleteProcedures =
        new Dictionary<string, ProcedureDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["CUSTOMER"] = new("DeleteCustomer", ["pl_N_customer_id"]),
            ["FABRIC"] = new("DeleteFabric", ["pl_N_fabric_id"]),
            ["PRODUCTT"] = new("DeleteProduct", ["pl_N_product_id"]),
            ["CUSTOMER_ORDER"] = new("DeleteCustomerOrder", ["pl_N_order_id"]),
            ["ORDER_PRODUCT"] = new("DeleteOrderProduct", ["pl_N_order_product_id"])
        };

    private static readonly IReadOnlyDictionary<string, ProcedureDefinition> ViewProcedures =
        new Dictionary<string, ProcedureDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["CUSTOMERVIEW"] = new("ViewCustomers", []),
            ["FABRICVIEW"] = new("ViewFabrics", []),
            ["PRODUCTFABRICVIEW"] = new("ViewProducts", []),
            ["CUSTOMERORDERSVIEW"] = new("ViewCustomerOrders", []),
            ["ORDERPRODUCTVIEW"] = new("ViewOrderProducts", []),
            ["USERSVIEW"] = new("ViewUsers", [])
        };

    private static readonly IReadOnlyDictionary<string, ProcedureDefinition> RecordViewProcedures =
        new Dictionary<string, ProcedureDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["CUSTOMER"] = new("GetCustomerByPhone", ["p_phone"]),
            ["FABRIC"] = new("GetFabricByTypeAndColor", ["p_fabric_type", "p_color"]),
            ["PRODUCTT"] = new("GetProductById", ["p_product_id"]),
            ["CUSTOMER_ORDER"] = new("GetCustomerOrderById", ["p_order_id"]),
            ["ORDER_PRODUCT"] = new("GetOrderProductByOrderAndProduct", ["p_order_id", "p_product_id"])
        };

    private readonly DbHelper _dbHelper;

    public BabyShopRepository(DbHelper dbHelper)
    {
        _dbHelper = dbHelper;
    }

    public string GetFriendlyTableName(string tableName)
    {
        var localizedName = LanguageManager.GetTableName(tableName);
        if (!string.IsNullOrWhiteSpace(localizedName))
        {
            return localizedName;
        }

        return TableDisplayNames.TryGetValue(tableName, out var displayName)
            ? displayName
            : ToFriendlyText(tableName);
    }

    public Task TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        return _dbHelper.TestConnectionAsync(cancellationToken);
    }

    public async Task<DataTable> GetDisplayTableAsync(string tableName, CancellationToken cancellationToken = default)
    {
        if (ViewProcedures.TryGetValue(tableName, out var definition))
        {
            return await LoadDisplayTableFromProcedureAsync(tableName, definition, connection: null, transaction: null, cancellationToken);
        }

        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand($"SELECT * FROM {EscapeIdentifier(tableName)};", connection);

        try
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var rawTable = new DataTable();
            rawTable.Load(reader);
            var columns = await GetColumnMetadataAsync(
                tableName,
                includeAutoIncrement: true,
                connection,
                transaction: null,
                cancellationToken);
            return await BuildDisplayTableAsync(tableName, rawTable, columns, cancellationToken);
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException(LanguageManager.Format("CouldNotLoadData", GetFriendlyTableName(tableName), exception.Message), exception);
        }
    }

    public async Task<DataTable> GetReportDisplayTableAsync(string tableName, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);

        try
        {
            DataTable table;

            if (ViewProcedures.TryGetValue(tableName, out var definition))
            {
                table = await LoadDisplayTableFromProcedureAsync(tableName, definition, connection, transaction, cancellationToken);
            }
            else
            {
                using var command = new MySqlCommand($"SELECT * FROM {EscapeIdentifier(tableName)};", connection)
                {
                    Transaction = transaction
                };

                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                var rawTable = new DataTable();
                rawTable.Load(reader);
                var columns = await GetColumnMetadataAsync(
                    tableName,
                    includeAutoIncrement: true,
                    connection,
                    transaction,
                    cancellationToken);
                table = await BuildDisplayTableAsync(tableName, rawTable, columns, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return table;
        }
        catch
        {
            await RollbackQuietlyAsync(transaction, cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<ColumnMetadata>> GetInsertableColumnsAsync(string tableName, CancellationToken cancellationToken = default)
    {
        return await GetColumnMetadataAsync(tableName, includeAutoIncrement: false, cancellationToken: cancellationToken);
    }

    public async Task<FilteredReportSnapshot> GetFilteredReportSnapshotAsync(
        DashboardFilter filter,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);

        try
        {
            var data = await ExecuteDashboardTableProcedureAsync(connection, transaction, "GetDashboardData", filter, cancellationToken);
            var summaryTable = await ExecuteDashboardTableProcedureAsync(connection, transaction, "GetDashboardSummary", filter, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return new FilteredReportSnapshot(data, MapDashboardSummary(summaryTable));
        }
        catch
        {
            await RollbackQuietlyAsync(transaction, cancellationToken);
            throw;
        }
    }

    public async Task<AnalyticsReportSnapshot> GetAnalyticsReportSnapshotAsync(
        DashboardFilter filter,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);

        try
        {
            var data = await ExecuteDashboardTableProcedureAsync(connection, transaction, "GetDashboardData", filter, cancellationToken);
            var summaryTable = await ExecuteDashboardTableProcedureAsync(connection, transaction, "GetDashboardSummary", filter, cancellationToken);
            var statusTable = await ExecuteDashboardTableProcedureAsync(connection, transaction, "GetDashboardStatusChart", filter, cancellationToken);
            var productTable = await ExecuteDashboardTableProcedureAsync(connection, transaction, "GetDashboardProductChart", filter, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return new AnalyticsReportSnapshot(
                data,
                MapDashboardSummary(summaryTable),
                MapStatusChart(statusTable),
                MapProductChart(productTable));
        }
        catch
        {
            await RollbackQuietlyAsync(transaction, cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<LookupOption>> GetAuditUserOptionsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand(
            """
            SELECT DISTINCT username
            FROM audit_report_view
            WHERE username IS NOT NULL AND TRIM(username) <> ''
            ORDER BY username;
            """,
            connection);

        var options = new List<LookupOption>();

        try
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var username = Convert.ToString(reader["username"], CultureInfo.InvariantCulture)?.Trim();
                if (string.IsNullOrWhiteSpace(username))
                {
                    continue;
                }

                options.Add(new LookupOption
                {
                    Value = username,
                    Label = username
                });
            }
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException($"Could not load audit users. {exception.Message}", exception);
        }

        return options;
    }

    public async Task<IReadOnlyList<LookupOption>> GetAuditActionOptionsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand(
            """
            SELECT
                action_type,
                MAX(action_label) AS action_label
            FROM audit_report_view
            WHERE action_type IS NOT NULL AND TRIM(action_type) <> ''
            GROUP BY action_type
            ORDER BY action_type;
            """,
            connection);

        var options = new List<LookupOption>();

        try
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var actionType = Convert.ToString(reader["action_type"], CultureInfo.InvariantCulture)?.Trim();
                if (string.IsNullOrWhiteSpace(actionType))
                {
                    continue;
                }

                var actionLabel = Convert.ToString(reader["action_label"], CultureInfo.InvariantCulture)?.Trim();
                options.Add(new LookupOption
                {
                    Value = actionType,
                    Label = string.IsNullOrWhiteSpace(actionLabel)
                        ? actionType
                        : $"{actionLabel} ({actionType})"
                });
            }
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException($"Could not load audit actions. {exception.Message}", exception);
        }

        return options;
    }

    public async Task<AuditReportSnapshot> GetAuditReportSnapshotAsync(
        AuditReportFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);

        try
        {
            var data = await ExecuteAuditReportProcedureAsync(connection, transaction, filter, cancellationToken);
            var summary = await ExecuteAuditSummaryProcedureAsync(connection, transaction, filter, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return new AuditReportSnapshot(data, summary);
        }
        catch
        {
            await RollbackQuietlyAsync(transaction, cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyCollection<string>> GetUserPermissionsAsync(int userId, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand("GetUserPermissions", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("@p_user_id", userId);

        var permissionCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var permissionCode = Convert.ToString(reader["permission_code"], CultureInfo.InvariantCulture)?.Trim();
                if (!string.IsNullOrWhiteSpace(permissionCode))
                {
                    permissionCodes.Add(permissionCode);
                }
            }
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException($"Could not load user permissions. {exception.Message}", exception);
        }

        return permissionCodes.ToArray();
    }

    public async Task AddUserAsync(
        AppUser actor,
        string username,
        string password,
        string roleName,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);

        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand("AddUser", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.AddWithValue("@p_actor_user_id", actor.UserId);
        command.Parameters.AddWithValue("@p_actor_username", actor.Username);
        command.Parameters.AddWithValue("@p_username", username.Trim());
        command.Parameters.AddWithValue("@p_password", password);
        command.Parameters.AddWithValue("@p_role_name", roleName.Trim());
        command.Parameters.AddWithValue("@p_is_active", isActive);

        await ExecuteUserMutationProcedureAsync(command, "Could not add the user.", cancellationToken);
    }

    public async Task UpdateUserAsync(
        AppUser actor,
        int userId,
        string username,
        string? password,
        string roleName,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);

        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand("UpdateUser", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.AddWithValue("@p_actor_user_id", actor.UserId);
        command.Parameters.AddWithValue("@p_actor_username", actor.Username);
        command.Parameters.AddWithValue("@p_user_id", userId);
        command.Parameters.AddWithValue("@p_username", username.Trim());
        command.Parameters.AddWithValue("@p_password", password ?? string.Empty);
        command.Parameters.AddWithValue("@p_role_name", roleName.Trim());
        command.Parameters.AddWithValue("@p_is_active", isActive);

        await ExecuteUserMutationProcedureAsync(command, "Could not update the user.", cancellationToken);
    }

    public async Task DeleteUserAsync(
        AppUser actor,
        int userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);

        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand("DeleteUser", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.AddWithValue("@p_actor_user_id", actor.UserId);
        command.Parameters.AddWithValue("@p_actor_username", actor.Username);
        command.Parameters.AddWithValue("@p_user_id", userId);

        await ExecuteUserMutationProcedureAsync(command, "Could not delete the user.", cancellationToken);
    }

    public async Task<IReadOnlyList<BackupHistoryEntry>> GetBackupHistoryAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand("GetBackupHistory", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        var entries = new List<BackupHistoryEntry>();

        try
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                entries.Add(MapBackupHistoryEntry(reader));
            }
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException($"Could not load backup history. {exception.Message}", exception);
        }

        return entries;
    }

    public async Task<BackupHistoryEntry?> GetLastBackupAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand("GetLastBackup", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        try
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken)
                ? MapBackupHistoryEntry(reader)
                : null;
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException($"Could not load the last backup information. {exception.Message}", exception);
        }
    }

    public async Task<BackupHistoryEntry?> AddBackupHistoryAsync(
        int userId,
        string username,
        string operationType,
        string fileName,
        string filePath,
        decimal fileSizeKb,
        string databaseName,
        string status,
        string message,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand("AddBackupHistory", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.AddWithValue("@p_user_id", userId);
        command.Parameters.AddWithValue("@p_username", username);
        command.Parameters.AddWithValue("@p_operation_type", operationType);
        command.Parameters.AddWithValue("@p_file_name", fileName);
        command.Parameters.AddWithValue("@p_file_path", filePath);
        command.Parameters.AddWithValue("@p_file_size_kb", fileSizeKb);
        command.Parameters.AddWithValue("@p_database_name", databaseName);
        command.Parameters.AddWithValue("@p_status", status);
        command.Parameters.AddWithValue("@p_message", message);

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);

            using var idCommand = new MySqlCommand("SELECT LAST_INSERT_ID();", connection);
            var insertedIdObject = await idCommand.ExecuteScalarAsync(cancellationToken);
            if (insertedIdObject is null or DBNull)
            {
                return null;
            }

            var insertedId = Convert.ToInt32(insertedIdObject, CultureInfo.InvariantCulture);
            using var fetchCommand = new MySqlCommand(
                """
                SELECT
                    backup_id,
                    username,
                    operation_type,
                    file_name,
                    file_path,
                    file_size_kb,
                    database_name,
                    status,
                    message,
                    created_at
                FROM backup_history
                WHERE backup_id = @backupId
                LIMIT 1;
                """,
                connection);
            fetchCommand.Parameters.AddWithValue("@backupId", insertedId);

            using var reader = await fetchCommand.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken)
                ? MapBackupHistoryEntry(reader)
                : null;
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException($"Could not write backup history. {exception.Message}", exception);
        }
    }

    public async Task AddAuditLogAsync(
        int userId,
        string username,
        string actionType,
        string entityName,
        int? entityId,
        string actionDescription,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand("AddAuditLog", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.AddWithValue("@p_user_id", userId);
        command.Parameters.AddWithValue("@p_username", username);
        command.Parameters.AddWithValue("@p_action_type", actionType);
        command.Parameters.AddWithValue("@p_entity_name", entityName);
        command.Parameters.AddWithValue("@p_entity_id", entityId.HasValue ? entityId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@p_action_description", actionDescription);

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException($"Could not write audit log. {exception.Message}", exception);
        }
    }

    private async Task<IReadOnlyList<ColumnMetadata>> GetColumnMetadataAsync(
        string tableName,
        bool includeAutoIncrement,
        MySqlConnection? connection = null,
        MySqlTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                c.COLUMN_NAME,
                c.DATA_TYPE,
                c.IS_NULLABLE,
                c.EXTRA,
                k.REFERENCED_TABLE_NAME,
                k.REFERENCED_COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS c
            LEFT JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE k
                ON c.TABLE_SCHEMA = k.TABLE_SCHEMA
               AND c.TABLE_NAME = k.TABLE_NAME
               AND c.COLUMN_NAME = k.COLUMN_NAME
            WHERE c.TABLE_SCHEMA = DATABASE()
              AND c.TABLE_NAME = @tableName
            ORDER BY c.ORDINAL_POSITION;
            """;

        var ownsConnection = connection is null;
        connection ??= await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand(sql, connection)
        {
            Transaction = transaction
        };
        command.Parameters.AddWithValue("@tableName", tableName);

        var columns = new List<ColumnMetadata>();

        try
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var isAutoIncrement = reader.GetString("EXTRA").Contains("auto_increment", StringComparison.OrdinalIgnoreCase);
                if (!includeAutoIncrement && isAutoIncrement)
                {
                    continue;
                }

                var columnName = reader.GetString("COLUMN_NAME");
                var metadata = new ColumnMetadata
                {
                    ColumnName = columnName,
                    DisplayName = GetDisplayColumnName(columnName),
                    DataType = reader.GetString("DATA_TYPE").ToLowerInvariant(),
                    IsNullable = string.Equals(reader.GetString("IS_NULLABLE"), "YES", StringComparison.OrdinalIgnoreCase),
                    IsAutoIncrement = isAutoIncrement,
                    ReferencedTableName = reader["REFERENCED_TABLE_NAME"] as string,
                    ReferencedColumnName = reader["REFERENCED_COLUMN_NAME"] as string
                };

                metadata = ApplyKnownForeignKeyFallbacks(tableName, metadata);
                var existingIndex = columns.FindIndex(existing => existing.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase));
                if (existingIndex >= 0)
                {
                    if (!columns[existingIndex].HasLookup && metadata.HasLookup)
                    {
                        columns[existingIndex] = metadata;
                    }

                    continue;
                }

                columns.Add(metadata);
            }
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException(LanguageManager.Format("CouldNotReadSchema", GetFriendlyTableName(tableName), exception.Message), exception);
        }
        finally
        {
            if (ownsConnection)
            {
                await connection.DisposeAsync();
            }
        }

        return columns;
    }

    private static async Task<DataTable> ExecuteAuditReportProcedureAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        AuditReportFilter filter,
        CancellationToken cancellationToken)
    {
        using var command = new MySqlCommand("GetAuditReport", connection, transaction)
        {
            CommandType = CommandType.StoredProcedure
        };

        AddAuditProcedureParameters(command, filter);

        try
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var table = new DataTable();
            table.Load(reader);
            return table;
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException($"Could not load the audit report. {exception.Message}", exception);
        }
    }

    private static async Task<AuditSummarySnapshot> ExecuteAuditSummaryProcedureAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        AuditReportFilter filter,
        CancellationToken cancellationToken)
    {
        using var command = new MySqlCommand("GetAuditSummary", connection, transaction)
        {
            CommandType = CommandType.StoredProcedure
        };

        AddAuditSummaryParameters(command, filter);

        try
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return new AuditSummarySnapshot(0, 0, 0, 0, 0);
            }

            return new AuditSummarySnapshot(
                TotalActions: reader["total_actions"] is DBNull ? 0 : Convert.ToInt32(reader["total_actions"], CultureInfo.InvariantCulture),
                SuccessfulLogins: reader["successful_logins"] is DBNull ? 0 : Convert.ToInt32(reader["successful_logins"], CultureInfo.InvariantCulture),
                FailedLogins: reader["failed_logins"] is DBNull ? 0 : Convert.ToInt32(reader["failed_logins"], CultureInfo.InvariantCulture),
                Registrations: reader["registrations"] is DBNull ? 0 : Convert.ToInt32(reader["registrations"], CultureInfo.InvariantCulture),
                ActiveUsers: reader["active_users"] is DBNull ? 0 : Convert.ToInt32(reader["active_users"], CultureInfo.InvariantCulture));
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException($"Could not load the audit summary. {exception.Message}", exception);
        }
    }

    private static void AddAuditProcedureParameters(MySqlCommand command, AuditReportFilter filter)
    {
        command.Parameters.AddWithValue("@p_date_from", AuditDateFromValue(filter.DateFrom));
        command.Parameters.AddWithValue("@p_date_to", AuditDateToValue(filter.DateTo));
        command.Parameters.AddWithValue("@p_username", TextOrDbNull(filter.Username));
        command.Parameters.AddWithValue("@p_action_type", TextOrDbNull(filter.ActionType));
    }

    private static void AddAuditSummaryParameters(MySqlCommand command, AuditReportFilter filter)
    {
        command.Parameters.AddWithValue("@p_date_from", AuditDateFromValue(filter.DateFrom));
        command.Parameters.AddWithValue("@p_date_to", AuditDateToValue(filter.DateTo));
    }

    private static object AuditDateFromValue(DateTime? value)
    {
        return value.HasValue ? value.Value.Date : DBNull.Value;
    }

    private static object AuditDateToValue(DateTime? value)
    {
        return value.HasValue ? value.Value.Date.AddDays(1).AddTicks(-1) : DBNull.Value;
    }

    private static object TextOrDbNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    }

    private static ColumnMetadata ApplyKnownForeignKeyFallbacks(string tableName, ColumnMetadata column)
    {
        if (column.HasLookup)
        {
            return column;
        }

        var table = tableName.ToUpperInvariant();
        var columnName = column.ColumnName.ToUpperInvariant();

        return (table, columnName) switch
        {
            ("ORDER_PRODUCT", "ORDER_ID") => WithLookup(column, "CUSTOMER_ORDER", "order_id"),
            ("ORDER_PRODUCT", "PRODUCT_ID") => WithLookup(column, "PRODUCTT", "product_id"),
            ("CUSTOMER_ORDER", "CUSTOMER_ID") => WithLookup(column, "CUSTOMER", "customer_id"),
            ("PRODUCTT", "CATEGORY_ID") => WithLookup(column, "CATEGORY", "category_id"),
            ("PRODUCTT", "FABRIC_ID") => WithLookup(column, "FABRIC", "fabric_id"),
            _ => column
        };
    }

    private static ColumnMetadata WithLookup(ColumnMetadata column, string referencedTableName, string referencedColumnName)
    {
        return new ColumnMetadata
        {
            ColumnName = column.ColumnName,
            DisplayName = column.DisplayName,
            DataType = column.DataType,
            IsNullable = column.IsNullable,
            IsAutoIncrement = column.IsAutoIncrement,
            ReferencedTableName = referencedTableName,
            ReferencedColumnName = referencedColumnName
        };
    }

    public async Task<IReadOnlyList<LookupOption>> GetLookupOptionsAsync(string tableName, string valueColumn, CancellationToken cancellationToken = default)
    {
        if (tableName.Equals("CUSTOMER_ORDER", StringComparison.OrdinalIgnoreCase) &&
            valueColumn.Equals("order_id", StringComparison.OrdinalIgnoreCase))
        {
            return await GetCustomerOrderLookupOptionsAsync(cancellationToken);
        }

        if (tableName.Equals("PRODUCTT", StringComparison.OrdinalIgnoreCase) &&
            valueColumn.Equals("product_id", StringComparison.OrdinalIgnoreCase))
        {
            return await GetProductLookupOptionsAsync(cancellationToken);
        }

        var columns = await GetInsertableColumnsAsync(tableName, cancellationToken);
        var displayColumn = GetLookupDisplayColumn(columns, valueColumn);

        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand(
            $"SELECT {EscapeIdentifier(valueColumn)}, {EscapeIdentifier(displayColumn)} FROM {EscapeIdentifier(tableName)} ORDER BY {EscapeIdentifier(displayColumn)};",
            connection);

        var options = new List<LookupOption>();

        try
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var value = Convert.ToString(reader[valueColumn], CultureInfo.InvariantCulture) ?? string.Empty;
                var display = Convert.ToString(reader[displayColumn], CultureInfo.InvariantCulture) ?? value;
                options.Add(new LookupOption
                {
                    Value = value,
                    Label = display
                });
            }
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException(LanguageManager.Format("CouldNotLoadLookup", GetFriendlyTableName(tableName), exception.Message), exception);
        }

        return options;
    }

    private async Task<IReadOnlyList<LookupOption>> GetCustomerOrderLookupOptionsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand(
            """
            SELECT
                co.order_id,
                CONCAT('#', co.order_id, ' - ', c.c_fullname, ' - ', DATE_FORMAT(co.start_date, '%d.%m.%Y'), ' - ', co.order_status) AS order_label
            FROM customer_order co
            JOIN customer c ON c.customer_id = co.customer_id
            ORDER BY co.order_id;
            """,
            connection);

        var options = new List<LookupOption>();

        try
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                options.Add(new LookupOption
                {
                    Value = Convert.ToString(reader["order_id"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Label = Convert.ToString(reader["order_label"], CultureInfo.InvariantCulture) ?? string.Empty
                });
            }
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException(LanguageManager.Format("CouldNotLoadLookup", GetFriendlyTableName("CUSTOMER_ORDER"), exception.Message), exception);
        }

        return options;
    }

    private async Task<IReadOnlyList<LookupOption>> GetProductLookupOptionsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand(
            """
            SELECT
                product_id,
                CONCAT('#', product_id, ' - ', COALESCE(product_title, 'Product'), ' - ', ROUND(price_per_m, 2)) AS product_label
            FROM productt
            ORDER BY product_title, product_id;
            """,
            connection);

        var options = new List<LookupOption>();

        try
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                options.Add(new LookupOption
                {
                    Value = Convert.ToString(reader["product_id"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Label = Convert.ToString(reader["product_label"], CultureInfo.InvariantCulture) ?? string.Empty
                });
            }
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException(LanguageManager.Format("CouldNotLoadLookup", GetFriendlyTableName("PRODUCTT"), exception.Message), exception);
        }

        return options;
    }

    public async Task<IReadOnlyList<LookupOption>> GetAddressOptionsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand(
            "SELECT address_text FROM address WHERE address_text IS NOT NULL AND TRIM(address_text) <> '' ORDER BY address_text;",
            connection);

        var options = new List<LookupOption>();

        try
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var address = Convert.ToString(reader["address_text"], CultureInfo.InvariantCulture)?.Trim();
                if (string.IsNullOrWhiteSpace(address))
                {
                    continue;
                }

                options.Add(new LookupOption
                {
                    Value = address,
                    Label = address
                });
            }
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException($"Could not load lookup values from addresses. {exception.Message}", exception);
        }

        return options;
    }

    public async Task<CheckoutCustomerDetails?> GetLatestCheckoutCustomerDetailsByFullNameAsync(
        string fullName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return null;
        }

        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand(
            """
            SELECT
                c.c_fullname,
                c.c_phone_number,
                COALESCE(co.delivery_address, '') AS delivery_address
            FROM customer c
            LEFT JOIN customer_order co ON co.customer_id = c.customer_id
            WHERE TRIM(c.c_fullname) = @fullName
            ORDER BY
                COALESCE(co.start_date, '1900-01-01') DESC,
                co.order_id DESC,
                c.customer_id DESC
            LIMIT 1;
            """,
            connection);
        command.Parameters.AddWithValue("@fullName", fullName.Trim());

        try
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            var customerFullName = Convert.ToString(reader["c_fullname"], CultureInfo.InvariantCulture)?.Trim();
            if (string.IsNullOrWhiteSpace(customerFullName))
            {
                return null;
            }

            var nameParts = customerFullName
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return new CheckoutCustomerDetails
            {
                FirstName = nameParts.Length > 0 ? nameParts[0] : string.Empty,
                LastName = nameParts.Length > 1 ? string.Join(' ', nameParts.Skip(1)) : string.Empty,
                PhoneDigits = Convert.ToString(reader["c_phone_number"], CultureInfo.InvariantCulture)?.Trim() ?? string.Empty,
                DeliveryAddress = Convert.ToString(reader["delivery_address"], CultureInfo.InvariantCulture)?.Trim() ?? string.Empty
            };
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException($"Could not load the latest checkout customer details. {exception.Message}", exception);
        }
    }

    public async Task<CheckoutCustomerDetails?> GetLatestCheckoutCustomerDetailsByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return null;
        }

        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand("GetLatestCheckoutCustomerDetailsByUserId", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("@p_user_id", userId);

        try
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            var customerFullName = Convert.ToString(reader["c_fullname"], CultureInfo.InvariantCulture)?.Trim();
            if (string.IsNullOrWhiteSpace(customerFullName))
            {
                return null;
            }

            var nameParts = customerFullName
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return new CheckoutCustomerDetails
            {
                FirstName = nameParts.Length > 0 ? nameParts[0] : string.Empty,
                LastName = nameParts.Length > 1 ? string.Join(' ', nameParts.Skip(1)) : string.Empty,
                PhoneDigits = Convert.ToString(reader["c_phone_number"], CultureInfo.InvariantCulture)?.Trim() ?? string.Empty,
                DeliveryAddress = Convert.ToString(reader["delivery_address"], CultureInfo.InvariantCulture)?.Trim() ?? string.Empty
            };
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException($"Could not load the latest checkout customer details. {exception.Message}", exception);
        }
    }

    public async Task EnsureAddressExistsAsync(string address, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return;
        }

        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        var normalizedAddress = address.Trim();

        try
        {
            using var command = new MySqlCommand("CALL AddAddress(@addressText);", connection);
            command.Parameters.AddWithValue("@addressText", normalizedAddress);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (MySqlException exception) when (exception.Number == 1305)
        {
            await InsertLookupValueFallbackAsync(
                connection,
                "address",
                "address_text",
                normalizedAddress,
                cancellationToken);
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException($"Could not save the address suggestion. {exception.Message}", exception);
        }
    }

    public async Task<IReadOnlyList<LookupOption>> GetColorOptionsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand(
            "SELECT color_name FROM color_lookup WHERE color_name IS NOT NULL AND TRIM(color_name) <> '' ORDER BY color_name;",
            connection);

        var options = new List<LookupOption>();

        try
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var color = Convert.ToString(reader["color_name"], CultureInfo.InvariantCulture)?.Trim();
                if (string.IsNullOrWhiteSpace(color))
                {
                    continue;
                }

                options.Add(new LookupOption
                {
                    Value = color,
                    Label = color
                });
            }
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException($"Could not load lookup values from colors. {exception.Message}", exception);
        }

        return options;
    }

    public async Task EnsureColorExistsAsync(string color, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return;
        }

        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        var normalizedColor = color.Trim();

        try
        {
            using var command = new MySqlCommand("CALL AddColor(@colorName);", connection);
            command.Parameters.AddWithValue("@colorName", normalizedColor);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (MySqlException exception) when (exception.Number == 1305)
        {
            await InsertLookupValueFallbackAsync(
                connection,
                "color_lookup",
                "color_name",
                normalizedColor,
                cancellationToken);
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException($"Could not save the color suggestion. {exception.Message}", exception);
        }
    }

    public async Task<int> GetNextIdentifierValueAsync(string tableName, string columnName, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand(
            $"SELECT COALESCE(MAX({EscapeIdentifier(columnName)}), 0) + 1 FROM {EscapeIdentifier(tableName)};",
            connection);

        try
        {
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException(LanguageManager.Format("CouldNotGenerateIdentifier", GetFriendlyTableName(tableName), exception.Message), exception);
        }
    }

    public async Task InsertRecordAsync(string tableName, IReadOnlyDictionary<string, object?> values, CancellationToken cancellationToken = default)
    {
        if (!InsertProcedures.TryGetValue(tableName, out var definition))
        {
            throw new InvalidOperationException(LanguageManager.Format("NoInsertProcedure", GetFriendlyTableName(tableName)));
        }

        var procedureValues = BuildProcedureValues(tableName, definition, values);
        await ExecuteStoredProcedureAsync(definition, procedureValues, GetFriendlyTableName(tableName), "add", cancellationToken);
    }

    public async Task InsertCustomerOrderWithProductsAsync(
        IReadOnlyDictionary<string, object?> orderValues,
        IReadOnlyList<(int ProductId, int Quantity)> orderItems,
        CancellationToken cancellationToken = default)
    {
        if (orderItems.Count == 0)
        {
            await InsertRecordAsync("CUSTOMER_ORDER", orderValues, cancellationToken);
            return;
        }

        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var orderId = Convert.ToInt32(orderValues["order_id"], CultureInfo.InvariantCulture);
            var customerId = Convert.ToInt32(orderValues["customer_id"], CultureInfo.InvariantCulture);
            var deliveryAddress = Convert.ToString(orderValues["delivery_address"], CultureInfo.InvariantCulture)?.Trim()
                                  ?? string.Empty;
            var startDate = Convert.ToDateTime(orderValues["start_date"], CultureInfo.InvariantCulture);
            var endDate = Convert.ToDateTime(orderValues["end_date"], CultureInfo.InvariantCulture);
            var orderStatus = Convert.ToString(orderValues["order_status"], CultureInfo.InvariantCulture)?.Trim()
                              ?? "Pending";

            await InsertCustomerOrderCoreAsync(
                connection,
                transaction,
                orderId,
                customerId,
                deliveryAddress,
                startDate,
                endDate,
                orderStatus,
                cancellationToken);

            foreach (var (productId, quantity) in orderItems)
            {
                var orderProductId = await GetNextIdentifierValueAsync(
                    connection,
                    transaction,
                    "order_product",
                    "order_product_id",
                    cancellationToken);

                await InsertOrderProductCoreAsync(
                    connection,
                    transaction,
                    orderProductId,
                    orderId,
                    productId,
                    quantity < 1 ? 1 : quantity,
                    cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (MySqlException exception)
        {
            await RollbackQuietlyAsync(transaction, cancellationToken);
            throw new InvalidOperationException(
                BuildFriendlyMutationErrorMessage(exception, GetFriendlyTableName("CUSTOMER_ORDER"), "add"),
                exception);
        }
        catch
        {
            await RollbackQuietlyAsync(transaction, cancellationToken);
            throw;
        }
    }

    public async Task UpdateRecordAsync(string tableName, IReadOnlyDictionary<string, object?> values, CancellationToken cancellationToken = default)
    {
        if (!UpdateProcedures.TryGetValue(tableName, out var definition))
        {
            throw new InvalidOperationException(LanguageManager.Format("NoUpdateProcedure", GetFriendlyTableName(tableName)));
        }

        var procedureValues = BuildProcedureValues(tableName, definition, values);
        await ExecuteStoredProcedureAsync(definition, procedureValues, GetFriendlyTableName(tableName), "update", cancellationToken);
    }

    public async Task DeleteRecordAsync(string tableName, object identifier, CancellationToken cancellationToken = default)
    {
        if (!DeleteProcedures.TryGetValue(tableName, out var definition))
        {
            throw new InvalidOperationException(LanguageManager.Format("NoDeleteProcedure", GetFriendlyTableName(tableName)));
        }

        await ExecuteStoredProcedureAsync(definition, [identifier], GetFriendlyTableName(tableName), "delete", cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, object?>> GetRecordForEditAsync(
        string tableName,
        IReadOnlyDictionary<string, object?> sourceValues,
        CancellationToken cancellationToken = default)
    {
        if (!RecordViewProcedures.TryGetValue(tableName, out var definition))
        {
            throw new InvalidOperationException($"No edit source is configured for {GetFriendlyTableName(tableName)}.");
        }

        var parameters = tableName.ToUpperInvariant() switch
        {
            "CUSTOMER" => new object?[] { GetSourceValue(sourceValues, "c_phone_number") },
            "FABRIC" => new object?[] { GetSourceValue(sourceValues, "fabric_type"), GetSourceValue(sourceValues, "color") },
            "PRODUCTT" => new object?[] { GetSourceValue(sourceValues, "product_id") },
            "CUSTOMER_ORDER" => new object?[] { GetSourceValue(sourceValues, "order_id") },
            "ORDER_PRODUCT" => new object?[] { GetSourceValue(sourceValues, "order_id"), GetSourceValue(sourceValues, "product_id") },
            _ => Array.Empty<object?>()
        };

        return await LoadSingleRecordFromProcedureAsync(definition, parameters, GetFriendlyTableName(tableName), cancellationToken);
    }

    public async Task DeleteRecordFromSourceAsync(
        string tableName,
        IReadOnlyDictionary<string, object?> sourceValues,
        CancellationToken cancellationToken = default)
    {
        var record = await GetRecordForEditAsync(tableName, sourceValues, cancellationToken);
        var identifierColumn = tableName.ToUpperInvariant() switch
        {
            "CUSTOMER" => "customer_id",
            "FABRIC" => "fabric_id",
            "PRODUCTT" => "product_id",
            "CUSTOMER_ORDER" => "order_id",
            "ORDER_PRODUCT" => "order_product_id",
            _ => throw new InvalidOperationException(LanguageManager.Format("NoDeleteIdentifier", GetFriendlyTableName(tableName)))
        };

        await DeleteRecordAsync(tableName, record[identifierColumn], cancellationToken);
    }

    public async Task<IReadOnlyList<LookupOption>> GetDashboardLookupOptionsAsync(
        string columnName,
        CancellationToken cancellationToken = default)
    {
        return columnName switch
        {
            "c_fullname" => await LoadLookupOptionsFromProcedureAsync("GetDashboardClients", "c_fullname", cancellationToken),
            "product_title" => await LoadLookupOptionsFromProcedureAsync("GetDashboardProducts", "product_title", cancellationToken),
            "fabric_type" => await LoadLookupOptionsFromProcedureAsync("GetDashboardFabricTypes", "fabric_type", cancellationToken),
            _ => throw new InvalidOperationException($"The dashboard filter column '{columnName}' is not supported.")
        };
    }

    public async Task<DataTable> GetDashboardDataAsync(DashboardFilter filter, CancellationToken cancellationToken = default)
    {
        var orders = await LoadFilteredDashboardOrdersAsync(filter, cancellationToken);
        return BuildDashboardDataTable(orders);
    }

    public async Task<DashboardSummary> GetDashboardSummaryAsync(DashboardFilter filter, CancellationToken cancellationToken = default)
    {
        var orders = await LoadFilteredDashboardOrdersAsync(filter, cancellationToken);
        var totals = orders.Select(order => order.TotalCost).ToList();

        if (totals.Count == 0)
        {
            return new DashboardSummary();
        }

        return new DashboardSummary
        {
            TotalSum = totals.Sum(),
            OrderCount = totals.Count,
            AverageValue = totals.Average(),
            MinValue = totals.Min(),
            MaxValue = totals.Max()
        };
    }

    public async Task<(decimal Minimum, decimal Maximum)> GetDashboardAmountBoundsAsync(CancellationToken cancellationToken = default)
    {
        var table = await ExecuteSimpleTableProcedureAsync("GetDashboardAmountBounds", cancellationToken);

        if (table.Rows.Count == 0)
        {
            return (0m, 1m);
        }

        var row = table.Rows[0];
        return (ReadDecimal(row, "min_value"), ReadDecimal(row, "max_value"));
    }

    public async Task<IReadOnlyList<DashboardSeriesPoint>> GetDashboardStatusChartAsync(DashboardFilter filter, CancellationToken cancellationToken = default)
    {
        var orders = await LoadFilteredDashboardOrdersAsync(filter, cancellationToken);

        return orders
            .GroupBy(order => order.OrderStatus, StringComparer.OrdinalIgnoreCase)
            .Select(group => new DashboardSeriesPoint
            {
                Label = group.First().OrderStatus,
                Value = group.Count(),
                SecondaryValue = group.Sum(order => order.TotalCost)
            })
            .Where(point => !string.IsNullOrWhiteSpace(point.Label) && point.Value > 0)
            .OrderByDescending(point => point.Value)
            .ThenBy(point => point.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<DashboardSeriesPoint>> GetDashboardProductChartAsync(DashboardFilter filter, CancellationToken cancellationToken = default)
    {
        var orders = await LoadFilteredDashboardOrdersAsync(filter, cancellationToken);

        return orders
            .SelectMany(order => order.Lines)
            .Where(line => !string.IsNullOrWhiteSpace(line.ProductTitle))
            .GroupBy(line => line.ProductTitle, StringComparer.OrdinalIgnoreCase)
            .Select(group => new DashboardSeriesPoint
            {
                Label = group.First().ProductTitle,
                Value = group.Sum(line => line.LineTotal),
                SecondaryValue = group.Sum(line => line.ProductCount)
            })
            .Where(point => point.Value > 0)
            .OrderByDescending(point => point.Value)
            .ThenBy(point => point.Label, StringComparer.CurrentCultureIgnoreCase)
            .Take(8)
            .ToList();
    }

    public async Task<IReadOnlyList<DashboardSeriesPoint>> GetDashboardCategoryChartAsync(DashboardFilter filter, CancellationToken cancellationToken = default)
    {
        var orders = await LoadFilteredDashboardOrdersAsync(filter, cancellationToken);

        return orders
            .SelectMany(order => order.Lines)
            .Where(line => !string.IsNullOrWhiteSpace(line.FabricType))
            .GroupBy(line => line.FabricType, StringComparer.OrdinalIgnoreCase)
            .Select(group => new DashboardSeriesPoint
            {
                Label = group.First().FabricType,
                Value = group.Sum(line => line.LineTotal),
                SecondaryValue = group.Sum(line => line.ProductCount)
            })
            .Where(point => !string.IsNullOrWhiteSpace(point.Label) && point.Value > 0)
            .OrderByDescending(point => point.Value)
            .ThenBy(point => point.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<DashboardSeriesPoint>> GetDashboardTimelineChartAsync(DashboardFilter filter, CancellationToken cancellationToken = default)
    {
        var orders = await LoadFilteredDashboardOrdersAsync(filter, cancellationToken);

        return orders
            .Where(order => order.StartDate.HasValue)
            .GroupBy(order => order.StartDate!.Value.Date)
            .OrderBy(group => group.Key)
            .Select(group => new DashboardSeriesPoint
            {
                Label = group.Key.ToString("dd.MM"),
                Value = group.Sum(order => order.TotalCost),
                SecondaryValue = group.Count()
            })
            .ToList();
    }

    public async Task<IReadOnlyList<DashboardSeriesPoint>> GetDashboardMonthStatsAsync(DashboardFilter filter, CancellationToken cancellationToken = default)
    {
        var orders = await LoadFilteredDashboardOrdersAsync(filter, cancellationToken);

        return orders
            .Where(order => order.StartDate.HasValue)
            .GroupBy(order => new { order.StartDate!.Value.Year, order.StartDate.Value.Month })
            .OrderBy(group => group.Key.Year)
            .ThenBy(group => group.Key.Month)
            .Select(group => new DashboardSeriesPoint
            {
                Label = new DateTime(group.Key.Year, group.Key.Month, 1).ToString("MM.yyyy"),
                Value = group.Sum(order => order.TotalCost),
                SecondaryValue = group.Count()
            })
            .Where(point => !string.IsNullOrWhiteSpace(point.Label))
            .ToList();
    }

    private async Task<IReadOnlyList<DashboardOrderAggregate>> LoadFilteredDashboardOrdersAsync(
        DashboardFilter filter,
        CancellationToken cancellationToken)
    {
        var lines = await LoadDashboardOrderLinesAsync(cancellationToken);

        return lines
            .GroupBy(line => line.OrderId)
            .Select(group =>
            {
                var orderLines = group.ToList();
                var firstLine = orderLines[0];
                var computedTotal = orderLines.Sum(line => line.LineTotal);
                var persistedTotal = orderLines.Max(line => line.TotalCost);
                var totalCost = persistedTotal > 0 ? persistedTotal : computedTotal;

                return new DashboardOrderAggregate(
                    firstLine.OrderId,
                    firstLine.ClientName,
                    firstLine.DeliveryAddress,
                    firstLine.StartDate,
                    firstLine.EndDate,
                    firstLine.OrderStatus,
                    totalCost,
                    orderLines);
            })
            .Where(order => MatchesDashboardFilter(order, filter))
            .OrderByDescending(order => order.StartDate ?? DateTime.MinValue)
            .ThenByDescending(order => order.OrderId)
            .ToList();
    }

    private async Task<List<DashboardOrderLine>> LoadDashboardOrderLinesAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                order_id,
                c_fullname,
                delivery_address,
                start_date,
                end_date,
                order_status,
                product_title,
                fabric_type,
                color,
                product_count,
                price_per_m,
                fabric_amount,
                line_total,
                total_cost
            FROM dashboard_main_view
            ORDER BY start_date DESC, order_id DESC, product_title;
            """;

        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand(sql, connection);

        try
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var lines = new List<DashboardOrderLine>();

            while (await reader.ReadAsync(cancellationToken))
            {
                lines.Add(new DashboardOrderLine(
                    OrderId: reader.GetInt32("order_id"),
                    ClientName: reader["c_fullname"] as string ?? string.Empty,
                    DeliveryAddress: reader["delivery_address"] as string ?? string.Empty,
                    StartDate: reader["start_date"] is DBNull ? null : reader.GetDateTime("start_date"),
                    EndDate: reader["end_date"] is DBNull ? null : reader.GetDateTime("end_date"),
                    OrderStatus: reader["order_status"] as string ?? string.Empty,
                    ProductTitle: reader["product_title"] as string ?? string.Empty,
                    FabricType: reader["fabric_type"] as string ?? string.Empty,
                    Color: reader["color"] as string ?? string.Empty,
                    ProductCount: reader["product_count"] is DBNull ? 0 : Convert.ToInt32(reader["product_count"], CultureInfo.InvariantCulture),
                    PricePerM: reader["price_per_m"] is DBNull ? 0m : Convert.ToDecimal(reader["price_per_m"], CultureInfo.InvariantCulture),
                    FabricAmount: reader["fabric_amount"] is DBNull ? 0m : Convert.ToDecimal(reader["fabric_amount"], CultureInfo.InvariantCulture),
                    LineTotal: reader["line_total"] is DBNull ? 0m : Convert.ToDecimal(reader["line_total"], CultureInfo.InvariantCulture),
                    TotalCost: reader["total_cost"] is DBNull ? 0m : Convert.ToDecimal(reader["total_cost"], CultureInfo.InvariantCulture)));
            }

            return lines;
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException($"The dashboard could not load data. {exception.Message}", exception);
        }
    }

    private static bool MatchesDashboardFilter(DashboardOrderAggregate order, DashboardFilter filter)
    {
        if (filter.DateFrom.HasValue && (!order.StartDate.HasValue || order.StartDate.Value.Date < filter.DateFrom.Value.Date))
        {
            return false;
        }

        if (filter.DateTo.HasValue && (!order.StartDate.HasValue || order.StartDate.Value.Date > filter.DateTo.Value.Date))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.Status) &&
            !string.Equals(order.OrderStatus, filter.Status, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.ClientName) &&
            order.ClientName.IndexOf(filter.ClientName, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        if (filter.MinPrice.HasValue && order.TotalCost < filter.MinPrice.Value)
        {
            return false;
        }

        if (filter.MaxPrice.HasValue && order.TotalCost > filter.MaxPrice.Value)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.ProductTitle) &&
            !order.Lines.Any(line => string.Equals(line.ProductTitle, filter.ProductTitle, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.FabricType) &&
            !order.Lines.Any(line => string.Equals(line.FabricType, filter.FabricType, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return true;
    }

    private static DataTable BuildDashboardDataTable(IReadOnlyList<DashboardOrderAggregate> orders)
    {
        var table = new DataTable();
        table.Columns.Add("order_id", typeof(int));
        table.Columns.Add("c_fullname", typeof(string));
        table.Columns.Add("delivery_address", typeof(string));
        table.Columns.Add("start_date", typeof(DateTime));
        table.Columns.Add("end_date", typeof(DateTime));
        table.Columns.Add("order_status", typeof(string));
        table.Columns.Add("order_items", typeof(string));
        table.Columns.Add("total_cost", typeof(decimal));

        foreach (var order in orders)
        {
            var row = table.NewRow();
            row["order_id"] = order.OrderId;
            row["c_fullname"] = order.ClientName;
            row["delivery_address"] = order.DeliveryAddress;
            row["start_date"] = order.StartDate.HasValue ? order.StartDate.Value : DBNull.Value;
            row["end_date"] = order.EndDate.HasValue ? order.EndDate.Value : DBNull.Value;
            row["order_status"] = order.OrderStatus;
            row["order_items"] = BuildOrderItemsDisplay(order.Lines);
            row["total_cost"] = order.TotalCost;
            table.Rows.Add(row);
        }

        return table;
    }

    private static string BuildOrderItemsDisplay(IReadOnlyList<DashboardOrderLine> lines)
    {
        return string.Join(", ",
            lines
                .Where(line => !string.IsNullOrWhiteSpace(line.ProductTitle))
                .Select(line => $"{line.ProductTitle} x{line.ProductCount}"));
    }

    private async Task ExecuteStoredProcedureAsync(
        ProcedureDefinition definition,
        IReadOnlyList<object?> procedureValues,
        string entityDisplayName,
        string actionName,
        CancellationToken cancellationToken)
    {
        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand(definition.ProcedureName, connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        for (var index = 0; index < definition.ParameterNames.Count; index++)
        {
            var value = index < procedureValues.Count ? procedureValues[index] : null;
            command.Parameters.AddWithValue(definition.ParameterNames[index], value ?? DBNull.Value);
        }

        try
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var databaseMessage = await ReadProcedureMessageAsync(reader, cancellationToken);
            ValidateProcedureMessage(databaseMessage);
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException(BuildFriendlyMutationErrorMessage(exception, entityDisplayName, actionName), exception);
        }
    }

    private static string BuildFriendlyMutationErrorMessage(MySqlException exception, string entityDisplayName, string actionName)
    {
        if (exception.Number == 1062)
        {
            var keyName = TryExtractDuplicateKeyName(exception.Message);

            if (keyName.Contains("c_phone_number", StringComparison.OrdinalIgnoreCase))
            {
                return LanguageManager.CurrentLanguage == AppLanguage.Russian
                    ? "Клиент с таким номером телефона уже существует."
                    : "A client with this phone number already exists.";
            }

            if (keyName.Contains("username", StringComparison.OrdinalIgnoreCase))
            {
                return LanguageManager.CurrentLanguage == AppLanguage.Russian
                    ? "Пользователь с таким логином уже существует."
                    : "A user with this username already exists.";
            }

            return LanguageManager.CurrentLanguage == AppLanguage.Russian
                ? $"Не удалось {TranslateAction(actionName)} запись для \"{entityDisplayName}\": такое значение уже существует."
                : $"Could not {actionName} data for \"{entityDisplayName}\": this value already exists.";
        }

        return LanguageManager.CurrentLanguage == AppLanguage.Russian
            ? $"Не удалось {TranslateAction(actionName)} запись для \"{entityDisplayName}\". {exception.Message}"
            : $"Could not {actionName} data for \"{entityDisplayName}\". {exception.Message}";
    }

    private static string TryExtractDuplicateKeyName(string message)
    {
        const string marker = "for key '";
        var startIndex = message.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (startIndex < 0)
        {
            return string.Empty;
        }

        startIndex += marker.Length;
        var endIndex = message.IndexOf('\'', startIndex);
        return endIndex > startIndex
            ? message[startIndex..endIndex]
            : string.Empty;
    }

    private static string TranslateAction(string actionName)
    {
        return actionName.ToLowerInvariant() switch
        {
            "add" => "добавить",
            "update" => "обновить",
            "delete" => "удалить",
            _ => "сохранить"
        };
    }

    private static async Task ExecuteUserMutationProcedureAsync(
        MySqlCommand command,
        string fallbackMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return;
            }

            if (!HasColumn(reader, "success"))
            {
                return;
            }

            var success = reader["success"] is not DBNull && Convert.ToInt32(reader["success"], CultureInfo.InvariantCulture) == 1;
            var message = HasColumn(reader, "message")
                ? Convert.ToString(reader["message"], CultureInfo.InvariantCulture) ?? string.Empty
                : string.Empty;

            if (!success)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(message) ? fallbackMessage : message);
            }
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException($"{fallbackMessage} {exception.Message}", exception);
        }
    }

    private async Task<DataTable> LoadDisplayTableFromProcedureAsync(
        string tableName,
        ProcedureDefinition definition,
        MySqlConnection? connection,
        MySqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var ownsConnection = connection is null;
        connection ??= await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand(definition.ProcedureName, connection)
        {
            CommandType = CommandType.StoredProcedure,
            Transaction = transaction
        };

        try
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var rawTable = new DataTable();
            rawTable.Load(reader);
            var columns = await GetColumnMetadataAsync(tableName, includeAutoIncrement: true, connection, transaction, cancellationToken);
            return await BuildDisplayTableAsync(tableName, rawTable, columns, cancellationToken);
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException(LanguageManager.Format("CouldNotLoadData", GetFriendlyTableName(tableName), exception.Message), exception);
        }
        finally
        {
            if (ownsConnection)
            {
                await connection.DisposeAsync();
            }
        }
    }

    private static async Task<string?> ReadProcedureMessageAsync(System.Data.Common.DbDataReader reader, CancellationToken cancellationToken)
    {
        do
        {
            if (!reader.HasRows)
            {
                continue;
            }

            while (await reader.ReadAsync(cancellationToken))
            {
                for (var fieldIndex = 0; fieldIndex < reader.FieldCount; fieldIndex++)
                {
                    if (!reader.GetName(fieldIndex).Equals("message", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    return reader.IsDBNull(fieldIndex) ? null : reader.GetString(fieldIndex);
                }
            }
        }
        while (await reader.NextResultAsync(cancellationToken));

        return null;
    }

    private static bool HasColumn(IDataRecord record, string columnName)
    {
        for (var index = 0; index < record.FieldCount; index++)
        {
            if (record.GetName(index).Equals(columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void ValidateProcedureMessage(string? databaseMessage)
    {
        if (string.IsNullOrWhiteSpace(databaseMessage))
        {
            return;
        }

        var message = databaseMessage.Trim();
        if (message.Contains("ERROR", StringComparison.OrdinalIgnoreCase) ||
            message.StartsWith("Error", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Ошибка", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(message);
        }
    }

    private static IReadOnlyList<object?> BuildProcedureValues(
        string tableName,
        ProcedureDefinition definition,
        IReadOnlyDictionary<string, object?> values)
    {
        string[] requiredColumns = definition.ProcedureName switch
        {
            "AddCustomer" => ["customer_id", "c_fullname", "c_phone_number"],
            "AddFabric" => ["fabric_id", "fabric_type", "price_per_m", "color"],
            "AddProduct" => ["product_id", "product_title", "category_id", "fabric_amount", "fabric_id", "price_per_m", "color", "image_path"],
            "AddCustomerOrder" => ["order_id", "customer_id", "delivery_address", "start_date", "end_date", "order_status", "total_cost"],
            "AddOrderProduct" => ["order_product_id", "order_id", "product_id", "product_count"],
            "UpdateCustomer" => ["customer_id", "c_fullname", "c_phone_number"],
            "UpdateFabric" => ["fabric_id", "fabric_type", "price_per_m", "color"],
            "UpdateProduct" => ["product_id", "product_title", "category_id", "fabric_amount", "fabric_id", "price_per_m", "color", "image_path"],
            "UpdateCustomerOrder" => ["order_id", "customer_id", "delivery_address", "start_date", "end_date", "order_status", "total_cost"],
            "UpdateOrderProduct" => ["order_product_id", "product_count"],
            _ => throw new InvalidOperationException($"The stored procedure {definition.ProcedureName} is not mapped to table columns.")
        };

        return requiredColumns
            .Select(columnName =>
            {
                if (!values.TryGetValue(columnName, out var value))
                {
                    throw new InvalidOperationException(
                        $"The value '{columnName}' is required for the procedure {definition.ProcedureName}.");
                }

                return value;
            })
            .ToList();
    }

    private async Task<DataTable> BuildDisplayTableAsync(
        string tableName,
        DataTable rawTable,
        IReadOnlyList<ColumnMetadata> columns,
        CancellationToken cancellationToken)
    {
        var displayTable = new DataTable();
        displayTable.Columns.Add(GetFriendlyColumnName("NO."), typeof(int));

        var visibleColumns = new List<(DataColumn Column, string Header, IReadOnlyDictionary<string, string>? LookupMap)>();

        foreach (DataColumn rawColumn in rawTable.Columns)
        {
            var sourceColumn = displayTable.Columns.Add(BuildSourceColumnName(rawColumn.ColumnName), rawColumn.DataType);
            sourceColumn.ColumnMapping = MappingType.Hidden;

            if (rawColumn.ColumnName.Equals("image_path", StringComparison.OrdinalIgnoreCase))
            {
                var previewColumn = displayTable.Columns.Add(BuildPreviewColumnName(rawColumn.ColumnName), typeof(string));
                previewColumn.ColumnMapping = MappingType.Hidden;
            }
        }

        foreach (DataColumn column in rawTable.Columns)
        {
            var metadata = columns.FirstOrDefault(item => item.ColumnName.Equals(column.ColumnName, StringComparison.OrdinalIgnoreCase));
            if (metadata?.IsIdentifier == true && !metadata.HasLookup)
            {
                continue;
            }

            if (metadata?.HasLookup == true)
            {
                visibleColumns.Add((
                    column,
                    GetDisplayColumnName(metadata),
                    await GetLookupValueMapAsync(metadata.ReferencedTableName!, metadata.ReferencedColumnName!, cancellationToken)));
                displayTable.Columns.Add(GetDisplayColumnName(metadata), typeof(string));
                continue;
            }

            visibleColumns.Add((column, GetDisplayColumnName(column.ColumnName), null));
            displayTable.Columns.Add(
                GetDisplayColumnName(column.ColumnName),
                metadata?.IsPhone == true ? typeof(string) : column.DataType);
        }

        for (var rowIndex = 0; rowIndex < rawTable.Rows.Count; rowIndex++)
        {
            var newRow = displayTable.NewRow();
            newRow[GetFriendlyColumnName("NO.")] = rowIndex + 1;

            foreach (DataColumn rawColumn in rawTable.Columns)
            {
                newRow[BuildSourceColumnName(rawColumn.ColumnName)] = rawTable.Rows[rowIndex][rawColumn];
            }

            for (var columnIndex = 0; columnIndex < visibleColumns.Count; columnIndex++)
            {
                var column = visibleColumns[columnIndex];
                var rawValue = rawTable.Rows[rowIndex][column.Column];

                if (column.LookupMap is not null)
                {
                    var lookupKey = Convert.ToString(rawValue, CultureInfo.InvariantCulture) ?? string.Empty;
                    newRow[column.Header] = column.LookupMap.TryGetValue(lookupKey, out var label)
                        ? label
                        : string.Empty;
                    continue;
                }

                var metadata = columns.FirstOrDefault(item => item.ColumnName.Equals(column.Column.ColumnName, StringComparison.OrdinalIgnoreCase));
                if (metadata?.IsImagePath == true)
                {
                    var storedPath = Convert.ToString(rawValue, CultureInfo.InvariantCulture);
                    newRow[BuildPreviewColumnName(column.Column.ColumnName)] = ProductImageStorage.ResolveImageAbsolutePath(storedPath) ?? string.Empty;
                    newRow[column.Header] = string.IsNullOrWhiteSpace(storedPath) ? string.Empty : Path.GetFileName(storedPath);
                    continue;
                }

                newRow[column.Header] = metadata?.IsPhone == true
                    ? FormatPhoneDisplay(rawValue)
                    : rawValue;
            }

            displayTable.Rows.Add(newRow);
        }

        return displayTable;
    }

    private async Task<IReadOnlyDictionary<string, string>> GetLookupValueMapAsync(
        string tableName,
        string valueColumn,
        CancellationToken cancellationToken = default)
    {
        var options = await GetLookupOptionsAsync(tableName, valueColumn, cancellationToken);
        return options.ToDictionary(option => option.Value, option => option.Label, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyDictionary<string, object?>> LoadSingleRecordAsync(
        string sql,
        IReadOnlyList<object?> parameters,
        string entityDisplayName,
        CancellationToken cancellationToken)
    {
        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand(sql, connection);

        for (var index = 0; index < parameters.Count; index++)
        {
            command.Parameters.AddWithValue($"@value{index + 1}", parameters[index] ?? DBNull.Value);
        }

        try
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var table = new DataTable();
            table.Load(reader);

            if (table.Rows.Count == 0)
            {
                throw new InvalidOperationException($"The selected record was not found in {entityDisplayName}.");
            }

            var row = table.Rows[0];
            return table.Columns
                .Cast<DataColumn>()
                .ToDictionary(column => column.ColumnName, column => row[column], StringComparer.OrdinalIgnoreCase);
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException(LanguageManager.Format("CouldNotLoadSelectedRecord", entityDisplayName, exception.Message), exception);
        }
    }

    private async Task<IReadOnlyDictionary<string, object?>> LoadSingleRecordFromProcedureAsync(
        ProcedureDefinition definition,
        IReadOnlyList<object?> parameters,
        string entityDisplayName,
        CancellationToken cancellationToken)
    {
        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand(definition.ProcedureName, connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        for (var index = 0; index < definition.ParameterNames.Count; index++)
        {
            var value = index < parameters.Count ? parameters[index] : null;
            command.Parameters.AddWithValue(definition.ParameterNames[index], value ?? DBNull.Value);
        }

        try
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var table = new DataTable();
            table.Load(reader);

            if (table.Rows.Count == 0)
            {
                throw new InvalidOperationException($"The selected record was not found in {entityDisplayName}.");
            }

            var row = table.Rows[0];
            return table.Columns
                .Cast<DataColumn>()
                .ToDictionary(column => column.ColumnName, column => row[column], StringComparer.OrdinalIgnoreCase);
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException(LanguageManager.Format("CouldNotLoadSelectedRecord", entityDisplayName, exception.Message), exception);
        }
    }

    private async Task<IReadOnlyList<LookupOption>> LoadLookupOptionsFromProcedureAsync(
        string procedureName,
        string columnName,
        CancellationToken cancellationToken)
    {
        var table = await ExecuteSimpleTableProcedureAsync(procedureName, cancellationToken);

        return table.Rows
            .Cast<DataRow>()
            .Select(row => Convert.ToString(row[columnName], CultureInfo.InvariantCulture)?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => new LookupOption
            {
                Value = value!,
                Label = value!
            })
            .ToList();
    }

    private async Task<DataTable> ExecuteDashboardTableProcedureAsync(
        string procedureName,
        DashboardFilter filter,
        CancellationToken cancellationToken)
    {
        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        return await ExecuteDashboardTableProcedureAsync(connection, transaction: null, procedureName, filter, cancellationToken);
    }

    private async Task<DataTable> ExecuteDashboardTableProcedureAsync(
        MySqlConnection connection,
        MySqlTransaction? transaction,
        string procedureName,
        DashboardFilter filter,
        CancellationToken cancellationToken)
    {
        using var command = new MySqlCommand(procedureName, connection)
        {
            CommandType = CommandType.StoredProcedure,
            Transaction = transaction
        };

        AddDashboardParameters(command, procedureName, filter);

        try
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var table = new DataTable();
            table.Load(reader);
            return table;
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException($"The dashboard could not load data. {exception.Message}", exception);
        }
    }

    private async Task<DataTable> ExecuteSimpleTableProcedureAsync(
        string procedureName,
        CancellationToken cancellationToken)
    {
        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand(procedureName, connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        try
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var table = new DataTable();
            table.Load(reader);
            return table;
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException($"The dashboard could not load data. {exception.Message}", exception);
        }
    }

    private static DashboardSummary MapDashboardSummary(DataTable table)
    {
        if (table.Rows.Count == 0)
        {
            return new DashboardSummary();
        }

        var row = table.Rows[0];
        return new DashboardSummary
        {
            TotalSum = ReadDecimal(row, "total_sum"),
            OrderCount = ReadInt(row, "order_count"),
            AverageValue = ReadDecimal(row, "avg_value"),
            MinValue = ReadDecimal(row, "min_value"),
            MaxValue = ReadDecimal(row, "max_value")
        };
    }

    private static IReadOnlyList<DashboardSeriesPoint> MapStatusChart(DataTable table)
    {
        return table.Rows
            .Cast<DataRow>()
            .Select(row => new DashboardSeriesPoint
            {
                Label = Convert.ToString(row["order_status"], CultureInfo.InvariantCulture) ?? string.Empty,
                Value = ReadDecimal(row, "order_count"),
                SecondaryValue = ReadDecimal(row, "total_sum")
            })
            .Where(point => !string.IsNullOrWhiteSpace(point.Label) && point.Value > 0)
            .ToList();
    }

    private static IReadOnlyList<DashboardSeriesPoint> MapProductChart(DataTable table)
    {
        return table.Rows
            .Cast<DataRow>()
            .Select(row => new DashboardSeriesPoint
            {
                Label = Convert.ToString(row["product_title"], CultureInfo.InvariantCulture) ?? string.Empty,
                Value = ReadDecimal(row, "total_sum"),
                SecondaryValue = ReadDecimal(row, "total_quantity")
            })
            .Where(point => !string.IsNullOrWhiteSpace(point.Label) && point.Value > 0)
            .Take(8)
            .ToList();
    }

    private static async Task RollbackQuietlyAsync(MySqlTransaction transaction, CancellationToken cancellationToken)
    {
        try
        {
            await transaction.RollbackAsync(cancellationToken);
        }
        catch
        {
        }
    }

    private static async Task InsertLookupValueFallbackAsync(
        MySqlConnection connection,
        string tableName,
        string columnName,
        string value,
        CancellationToken cancellationToken)
    {
        using var fallbackCommand = new MySqlCommand(
            $"INSERT IGNORE INTO {EscapeIdentifier(tableName)}({EscapeIdentifier(columnName)}) VALUES (@value);",
            connection);
        fallbackCommand.Parameters.AddWithValue("@value", value);
        await fallbackCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    public static IReadOnlyDictionary<string, object?> GetSourceValues(DataRowView rowView)
    {
        return rowView.Row.Table.Columns
            .Cast<DataColumn>()
            .Where(column => column.ColumnName.StartsWith("__src_", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                column => column.ColumnName["__src_".Length..],
                column => rowView.Row[column],
                StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> GetVisibleSearchColumns(DataView? view)
    {
        if (view?.Table is null)
        {
            return Array.Empty<string>();
        }

        return view.Table.Columns
            .Cast<DataColumn>()
            .Select(column => column.ColumnName)
            .Where(column => !column.StartsWith("__src_", StringComparison.OrdinalIgnoreCase) &&
                             !column.StartsWith("__preview_", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static string GetFriendlyColumnName(string columnName)
    {
        if (columnName.Equals("NO.", StringComparison.OrdinalIgnoreCase) ||
            columnName.Equals("№", StringComparison.OrdinalIgnoreCase))
        {
            return "№";
        }

        var localizedName = LanguageManager.GetColumnName(columnName);
        if (!string.IsNullOrWhiteSpace(localizedName))
        {
            return localizedName;
        }

        return FriendlyColumnNames.TryGetValue(columnName, out var friendlyName)
            ? friendlyName
            : ToFriendlyText(columnName);
    }

    private static string GetDisplayColumnName(ColumnMetadata column)
    {
        return GetDisplayColumnName(column.ColumnName);
    }

    private static string GetDisplayColumnName(string columnName)
    {
        if (columnName.Equals("category_id", StringComparison.OrdinalIgnoreCase))
        {
            return LanguageManager.GetColumnName("category") ?? "Category";
        }

        if (columnName.Equals("customer_id", StringComparison.OrdinalIgnoreCase))
        {
            return LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Клиент" : "Customer";
        }

        if (columnName.Equals("fabric_id", StringComparison.OrdinalIgnoreCase))
        {
            return LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Ткань" : "Fabric";
        }

        if (columnName.Equals("product_id", StringComparison.OrdinalIgnoreCase))
        {
            return LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Товар" : "Product";
        }

        if (columnName.Equals("order_id", StringComparison.OrdinalIgnoreCase))
        {
            return LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Заказ" : "Order";
        }

        if (columnName.Equals("order_product_id", StringComparison.OrdinalIgnoreCase))
        {
            return LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Позиция заказа" : "Order Item";
        }

        if (columnName.EndsWith("_ID", StringComparison.OrdinalIgnoreCase))
        {
            return ToFriendlyText(columnName[..^3]);
        }

        return GetFriendlyColumnName(columnName);
    }

    private static string GetLookupDisplayColumn(IReadOnlyList<ColumnMetadata> columns, string valueColumn)
    {
        return columns
            .FirstOrDefault(column => column.ColumnName.Contains("name", StringComparison.OrdinalIgnoreCase) ||
                                      column.ColumnName.Contains("title", StringComparison.OrdinalIgnoreCase))
            ?.ColumnName
            ?? columns.FirstOrDefault(column => !column.IsIdentifier && !column.IsNumeric && !column.IsDate)?.ColumnName
            ?? columns.FirstOrDefault(column => !column.IsIdentifier && column.IsDate)?.ColumnName
            ?? columns.FirstOrDefault(column => !column.IsIdentifier)?.ColumnName
            ?? valueColumn;
    }

    private static string ToFriendlyText(string value)
    {
        var parts = value
            .Replace('_', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant());

        return string.Join(" ", parts);
    }

    private static string EscapeIdentifier(string identifier)
    {
        return $"`{identifier.Replace("`", "``", StringComparison.Ordinal)}`";
    }

    private static bool IsViewSource(string tableName)
    {
        return tableName.EndsWith("VIEW", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatPhoneDisplay(object? value)
    {
        var rawText = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        var digits = new string(rawText.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("373", StringComparison.Ordinal))
        {
            digits = digits[3..];
        }

        if (digits.Length == 0)
        {
            return "+373 ";
        }

        if (digits.Length > 8)
        {
            digits = digits[..8];
        }

        var prefix = digits[..Math.Min(2, digits.Length)];
        var middle = digits.Length > 2 ? digits.Substring(2, Math.Min(3, digits.Length - 2)) : string.Empty;
        var suffix = digits.Length > 5 ? digits.Substring(5, Math.Min(3, digits.Length - 5)) : string.Empty;

        return suffix.Length > 0
            ? $"+373 {prefix}-{middle}-{suffix}"
            : middle.Length > 0
                ? $"+373 {prefix}-{middle}"
                : $"+373 {prefix}";
    }

    private static string BuildSourceColumnName(string columnName)
    {
        return $"__src_{columnName}";
    }

    private static string BuildPreviewColumnName(string columnName)
    {
        return $"__preview_{columnName}";
    }

    private static object? GetSourceValue(IReadOnlyDictionary<string, object?> sourceValues, string columnName)
    {
        if (!sourceValues.TryGetValue(columnName, out var value))
        {
            throw new InvalidOperationException(LanguageManager.Format("MissingSourceValue", columnName));
        }

        return value is DBNull ? null : value;
    }

    private static BackupHistoryEntry MapBackupHistoryEntry(IDataRecord record)
    {
        return new BackupHistoryEntry
        {
            BackupId = record["backup_id"] is DBNull ? 0 : Convert.ToInt32(record["backup_id"], CultureInfo.InvariantCulture),
            Username = Convert.ToString(record["username"], CultureInfo.InvariantCulture) ?? string.Empty,
            OperationType = Convert.ToString(record["operation_type"], CultureInfo.InvariantCulture) ?? string.Empty,
            FileName = Convert.ToString(record["file_name"], CultureInfo.InvariantCulture) ?? string.Empty,
            FilePath = Convert.ToString(record["file_path"], CultureInfo.InvariantCulture) ?? string.Empty,
            FileSizeKb = record["file_size_kb"] is DBNull ? 0m : Convert.ToDecimal(record["file_size_kb"], CultureInfo.InvariantCulture),
            DatabaseName = Convert.ToString(record["database_name"], CultureInfo.InvariantCulture) ?? string.Empty,
            Status = Convert.ToString(record["status"], CultureInfo.InvariantCulture) ?? string.Empty,
            Message = Convert.ToString(record["message"], CultureInfo.InvariantCulture) ?? string.Empty,
            CreatedAt = record["created_at"] is DBNull
                ? DateTime.MinValue
                : Convert.ToDateTime(record["created_at"], CultureInfo.InvariantCulture)
        };
    }

    private static void AddDashboardParameters(MySqlCommand command, string procedureName, DashboardFilter filter)
    {
        object TextValue(string? value) => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
        object DateValue(DateTime? value) => value.HasValue ? value.Value : DBNull.Value;
        object NumericValue(decimal? value) => value.HasValue ? value.Value : DBNull.Value;

        command.Parameters.AddWithValue("@p_date_from", DateValue(filter.DateFrom));
        command.Parameters.AddWithValue("@p_date_to", DateValue(filter.DateTo));

        switch (procedureName)
        {
            case "GetDashboardStatusChart":
                command.Parameters.AddWithValue("@p_client_name", TextValue(filter.ClientName));
                command.Parameters.AddWithValue("@p_min_price", NumericValue(filter.MinPrice));
                command.Parameters.AddWithValue("@p_max_price", NumericValue(filter.MaxPrice));
                command.Parameters.AddWithValue("@p_product_title", TextValue(filter.ProductTitle));
                command.Parameters.AddWithValue("@p_fabric_type", TextValue(filter.FabricType));
                break;

            case "GetDashboardProductChart":
                command.Parameters.AddWithValue("@p_status", TextValue(filter.Status));
                command.Parameters.AddWithValue("@p_client_name", TextValue(filter.ClientName));
                command.Parameters.AddWithValue("@p_min_price", NumericValue(filter.MinPrice));
                command.Parameters.AddWithValue("@p_max_price", NumericValue(filter.MaxPrice));
                command.Parameters.AddWithValue("@p_fabric_type", TextValue(filter.FabricType));
                break;

            default:
                command.Parameters.AddWithValue("@p_status", TextValue(filter.Status));
                command.Parameters.AddWithValue("@p_client_name", TextValue(filter.ClientName));
                command.Parameters.AddWithValue("@p_min_price", NumericValue(filter.MinPrice));
                command.Parameters.AddWithValue("@p_max_price", NumericValue(filter.MaxPrice));
                command.Parameters.AddWithValue("@p_product_title", TextValue(filter.ProductTitle));
                command.Parameters.AddWithValue("@p_fabric_type", TextValue(filter.FabricType));
                break;
        }
    }

    private static void AddDashboardQueryParameters(MySqlCommand command, DashboardFilter filter)
    {
        object TextValue(string? value) => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
        object DateValue(DateTime? value) => value.HasValue ? value.Value : DBNull.Value;
        object NumericValue(decimal? value) => value.HasValue ? value.Value : DBNull.Value;

        command.Parameters.AddWithValue("@dateFrom", DateValue(filter.DateFrom));
        command.Parameters.AddWithValue("@dateTo", DateValue(filter.DateTo));
        command.Parameters.AddWithValue("@status", TextValue(filter.Status));
        command.Parameters.AddWithValue("@clientName", TextValue(filter.ClientName));
        command.Parameters.AddWithValue("@minPrice", NumericValue(filter.MinPrice));
        command.Parameters.AddWithValue("@maxPrice", NumericValue(filter.MaxPrice));
        command.Parameters.AddWithValue("@productTitle", TextValue(filter.ProductTitle));
        command.Parameters.AddWithValue("@fabricType", TextValue(filter.FabricType));
    }

    private static decimal ReadDecimal(DataRow row, string columnName)
    {
        return row.Table.Columns.Contains(columnName) && row[columnName] is not DBNull
            ? Convert.ToDecimal(row[columnName], CultureInfo.InvariantCulture)
            : 0m;
    }

    private static int ReadInt(DataRow row, string columnName)
    {
        return row.Table.Columns.Contains(columnName) && row[columnName] is not DBNull
            ? Convert.ToInt32(row[columnName], CultureInfo.InvariantCulture)
            : 0;
    }

    public async Task<CheckoutOrderResult> CreateCheckoutOrderAsync(
        CheckoutOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Items.Count == 0)
        {
            throw new InvalidOperationException("Add at least one product to the cart before checkout.");
        }

        if (string.IsNullOrWhiteSpace(request.CustomerFullName))
        {
            throw new InvalidOperationException("Customer name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.CustomerPhoneDigits))
        {
            throw new InvalidOperationException("Customer phone is required.");
        }

        if (string.IsNullOrWhiteSpace(request.DeliveryAddress))
        {
            throw new InvalidOperationException("Delivery address is required.");
        }

        var normalizedName = request.CustomerFullName.Trim();
        var normalizedAddress = request.DeliveryAddress.Trim();
        var normalizedStatus = string.IsNullOrWhiteSpace(request.OrderStatus) ? "Pending" : request.OrderStatus.Trim();

        if (normalizedStatus is not ("Pending" or "Shipped" or "Completed"))
        {
            throw new InvalidOperationException("Unsupported order status.");
        }

        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        var createdOrderId = 0;

        try
        {
            var phoneDigits = request.CustomerPhoneDigits.Trim();
            (int CustomerId, int OrderId) createdOrder;

            try
            {
                createdOrder = await CreateCheckoutOrderHeaderByFullNameAsync(
                    connection,
                    normalizedName,
                    phoneDigits,
                    normalizedAddress,
                    request.StartDate.Date,
                    request.EndDate.Date,
                    normalizedStatus,
                    cancellationToken);
            }
            catch (InvalidOperationException exception) when (ShouldFallbackToLocalCheckoutHeader(exception))
            {
                createdOrder = await CreateCheckoutOrderHeaderFallbackAsync(
                    connection,
                    normalizedName,
                    phoneDigits,
                    normalizedAddress,
                    request.StartDate.Date,
                    request.EndDate.Date,
                    normalizedStatus,
                    cancellationToken);
            }

            createdOrderId = createdOrder.OrderId;

            using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            foreach (var item in request.Items)
            {
                if (item.Quantity <= 0)
                {
                    throw new InvalidOperationException("Each checkout item must have a quantity greater than zero.");
                }

                var orderProductId = await GetNextIdentifierValueAsync(connection, transaction, "order_product", "order_product_id", cancellationToken);
                await InsertOrderProductCoreAsync(connection, transaction, orderProductId, createdOrder.OrderId, item.ProductId, item.Quantity, cancellationToken);
            }

            var totalCost = await GetOrderTotalAsync(connection, transaction, createdOrder.OrderId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            if (request.AccountUserId.HasValue && request.AccountUserId.Value > 0)
            {
                await LinkOrderToUserAsync(connection, request.AccountUserId.Value, createdOrder.OrderId, cancellationToken);
            }

            return new CheckoutOrderResult
            {
                CustomerId = createdOrder.CustomerId,
                OrderId = createdOrder.OrderId,
                TotalCost = totalCost
            };
        }
        catch (MySqlException exception)
        {
            if (createdOrderId > 0)
            {
                await CleanupIncompleteCheckoutOrderAsync(connection, createdOrderId, cancellationToken);
            }

            throw new InvalidOperationException($"Could not save the checkout order. {exception.Message}", exception);
        }
        catch
        {
            if (createdOrderId > 0)
            {
                await CleanupIncompleteCheckoutOrderAsync(connection, createdOrderId, cancellationToken);
            }

            throw;
        }
    }

    private static async Task LinkOrderToUserAsync(
        MySqlConnection connection,
        int userId,
        int orderId,
        CancellationToken cancellationToken)
    {
        using var command = new MySqlCommand("AddUserCustomerOrderLink", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.AddWithValue("@p_user_id", userId);
        command.Parameters.AddWithValue("@p_order_id", orderId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int?> FindCustomerIdByPhoneAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string phoneDigits,
        CancellationToken cancellationToken)
    {
        using var command = new MySqlCommand(
            """
            SELECT customer_id
            FROM customer
            WHERE c_phone_number = @phone
            ORDER BY customer_id DESC
            LIMIT 1
            FOR UPDATE;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@phone", phoneDigits);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null || result is DBNull)
        {
            return null;
        }

        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private static async Task<int?> FindCustomerIdByFullNameAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string fullName,
        CancellationToken cancellationToken)
    {
        using var command = new MySqlCommand(
            """
            SELECT customer_id
            FROM customer
            WHERE LOWER(TRIM(c_fullname)) = LOWER(TRIM(@fullName))
            ORDER BY customer_id DESC
            LIMIT 1
            FOR UPDATE;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@fullName", fullName);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null || result is DBNull)
        {
            return null;
        }

        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private static async Task<int?> FindCustomerIdByFullNameAndPhoneAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string fullName,
        string phoneDigits,
        CancellationToken cancellationToken)
    {
        using var command = new MySqlCommand(
            """
            SELECT customer_id
            FROM customer
            WHERE LOWER(TRIM(c_fullname)) = LOWER(TRIM(@fullName))
              AND c_phone_number = @phoneDigits
            ORDER BY customer_id DESC
            LIMIT 1
            FOR UPDATE;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@fullName", fullName);
        command.Parameters.AddWithValue("@phoneDigits", phoneDigits);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null || result is DBNull)
        {
            return null;
        }

        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private static async Task<string?> GetCustomerPhoneAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int customerId,
        CancellationToken cancellationToken)
    {
        using var command = new MySqlCommand(
            """
            SELECT c_phone_number
            FROM customer
            WHERE customer_id = @customerId
            LIMIT 1
            FOR UPDATE;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@customerId", customerId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull
            ? null
            : Convert.ToString(result, CultureInfo.InvariantCulture)?.Trim();
    }

    private static async Task<bool> IsPhoneAssignedToAnotherCustomerAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int customerId,
        string phoneDigits,
        CancellationToken cancellationToken)
    {
        using var command = new MySqlCommand(
            """
            SELECT 1
            FROM customer
            WHERE c_phone_number = @phoneDigits
              AND customer_id <> @customerId
            LIMIT 1
            FOR UPDATE;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@phoneDigits", phoneDigits);
        command.Parameters.AddWithValue("@customerId", customerId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null and not DBNull;
    }

    private static async Task<int> GetNextIdentifierValueAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string tableName,
        string columnName,
        CancellationToken cancellationToken)
    {
        using var command = new MySqlCommand(
            $"SELECT COALESCE(MAX({EscapeIdentifier(columnName)}), 0) + 1 FROM {EscapeIdentifier(tableName)};",
            connection,
            transaction);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private static async Task InsertCustomerCoreAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int customerId,
        string fullName,
        string phoneDigits,
        CancellationToken cancellationToken)
    {
        using var command = new MySqlCommand(
            """
            INSERT INTO customer (customer_id, c_fullname, c_phone_number)
            VALUES (@customerId, @fullName, @phoneDigits);
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@customerId", customerId);
        command.Parameters.AddWithValue("@fullName", fullName);
        command.Parameters.AddWithValue("@phoneDigits", phoneDigits);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpdateCustomerCoreAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int customerId,
        string fullName,
        string phoneDigits,
        CancellationToken cancellationToken)
    {
        using var command = new MySqlCommand(
            """
            UPDATE customer
            SET c_fullname = @fullName,
                c_phone_number = @phoneDigits
            WHERE customer_id = @customerId;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@customerId", customerId);
        command.Parameters.AddWithValue("@fullName", fullName);
        command.Parameters.AddWithValue("@phoneDigits", phoneDigits);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertCustomerOrderCoreAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int orderId,
        int customerId,
        string deliveryAddress,
        DateTime startDate,
        DateTime endDate,
        string orderStatus,
        CancellationToken cancellationToken)
    {
        using var command = new MySqlCommand(
            """
            INSERT INTO customer_order (order_id, customer_id, delivery_address, start_date, end_date, order_status, total_cost)
            VALUES (@orderId, @customerId, @deliveryAddress, @startDate, @endDate, @orderStatus, 0);
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@orderId", orderId);
        command.Parameters.AddWithValue("@customerId", customerId);
        command.Parameters.AddWithValue("@deliveryAddress", deliveryAddress);
        command.Parameters.AddWithValue("@startDate", startDate);
        command.Parameters.AddWithValue("@endDate", endDate);
        command.Parameters.AddWithValue("@orderStatus", orderStatus);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertOrderProductCoreAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int orderProductId,
        int orderId,
        int productId,
        int quantity,
        CancellationToken cancellationToken)
    {
        using var command = new MySqlCommand(
            """
            INSERT INTO order_product (order_product_id, order_id, product_id, product_count)
            VALUES (@orderProductId, @orderId, @productId, @quantity);
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@orderProductId", orderProductId);
        command.Parameters.AddWithValue("@orderId", orderId);
        command.Parameters.AddWithValue("@productId", productId);
        command.Parameters.AddWithValue("@quantity", quantity);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<decimal> GetOrderTotalAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int orderId,
        CancellationToken cancellationToken)
    {
        using var command = new MySqlCommand(
            """
            SELECT COALESCE(total_cost, 0)
            FROM customer_order
            WHERE order_id = @orderId
            LIMIT 1;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@orderId", orderId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull
            ? 0m
            : Convert.ToDecimal(result, CultureInfo.InvariantCulture);
    }

    private static bool ShouldFallbackToLocalCheckoutHeader(InvalidOperationException exception)
    {
        var message = exception.Message ?? string.Empty;
        return message.Contains("ERROR: Could not create order.", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("invalid identifiers", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("did not return a result", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<(int CustomerId, int OrderId)> CreateCheckoutOrderHeaderFallbackAsync(
        MySqlConnection connection,
        string fullName,
        string phoneDigits,
        string deliveryAddress,
        DateTime startDate,
        DateTime endDate,
        string orderStatus,
        CancellationToken cancellationToken)
    {
        using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var customerId =
            await FindCustomerIdByFullNameAndPhoneAsync(connection, transaction, fullName, phoneDigits, cancellationToken) ??
            await FindCustomerIdByFullNameAsync(connection, transaction, fullName, cancellationToken);

        if (!customerId.HasValue)
        {
            customerId = await FindCustomerIdByPhoneAsync(connection, transaction, phoneDigits, cancellationToken);
        }

        if (!customerId.HasValue)
        {
            customerId = await GetNextIdentifierValueAsync(connection, transaction, "customer", "customer_id", cancellationToken);
            await InsertCustomerCoreAsync(connection, transaction, customerId.Value, fullName, phoneDigits, cancellationToken);
        }
        else
        {
            var currentPhone = await GetCustomerPhoneAsync(connection, transaction, customerId.Value, cancellationToken) ?? string.Empty;
            var phoneBelongsToAnotherCustomer = !string.IsNullOrWhiteSpace(phoneDigits) &&
                                                await IsPhoneAssignedToAnotherCustomerAsync(connection, transaction, customerId.Value, phoneDigits, cancellationToken);

            var safePhoneToSave = phoneBelongsToAnotherCustomer
                ? currentPhone
                : phoneDigits;

            await UpdateCustomerCoreAsync(connection, transaction, customerId.Value, fullName, safePhoneToSave, cancellationToken);
        }

        var orderId = await GetNextIdentifierValueAsync(connection, transaction, "customer_order", "order_id", cancellationToken);
        await InsertCustomerOrderCoreAsync(connection, transaction, orderId, customerId.Value, deliveryAddress, startDate, endDate, orderStatus, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return (customerId.Value, orderId);
    }

    private static async Task<(int CustomerId, int OrderId)> CreateCheckoutOrderHeaderByFullNameAsync(
        MySqlConnection connection,
        string fullName,
        string phoneDigits,
        string deliveryAddress,
        DateTime startDate,
        DateTime endDate,
        string orderStatus,
        CancellationToken cancellationToken)
    {
        using var command = new MySqlCommand("CreateCheckoutOrderByFullName", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.AddWithValue("@p_full_name", fullName);
        command.Parameters.AddWithValue("@p_phone_digits", phoneDigits);
        command.Parameters.AddWithValue("@p_delivery_address", deliveryAddress);
        command.Parameters.AddWithValue("@p_start_date", startDate);
        command.Parameters.AddWithValue("@p_end_date", endDate);
        command.Parameters.AddWithValue("@p_order_status", orderStatus);

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("The checkout procedure did not return a result.");
        }

        var success = Convert.ToInt32(reader["success"], CultureInfo.InvariantCulture) == 1;
        var message = Convert.ToString(reader["message"], CultureInfo.InvariantCulture) ?? string.Empty;
        if (!success)
        {
            throw new InvalidOperationException(message);
        }

        var customerId = reader["customer_id"] is DBNull
            ? 0
            : Convert.ToInt32(reader["customer_id"], CultureInfo.InvariantCulture);
        var orderId = reader["order_id"] is DBNull
            ? 0
            : Convert.ToInt32(reader["order_id"], CultureInfo.InvariantCulture);

        if (customerId <= 0 || orderId <= 0)
        {
            throw new InvalidOperationException("The checkout procedure returned invalid identifiers.");
        }

        return (customerId, orderId);
    }

    private static async Task CleanupIncompleteCheckoutOrderAsync(
        MySqlConnection connection,
        int orderId,
        CancellationToken cancellationToken)
    {
        try
        {
            using var deleteItemsCommand = new MySqlCommand(
                """
                DELETE FROM order_product
                WHERE order_id = @orderId;
                """,
                connection);
            deleteItemsCommand.Parameters.AddWithValue("@orderId", orderId);
            await deleteItemsCommand.ExecuteNonQueryAsync(cancellationToken);

            using var deleteOrderCommand = new MySqlCommand(
                """
                DELETE FROM customer_order
                WHERE order_id = @orderId;
                """,
                connection);
            deleteOrderCommand.Parameters.AddWithValue("@orderId", orderId);
            await deleteOrderCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        catch
        {
            // Intentionally ignore cleanup errors to preserve the original checkout exception.
        }
    }

    public async Task<OrderDetailsViewModel?> GetOrderDetailsAsync(int orderId, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand(
            """
            SELECT
                co.order_id,
                co.customer_id,
                COALESCE(c.c_fullname, '') AS c_fullname,
                COALESCE(co.delivery_address, '') AS delivery_address,
                co.start_date,
                co.end_date,
                COALESCE(co.order_status, '') AS order_status,
                COALESCE(co.total_cost, 0) AS total_cost,
                COALESCE(op.product_id, 0) AS product_id,
                COALESCE(p.product_title, '') AS product_title,
                COALESCE(cat.category_name, '') AS category_name,
                COALESCE(f.fabric_type, '') AS fabric_type,
                COALESCE(p.color, '') AS color,
                COALESCE(p.image_path, '') AS image_path,
                COALESCE(op.product_count, 0) AS product_count,
                COALESCE(p.price_per_m, 0) * COALESCE(p.fabric_amount, 0) AS unit_price,
                COALESCE(op.product_count, 0) * COALESCE(p.price_per_m, 0) * COALESCE(p.fabric_amount, 0) AS line_total
            FROM customer_order co
            INNER JOIN customer c ON c.customer_id = co.customer_id
            LEFT JOIN order_product op ON op.order_id = co.order_id
            LEFT JOIN productt p ON p.product_id = op.product_id
            LEFT JOIN category cat ON cat.category_id = p.category_id
            LEFT JOIN fabric f ON f.fabric_id = p.fabric_id
            WHERE co.order_id = @orderId
            ORDER BY op.order_product_id, p.product_title;
            """,
            connection);
        command.Parameters.AddWithValue("@orderId", orderId);

        try
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            OrderDetailsViewModel? order = null;
            var items = new List<OrderDetailItemViewModel>();

            while (await reader.ReadAsync(cancellationToken))
            {
                if (order is null)
                {
                    order = new OrderDetailsViewModel
                    {
                        OrderId = reader.GetInt32("order_id"),
                        CustomerId = reader.GetInt32("customer_id"),
                        ClientName = Convert.ToString(reader["c_fullname"], CultureInfo.InvariantCulture) ?? string.Empty,
                        DeliveryAddress = Convert.ToString(reader["delivery_address"], CultureInfo.InvariantCulture) ?? string.Empty,
                        StartDate = reader.IsDBNull(reader.GetOrdinal("start_date")) ? null : reader.GetDateTime("start_date"),
                        EndDate = reader.IsDBNull(reader.GetOrdinal("end_date")) ? null : reader.GetDateTime("end_date"),
                        OrderStatus = Convert.ToString(reader["order_status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        TotalCost = Convert.ToDecimal(reader["total_cost"], CultureInfo.InvariantCulture)
                    };
                }

                var productId = Convert.ToInt32(reader["product_id"], CultureInfo.InvariantCulture);
                if (productId <= 0)
                {
                    continue;
                }

                items.Add(new OrderDetailItemViewModel
                {
                    ProductId = productId,
                    ProductTitle = Convert.ToString(reader["product_title"], CultureInfo.InvariantCulture) ?? string.Empty,
                    CategoryName = Convert.ToString(reader["category_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                    FabricType = Convert.ToString(reader["fabric_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Color = Convert.ToString(reader["color"], CultureInfo.InvariantCulture) ?? string.Empty,
                    ImagePath = StorefrontAssetResolver.ResolveProductImagePath(Convert.ToString(reader["image_path"], CultureInfo.InvariantCulture)),
                    Quantity = Convert.ToInt32(reader["product_count"], CultureInfo.InvariantCulture),
                    UnitPrice = Convert.ToDecimal(reader["unit_price"], CultureInfo.InvariantCulture),
                    LineTotal = Convert.ToDecimal(reader["line_total"], CultureInfo.InvariantCulture)
                });
            }

            if (order is null)
            {
                return null;
            }

            order.Items = items;
            return order;
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException(LanguageManager.Format("CouldNotLoadData", GetFriendlyTableName("CUSTOMER_ORDER"), exception.Message), exception);
        }
    }

    public async Task<IReadOnlyList<UserOrderSummaryViewModel>> GetCustomerOrdersByIdentityAsync(
        string? customerFullName,
        string? phoneDigits,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = customerFullName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return Array.Empty<UserOrderSummaryViewModel>();
        }

        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand("GetCustomerOrdersByFullName", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("@p_full_name", normalizedName);

        var orders = new List<UserOrderSummaryViewModel>();

        try
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                orders.Add(new UserOrderSummaryViewModel
                {
                    OrderId = Convert.ToInt32(reader["order_id"], CultureInfo.InvariantCulture),
                    OrderDate = reader["order_date"] is DBNull ? null : Convert.ToDateTime(reader["order_date"], CultureInfo.InvariantCulture),
                    OrderStatus = Convert.ToString(reader["order_status"], CultureInfo.InvariantCulture) ?? string.Empty,
                    TotalCost = Convert.ToDecimal(reader["total_cost"], CultureInfo.InvariantCulture)
                });
            }
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException($"Could not load customer orders. {exception.Message}", exception);
        }

        return orders;
    }

    public async Task<IReadOnlyList<UserOrderSummaryViewModel>> GetCustomerOrdersByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return Array.Empty<UserOrderSummaryViewModel>();
        }

        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand("GetUserCustomerOrders", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("@p_user_id", userId);

        var orders = new List<UserOrderSummaryViewModel>();

        try
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                orders.Add(new UserOrderSummaryViewModel
                {
                    OrderId = Convert.ToInt32(reader["order_id"], CultureInfo.InvariantCulture),
                    OrderDate = reader["order_date"] is DBNull ? null : Convert.ToDateTime(reader["order_date"], CultureInfo.InvariantCulture),
                    OrderStatus = Convert.ToString(reader["order_status"], CultureInfo.InvariantCulture) ?? string.Empty,
                    TotalCost = Convert.ToDecimal(reader["total_cost"], CultureInfo.InvariantCulture)
                });
            }
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException($"Could not load user orders. {exception.Message}", exception);
        }

        return orders;
    }

    public async Task<IReadOnlyList<ProductCardViewModel>> GetProductsForCustomerCatalogAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand(
            """
            SELECT
                p.product_id,
                p.product_title,
                COALESCE(c.category_name, '') AS category_name,
                COALESCE(f.fabric_type, '') AS fabric_type,
                COALESCE(p.color, '') AS color,
                COALESCE(p.fabric_amount, 0) AS fabric_amount,
                COALESCE(p.price_per_m, 0) AS price_per_m,
                COALESCE(p.image_path, '') AS image_path
            FROM productt p
            LEFT JOIN category c ON p.category_id = c.category_id
            LEFT JOIN fabric f ON p.fabric_id = f.fabric_id
            ORDER BY p.product_id;
            """,
            connection);

        var products = new List<ProductCardViewModel>();

        try
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                products.Add(new ProductCardViewModel
                {
                    ProductId = reader.GetInt32("product_id"),
                    ProductTitle = Convert.ToString(reader["product_title"], CultureInfo.InvariantCulture) ?? string.Empty,
                    CategoryName = Convert.ToString(reader["category_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                    FabricType = Convert.ToString(reader["fabric_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Color = Convert.ToString(reader["color"], CultureInfo.InvariantCulture) ?? string.Empty,
                    FabricAmount = Convert.ToDecimal(reader["fabric_amount"], CultureInfo.InvariantCulture),
                    PricePerM = Convert.ToDecimal(reader["price_per_m"], CultureInfo.InvariantCulture),
                    ImagePath = Convert.ToString(reader["image_path"], CultureInfo.InvariantCulture) ?? string.Empty
                });
            }
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException(LanguageManager.Format("CouldNotLoadData", GetFriendlyTableName("PRODUCTT"), exception.Message), exception);
        }

        return products;
    }

    public async Task<IReadOnlySet<int>> GetCatalogProductIdsWithPhotosAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand("GetCatalogProductsWithPhotos", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        var productIds = new HashSet<int>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            productIds.Add(Convert.ToInt32(reader["product_id"], CultureInfo.InvariantCulture));
        }

        return productIds;
    }

    public async Task<IReadOnlySet<int>> GetCatalogHitProductIdsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand("GetCatalogHitProducts", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        var productIds = new HashSet<int>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            productIds.Add(Convert.ToInt32(reader["product_id"], CultureInfo.InvariantCulture));
        }

        return productIds;
    }

    public async Task<IReadOnlySet<int>> GetCatalogAffordableProductIdsAsync(decimal maxTotalPrice, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand("GetCatalogProductsUnderPrice", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("@p_max_total_price", maxTotalPrice);

        var productIds = new HashSet<int>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            productIds.Add(Convert.ToInt32(reader["product_id"], CultureInfo.InvariantCulture));
        }

        return productIds;
    }

    public async Task<IReadOnlySet<int>> GetCatalogMuslinProductIdsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand("GetCatalogMuslinProducts", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        var productIds = new HashSet<int>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            productIds.Add(Convert.ToInt32(reader["product_id"], CultureInfo.InvariantCulture));
        }

        return productIds;
    }

    public async Task<IReadOnlySet<int>> GetFavoriteProductIdsAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return new HashSet<int>();
        }

        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand("GetUserFavoriteProducts", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("@p_user_id", userId);

        var favoriteIds = new HashSet<int>();

        try
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                favoriteIds.Add(Convert.ToInt32(reader["product_id"], CultureInfo.InvariantCulture));
            }
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException($"Could not load favorites. {exception.Message}", exception);
        }

        return favoriteIds;
    }

    public async Task SetFavoriteProductStateAsync(int userId, int productId, bool isFavorite, CancellationToken cancellationToken = default)
    {
        if (userId <= 0 || productId <= 0)
        {
            return;
        }

        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand(isFavorite ? "AddUserFavoriteProduct" : "RemoveUserFavoriteProduct", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.AddWithValue("@p_user_id", userId);
        command.Parameters.AddWithValue("@p_product_id", productId);

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException($"Could not update favorites. {exception.Message}", exception);
        }
    }

    public async Task ClearFavoriteProductsAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return;
        }

        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand("ClearUserFavoriteProducts", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.AddWithValue("@p_user_id", userId);

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException($"Could not clear favorites. {exception.Message}", exception);
        }
    }

    public async Task<IReadOnlyList<CategoryFilterViewModel>> GetCustomerCatalogCategoriesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbHelper.CreateOpenConnectionAsync(cancellationToken);
        using var command = new MySqlCommand(
            """
            SELECT
                c.category_id,
                c.category_name,
                COUNT(p.product_id) AS product_count
            FROM category c
            LEFT JOIN productt p ON p.category_id = c.category_id
            GROUP BY c.category_id, c.category_name
            ORDER BY c.category_name;
            """,
            connection);

        var categories = new List<CategoryFilterViewModel>();

        try
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                categories.Add(new CategoryFilterViewModel
                {
                    CategoryId = reader.GetInt32("category_id"),
                    CategoryName = Convert.ToString(reader["category_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                    ProductCount = Convert.ToInt32(reader["product_count"], CultureInfo.InvariantCulture)
                });
            }
        }
        catch (MySqlException exception)
        {
            throw new InvalidOperationException(LanguageManager.Format("CouldNotLoadLookup", GetFriendlyTableName("CATEGORY"), exception.Message), exception);
        }

        return categories;
    }
}

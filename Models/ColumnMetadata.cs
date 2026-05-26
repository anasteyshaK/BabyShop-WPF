namespace BabyShop.Models;

public sealed class ColumnMetadata
{
    private static readonly HashSet<string> WholeNumberTypes =
    [
        "int",
        "integer",
        "smallint",
        "mediumint",
        "bigint",
        "tinyint"
    ];

    private static readonly HashSet<string> DecimalTypes =
    [
        "decimal",
        "numeric",
        "double",
        "float",
        "real"
    ];

    private static readonly HashSet<string> DateTypes =
    [
        "date",
        "datetime",
        "timestamp"
    ];

    public required string ColumnName { get; init; }
    public required string DisplayName { get; init; }
    public required string DataType { get; init; }
    public bool IsNullable { get; init; }
    public bool IsAutoIncrement { get; init; }
    public string? ReferencedTableName { get; init; }
    public string? ReferencedColumnName { get; init; }

    public bool IsNumeric => WholeNumberTypes.Contains(DataType) || DecimalTypes.Contains(DataType);
    public bool IsWholeNumber => WholeNumberTypes.Contains(DataType);
    public bool IsDate => DateTypes.Contains(DataType);
    public bool HasLookup => !string.IsNullOrWhiteSpace(ReferencedTableName) && !string.IsNullOrWhiteSpace(ReferencedColumnName);
    public bool IsPhone => ColumnName.Contains("phone", StringComparison.OrdinalIgnoreCase);
    public bool IsAddress => ColumnName.Contains("address", StringComparison.OrdinalIgnoreCase);
    public bool IsColor => ColumnName.Contains("color", StringComparison.OrdinalIgnoreCase);
    public bool IsImagePath => ColumnName.Equals("image_path", StringComparison.OrdinalIgnoreCase);
    public bool IsOrderStatus => ColumnName.Contains("order_status", StringComparison.OrdinalIgnoreCase);
    public bool IsComputedOrderTotal => ColumnName.Equals("total_cost", StringComparison.OrdinalIgnoreCase);
    public bool IsIdentifier => ColumnName.EndsWith("_ID", StringComparison.OrdinalIgnoreCase);
}

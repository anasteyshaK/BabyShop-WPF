namespace BabyShop.Reporting;

public sealed record ReportTableDefinition(
    string SourceTableName,
    string QueryTableName,
    string LabelKey);

public static class ReportCatalog
{
    public static IReadOnlyList<ReportTableDefinition> TableDefinitions { get; } =
    [
        new("CUSTOMER", "CUSTOMERVIEW", "Customers"),
        new("FABRIC", "FABRICVIEW", "Fabrics"),
        new("PRODUCTT", "PRODUCTFABRICVIEW", "Products"),
        new("CUSTOMER_ORDER", "CUSTOMERORDERSVIEW", "Orders"),
        new("ORDER_PRODUCT", "ORDERPRODUCTVIEW", "OrderItems")
    ];

    public static ReportTableDefinition GetDefaultTable(string? sourceTableName)
    {
        return FindDefinition(sourceTableName) ?? TableDefinitions[0];
    }

    public static ReportTableDefinition GetRequiredTable(string? sourceTableName)
    {
        return FindDefinition(sourceTableName)
               ?? throw new InvalidOperationException($"The report source '{sourceTableName}' is not supported.");
    }

    public static ReportTableDefinition? FindDefinition(string? sourceTableName)
    {
        if (string.IsNullOrWhiteSpace(sourceTableName))
        {
            return null;
        }

        return TableDefinitions.FirstOrDefault(
            definition => definition.SourceTableName.Equals(sourceTableName, StringComparison.OrdinalIgnoreCase));
    }
}

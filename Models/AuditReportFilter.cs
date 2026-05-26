namespace BabyShop.Models;

public sealed class AuditReportFilter
{
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }
    public string? Username { get; init; }
    public string? ActionType { get; init; }
}

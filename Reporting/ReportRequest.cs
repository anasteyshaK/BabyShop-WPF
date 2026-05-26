using BabyShop.Models;

namespace BabyShop.Reporting;

public sealed class ReportRequest
{
    public required ReportKind Kind { get; init; }

    public string? SourceTableName { get; init; }

    public DashboardFilter? Filter { get; init; }

    public AuditReportFilter? AuditFilter { get; init; }
}

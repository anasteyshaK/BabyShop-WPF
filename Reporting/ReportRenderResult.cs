using System.Windows.Documents;

namespace BabyShop.Reporting;

public sealed class ReportRenderResult
{
    public required string Title { get; init; }

    public required string Subtitle { get; init; }

    public required string SuggestedFileName { get; init; }

    public required FlowDocument Document { get; init; }

    public required string HtmlContent { get; init; }
}

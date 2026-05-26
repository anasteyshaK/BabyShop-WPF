using System.Data;
using System.Globalization;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using BabyShop.Localization;
using BabyShop.Models;
using BabyShop.Repositories;

namespace BabyShop.Reporting;

public sealed class ReportComposer
{
    private sealed record AnalyticsOverview(
        string PeriodLabel,
        decimal TotalRevenue,
        int TotalOrders,
        decimal AverageOrder,
        int CustomersCount,
        int CompletedOrders,
        int ShippedOrders,
        int PendingOrders,
        string MostPopularProduct,
        decimal MostPopularProductQuantity,
        string MostProfitableProduct,
        decimal MostProfitableProductRevenue,
        string Conclusion);

    private sealed record AnalyticsMetricRow(string Indicator, string Value, string Description);

    private static readonly Brush AccentBrush = new SolidColorBrush(Color.FromRgb(215, 125, 145));
    private static readonly Brush AccentSoftBrush = new SolidColorBrush(Color.FromRgb(255, 231, 238));
    private static readonly Brush BorderBrush = new SolidColorBrush(Color.FromRgb(232, 222, 212));
    private static readonly Brush TextBrush = new SolidColorBrush(Color.FromRgb(55, 64, 58));
    private static readonly Brush MutedBrush = new SolidColorBrush(Color.FromRgb(123, 129, 122));
    private static readonly Brush SurfaceBrush = Brushes.White;
    private static readonly FontFamily ReportFont = new("Nunito, Quicksand, Poppins, Trebuchet MS, Segoe UI Variable, Segoe UI");

    private readonly BabyShopRepository _repository;

    public ReportComposer(BabyShopRepository repository)
    {
        _repository = repository;
    }

    public async Task<ReportRenderResult> BuildAsync(ReportRequest request, CancellationToken cancellationToken = default)
    {
        return request.Kind switch
        {
            ReportKind.AllRecords => await BuildAllRecordsReportAsync(request, cancellationToken),
            ReportKind.FilteredData => await BuildFilteredReportAsync(request, cancellationToken),
            ReportKind.Analytics => await BuildAnalyticsReportAsync(request, cancellationToken),
            ReportKind.Audit => await BuildAuditReportAsync(request, cancellationToken),
            _ => throw new InvalidOperationException("The selected report type is not supported.")
        };
    }

    private async Task<ReportRenderResult> BuildAllRecordsReportAsync(ReportRequest request, CancellationToken cancellationToken)
    {
        var tableDefinition = ReportCatalog.GetRequiredTable(request.SourceTableName);
        var table = await _repository.GetReportDisplayTableAsync(tableDefinition.QueryTableName, cancellationToken);
        var visibleColumns = GetVisibleColumns(table);
        var entityName = LanguageManager.Get(tableDefinition.LabelKey);
        var reportTitle = LanguageManager.Get("ReportAllRecordsTitle");
        var subtitle = LanguageManager.Format("ReportAllRecordsSubtitle", entityName);
        var rowCount = table.Rows.Count.ToString("N0", CultureInfo.CurrentCulture);

        var document = CreateBaseDocument(reportTitle, subtitle);
        AddMetaFacts(
            document,
            LanguageManager.Get("ReportSectionSummary"),
            [
                (LanguageManager.Get("ReportSourceTable"), entityName),
                (LanguageManager.Get("ReportRowsCount"), rowCount),
                (LanguageManager.Get("ReportGeneratedAt"), DateTime.Now.ToString("g", CultureInfo.CurrentCulture))
            ]);
        AddDataSection(document, LanguageManager.Get("ReportSectionRecords"), table, visibleColumns);

        var html = BuildHtmlShell(
            reportTitle,
            subtitle,
            BuildSummaryCardsHtml(
            [
                (LanguageManager.Get("ReportSourceTable"), entityName),
                (LanguageManager.Get("ReportRowsCount"), rowCount),
                (LanguageManager.Get("ReportGeneratedAt"), DateTime.Now.ToString("g", CultureInfo.CurrentCulture))
            ]) +
            BuildTableSectionHtml(LanguageManager.Get("ReportSectionRecords"), table, visibleColumns));

        return new ReportRenderResult
        {
            Title = reportTitle,
            Subtitle = subtitle,
            SuggestedFileName = BuildFileName("all-records", entityName),
            Document = document,
            HtmlContent = html
        };
    }

    private async Task<ReportRenderResult> BuildFilteredReportAsync(ReportRequest request, CancellationToken cancellationToken)
    {
        var filter = request.Filter ?? new DashboardFilter();
        var snapshot = await _repository.GetFilteredReportSnapshotAsync(filter, cancellationToken);
        var table = snapshot.Data;
        var summary = snapshot.Summary;
        var visibleColumns = GetVisibleColumns(table, hideEmptyColumns: true);
        var reportTitle = LanguageManager.Get("ReportFilteredTitle");
        var subtitle = LanguageManager.Get("ReportFilteredSubtitle");
        var periodLabel = ResolvePeriodLabel(filter, table);
        var cards =
            new (string Label, string Value)[]
            {
                (LanguageManager.Get("DashboardTotalSum"), FormatMdl(summary.TotalSum)),
                (LanguageManager.Get("DashboardOrderCount"), summary.OrderCount.ToString("N0", CultureInfo.CurrentCulture)),
                (LanguageManager.Get("DashboardAverage"), FormatMdl(summary.AverageValue)),
                (LanguageManager.Get("DashboardRange"), $"{FormatMdl(summary.MinValue)} - {FormatMdl(summary.MaxValue)}")
            };

        var document = CreateFilteredReferenceDocument(reportTitle, subtitle, periodLabel, cards, table, visibleColumns);
        var html = BuildFilteredReferenceHtml(reportTitle, subtitle, periodLabel, cards, table, visibleColumns);

        return new ReportRenderResult
        {
            Title = reportTitle,
            Subtitle = subtitle,
            SuggestedFileName = BuildFileName("filtered-report", "orders"),
            Document = document,
            HtmlContent = html
        };
    }

    private async Task<ReportRenderResult> BuildAnalyticsReportAsync(ReportRequest request, CancellationToken cancellationToken)
    {
        var filter = request.Filter ?? new DashboardFilter();
        var snapshot = await _repository.GetAnalyticsReportSnapshotAsync(filter, cancellationToken);
        var data = snapshot.Data;
        var summary = snapshot.Summary;
        var statusPoints = snapshot.StatusPoints;
        var productPoints = snapshot.ProductPoints;

        var overview = BuildAnalyticsOverview(filter, data, summary, statusPoints, productPoints);
        var metricRows = BuildAnalyticsMetricRows(overview);

        var reportTitle = LanguageManager.Get("ReportAnalyticsHeroTitle");
        var subtitle = LanguageManager.Get("ReportAnalyticsHeroSubtitle");
        var document = CreateAnalyticsReferenceDocument(overview, metricRows);
        var html = BuildAnalyticsReferenceHtml(overview, metricRows);

        return new ReportRenderResult
        {
            Title = reportTitle,
            Subtitle = subtitle,
            SuggestedFileName = BuildFileName("analytics-report", "dashboard"),
            Document = document,
            HtmlContent = html
        };
    }

    private async Task<ReportRenderResult> BuildAuditReportAsync(ReportRequest request, CancellationToken cancellationToken)
    {
        var filter = request.AuditFilter ?? new AuditReportFilter();
        var snapshot = await _repository.GetAuditReportSnapshotAsync(filter, cancellationToken);
        var table = snapshot.Data;
        var summary = snapshot.Summary;
        var visibleColumns = GetVisibleColumns(table, hideEmptyColumns: true);
        var reportTitle = LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Журнал аудита" : "Audit Log";
        var subtitle = LanguageManager.CurrentLanguage == AppLanguage.Russian
            ? "Действия пользователей, входы в систему и изменения данных."
            : "User actions, sign-ins, and tracked data changes.";
        var filters = DescribeAuditFilter(filter);
        var cards =
            new (string Label, string Value)[]
            {
                (LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Всего действий" : "Total Actions", summary.TotalActions.ToString("N0", CultureInfo.CurrentCulture)),
                (LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Успешные входы" : "Successful Logins", summary.SuccessfulLogins.ToString("N0", CultureInfo.CurrentCulture)),
                (LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Неудачные входы" : "Failed Logins", summary.FailedLogins.ToString("N0", CultureInfo.CurrentCulture)),
                (LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Регистрации" : "Registrations", summary.Registrations.ToString("N0", CultureInfo.CurrentCulture)),
                (LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Активные пользователи" : "Active Users", summary.ActiveUsers.ToString("N0", CultureInfo.CurrentCulture))
            };

        var document = CreateBaseDocument(reportTitle, subtitle);
        AddMetricCards(document, cards);
        AddFilterChips(document, filters);
        AddDataSection(document, LanguageManager.Get("ReportSectionRecords"), table, visibleColumns);

        var html = BuildHtmlShell(
            reportTitle,
            subtitle,
            BuildSummaryCardsHtml(cards) +
            BuildFilterChipSectionHtml(filters) +
            BuildTableSectionHtml(LanguageManager.Get("ReportSectionRecords"), table, visibleColumns));

        return new ReportRenderResult
        {
            Title = reportTitle,
            Subtitle = subtitle,
            SuggestedFileName = BuildFileName("audit-report", "audit-log"),
            Document = document,
            HtmlContent = html
        };
    }

    private static AnalyticsOverview BuildAnalyticsOverview(
        DashboardFilter filter,
        DataTable data,
        DashboardSummary summary,
        IReadOnlyList<DashboardSeriesPoint> statusPoints,
        IReadOnlyList<DashboardSeriesPoint> productPoints)
    {
        var periodLabel = ResolvePeriodLabel(filter, data);
        var completedOrders = GetStatusCount(statusPoints, "Completed");
        var shippedOrders = GetStatusCount(statusPoints, "Shipped");
        var pendingOrders = GetStatusCount(statusPoints, "Pending");
        var customersCount = GetDistinctCount(data, "c_fullname");
        var topPopularProduct = productPoints
            .OrderByDescending(point => point.SecondaryValue)
            .ThenByDescending(point => point.Value)
            .FirstOrDefault();
        var topProfitableProduct = productPoints
            .OrderByDescending(point => point.Value)
            .ThenByDescending(point => point.SecondaryValue)
            .FirstOrDefault();

        var conclusion = BuildAnalyticsConclusion(
            summary.TotalSum,
            completedOrders,
            summary.OrderCount,
            topPopularProduct?.Label ?? LanguageManager.Get("ReportNoData"));

        return new AnalyticsOverview(
            periodLabel,
            summary.TotalSum,
            summary.OrderCount,
            summary.AverageValue,
            customersCount,
            completedOrders,
            shippedOrders,
            pendingOrders,
            topPopularProduct?.Label ?? LanguageManager.Get("ReportNoData"),
            topPopularProduct?.SecondaryValue ?? 0m,
            topProfitableProduct?.Label ?? LanguageManager.Get("ReportNoData"),
            topProfitableProduct?.Value ?? 0m,
            conclusion);
    }

    private static IReadOnlyList<AnalyticsMetricRow> BuildAnalyticsMetricRows(AnalyticsOverview overview)
    {
        return
        [
            new AnalyticsMetricRow(LanguageManager.Get("ReportMetricTotalOrders"), overview.TotalOrders.ToString("N0", CultureInfo.CurrentCulture), LanguageManager.Get("ReportMetricTotalOrdersNote")),
            new AnalyticsMetricRow(LanguageManager.Get("ReportMetricRevenue"), FormatMdl(overview.TotalRevenue), LanguageManager.Get("ReportMetricRevenueNote")),
            new AnalyticsMetricRow(LanguageManager.Get("ReportMetricAverageOrder"), FormatMdl(overview.AverageOrder), LanguageManager.Get("ReportMetricAverageOrderNote")),
            new AnalyticsMetricRow(LanguageManager.Get("ReportMetricCustomers"), overview.CustomersCount.ToString("N0", CultureInfo.CurrentCulture), LanguageManager.Get("ReportMetricCustomersNote")),
            new AnalyticsMetricRow(LanguageManager.Get("ReportMetricCompleted"), overview.CompletedOrders.ToString("N0", CultureInfo.CurrentCulture), LanguageManager.Get("ReportMetricCompletedNote")),
            new AnalyticsMetricRow(LanguageManager.Get("ReportMetricShipped"), overview.ShippedOrders.ToString("N0", CultureInfo.CurrentCulture), LanguageManager.Get("ReportMetricShippedNote")),
            new AnalyticsMetricRow(LanguageManager.Get("ReportMetricPending"), overview.PendingOrders.ToString("N0", CultureInfo.CurrentCulture), LanguageManager.Get("ReportMetricPendingNote")),
            new AnalyticsMetricRow(LanguageManager.Get("ReportMetricPopularProduct"), overview.MostPopularProduct, LanguageManager.Format("ReportMetricPopularProductNote", overview.MostPopularProductQuantity.ToString("N0", CultureInfo.CurrentCulture))),
            new AnalyticsMetricRow(LanguageManager.Get("ReportMetricProfitableProduct"), overview.MostProfitableProduct, LanguageManager.Format("ReportMetricProfitableProductNote", FormatMdl(overview.MostProfitableProductRevenue)))
        ];
    }

    private static FlowDocument CreateAnalyticsReferenceDocument(AnalyticsOverview overview, IReadOnlyList<AnalyticsMetricRow> rows)
    {
        var document = new FlowDocument
        {
            FontFamily = ReportFont,
            FontSize = 13,
            PagePadding = new Thickness(36, 28, 36, 32),
            ColumnWidth = 960,
            TextAlignment = TextAlignment.Left,
            Background = Brushes.White
        };

        var headerTable = new Table
        {
            CellSpacing = 0,
            Margin = new Thickness(0, 0, 0, 16)
        };
        headerTable.Columns.Add(new TableColumn { Width = new GridLength(72) });
        headerTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

        var headerGroup = new TableRowGroup();
        var headerRow = new TableRow();
        headerRow.Cells.Add(new TableCell(new Paragraph(new Run("🧸"))
        {
            Margin = new Thickness(0),
            TextAlignment = TextAlignment.Center
        })
        {
            Padding = new Thickness(0, 4, 0, 0),
            BorderThickness = new Thickness(0),
            FontSize = 34,
            Foreground = AccentBrush
        });

        var titleCellParagraph = new Paragraph
        {
            Margin = new Thickness(0)
        };
        titleCellParagraph.Inlines.Add(new Run(LanguageManager.Get("AppTitle"))
        {
            Foreground = AccentBrush,
            FontSize = 28,
            FontWeight = FontWeights.Black
        });
        titleCellParagraph.Inlines.Add(new LineBreak());
        titleCellParagraph.Inlines.Add(new Run(LanguageManager.Get("ReportAnalyticsHeroSubtitle"))
        {
            Foreground = TextBrush,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold
        });
        titleCellParagraph.Inlines.Add(new LineBreak());
        titleCellParagraph.Inlines.Add(new Run($"{LanguageManager.Get("ReportPeriodLabel")}: {overview.PeriodLabel}")
        {
            Foreground = MutedBrush,
            FontSize = 11.5
        });

        headerRow.Cells.Add(new TableCell(titleCellParagraph)
        {
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6, 0, 0, 0)
        });
        headerGroup.Rows.Add(headerRow);
        headerTable.RowGroups.Add(headerGroup);
        document.Blocks.Add(headerTable);

        AddMetricCards(
            document,
            [
                (LanguageManager.Get("ReportMetricTotalOrders"), overview.TotalOrders.ToString("N0", CultureInfo.CurrentCulture)),
                (LanguageManager.Get("ReportMetricRevenue"), FormatMdl(overview.TotalRevenue)),
                (LanguageManager.Get("ReportMetricAverageOrder"), FormatMdl(overview.AverageOrder)),
                (LanguageManager.Get("ReportMetricCustomers"), overview.CustomersCount.ToString("N0", CultureInfo.CurrentCulture))
            ]);

        var metricTable = new Table
        {
            CellSpacing = 0,
            Margin = new Thickness(0, 2, 0, 16)
        };
        metricTable.Columns.Add(new TableColumn { Width = new GridLength(260) });
        metricTable.Columns.Add(new TableColumn { Width = new GridLength(150) });
        metricTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

        var metricHeaderGroup = new TableRowGroup();
        var metricHeader = new TableRow();
        metricHeader.Cells.Add(CreateTextCell(LanguageManager.Get("ReportMetricHeaderIndicator"), isHeader: true));
        metricHeader.Cells.Add(CreateTextCell(LanguageManager.Get("ReportMetricHeaderValue"), isHeader: true));
        metricHeader.Cells.Add(CreateTextCell(LanguageManager.Get("ReportMetricHeaderDescription"), isHeader: true));
        metricHeaderGroup.Rows.Add(metricHeader);
        metricTable.RowGroups.Add(metricHeaderGroup);

        var metricBodyGroup = new TableRowGroup();
        foreach (var row in rows)
        {
            var bodyRow = new TableRow();
            bodyRow.Cells.Add(CreateTextCell(row.Indicator));
            bodyRow.Cells.Add(CreateTextCell(row.Value));
            bodyRow.Cells.Add(CreateTextCell(row.Description));
            metricBodyGroup.Rows.Add(bodyRow);
        }

        metricTable.RowGroups.Add(metricBodyGroup);
        document.Blocks.Add(metricTable);

        var conclusionBorder = new Border
        {
            Background = Brushes.White,
            BorderBrush = AccentBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 0, 0, 16),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = LanguageManager.Get("ReportConclusionTitle"),
                        Foreground = AccentBrush,
                        FontSize = 15,
                        FontWeight = FontWeights.Black
                    },
                    new TextBlock
                    {
                        Text = overview.Conclusion,
                        Foreground = TextBrush,
                        FontSize = 12.5,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 6, 0, 0)
                    }
                }
            }
        };

        document.Blocks.Add(new BlockUIContainer(conclusionBorder));
        document.Blocks.Add(new Paragraph(new Run($"{LanguageManager.Get("ReportFooterGenerated")}    {LanguageManager.Get("ReportFooterPage")} 1"))
        {
            Foreground = MutedBrush,
            FontSize = 10.5,
            Margin = new Thickness(0, 10, 0, 0)
        });

        return document;
    }

    private static FlowDocument CreateFilteredReferenceDocument(
        string title,
        string subtitle,
        string periodLabel,
        IReadOnlyList<(string Label, string Value)> cards,
        DataTable table,
        IReadOnlyList<DataColumn> visibleColumns)
    {
        var document = new FlowDocument
        {
            FontFamily = ReportFont,
            FontSize = 13,
            PagePadding = new Thickness(36, 28, 36, 32),
            ColumnWidth = 960,
            TextAlignment = TextAlignment.Left,
            Background = Brushes.White
        };

        document.Blocks.Add(new Paragraph(new Run(LanguageManager.Get("AppTitle")))
        {
            Foreground = AccentBrush,
            FontSize = 28,
            FontWeight = FontWeights.Black,
            Margin = new Thickness(0, 0, 0, 4)
        });
        document.Blocks.Add(new Paragraph(new Run(subtitle))
        {
            Foreground = TextBrush,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4)
        });
        document.Blocks.Add(new Paragraph(new Run($"{LanguageManager.Get("ReportPeriodLabel")}: {periodLabel}"))
        {
            Foreground = MutedBrush,
            FontSize = 11.5,
            Margin = new Thickness(0, 0, 0, 16)
        });

        AddMetricCards(document, cards);
        AddDataSection(document, title, table, visibleColumns);

        return document;
    }

    private static string BuildFilteredReferenceHtml(
        string title,
        string subtitle,
        string periodLabel,
        IReadOnlyList<(string Label, string Value)> cards,
        DataTable table,
        IReadOnlyList<DataColumn> visibleColumns)
    {
        var safeTitle = WebUtility.HtmlEncode(LanguageManager.Get("AppTitle"));
        var safeSubtitle = WebUtility.HtmlEncode(subtitle);
        var safePeriod = WebUtility.HtmlEncode($"{LanguageManager.Get("ReportPeriodLabel")}: {periodLabel}");
        var builder = new StringBuilder();

        builder.AppendLine($$"""
        <!DOCTYPE html>
        <html lang="{{(LanguageManager.CurrentLanguage == AppLanguage.Russian ? "ru" : "en")}}">
        <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1" />
            <title>{{WebUtility.HtmlEncode(title)}}</title>
            <style>
                :root {
                    --paper-edge: #f5dbe3;
                    --accent: #ef7fa0;
                    --accent-soft: #fff4f7;
                    --text: #4a4146;
                    --muted: #7f7277;
                    --line: #f0d9e0;
                }
                * { box-sizing: border-box; }
                body {
                    margin: 0;
                    background: #faf6f8;
                    font-family: "Nunito", "Quicksand", "Poppins", "Segoe UI", sans-serif;
                    color: var(--text);
                    padding: 40px 0;
                }
                .page {
                    width: 1000px;
                    margin: 0 auto;
                    background:
                        radial-gradient(circle at 0% 0%, rgba(255, 231, 238, 0.7), transparent 30%),
                        radial-gradient(circle at 100% 100%, rgba(255, 243, 247, 0.9), transparent 34%),
                        linear-gradient(135deg, #ffffff 0%, #fffafc 50%, #fff5f8 100%);
                    border: 1px solid var(--paper-edge);
                    box-shadow: 0 24px 48px rgba(148, 83, 106, 0.12);
                    padding: 28px 28px 24px;
                }
                .brand {
                    font-size: 28px;
                    line-height: 1;
                    color: var(--accent);
                    font-weight: 900;
                    margin: 0 0 6px;
                }
                .subtitle {
                    font-size: 18px;
                    font-weight: 700;
                    margin: 0 0 6px;
                }
                .period {
                    font-size: 13px;
                    color: var(--muted);
                    margin-bottom: 18px;
                }
                .kpis {
                    display: grid;
                    grid-template-columns: repeat(4, 1fr);
                    gap: 12px;
                    margin-bottom: 16px;
                }
                .kpi {
                    border: 1px solid var(--paper-edge);
                    border-radius: 8px;
                    background: rgba(255,255,255,0.92);
                    padding: 14px 14px 16px;
                    min-height: 118px;
                }
                .kpi-dot {
                    width: 34px;
                    height: 34px;
                    border-radius: 50%;
                    background: linear-gradient(180deg, #fff4f7 0%, #ffe5ee 100%);
                    margin-bottom: 14px;
                }
                .kpi-label {
                    color: var(--muted);
                    font-size: 14px;
                    margin-bottom: 8px;
                }
                .kpi-value {
                    color: var(--accent);
                    font-size: 19px;
                    font-weight: 900;
                }
                .table-wrap {
                    border: 1px solid var(--paper-edge);
                    border-radius: 8px;
                    overflow: hidden;
                    background: rgba(255,255,255,0.95);
                }
                table {
                    width: 100%;
                    border-collapse: collapse;
                }
                thead th {
                    background: linear-gradient(90deg, #fff4f7 0%, #fffafb 100%);
                    padding: 13px 12px;
                    font-size: 14px;
                    font-weight: 800;
                    color: var(--text);
                    border-bottom: 1px solid var(--paper-edge);
                    text-align: left;
                }
                tbody td {
                    padding: 13px 12px;
                    font-size: 13.5px;
                    border-bottom: 1px solid #f6e8ed;
                    vertical-align: top;
                }
                tbody tr:nth-child(even) td {
                    background: #fffdfd;
                }
            </style>
        </head>
        <body>
            <main class="page">
                <div class="brand">{{safeTitle}}</div>
                <div class="subtitle">{{safeSubtitle}}</div>
                <div class="period">{{safePeriod}}</div>
                <section class="kpis">
        """);

        foreach (var card in cards)
        {
            builder.AppendLine($$"""
                    <article class="kpi">
                        <div class="kpi-dot"></div>
                        <div class="kpi-label">{{WebUtility.HtmlEncode(card.Label)}}</div>
                        <div class="kpi-value">{{WebUtility.HtmlEncode(card.Value)}}</div>
                    </article>
            """);
        }

        builder.AppendLine("""
                </section>
                <section class="table-wrap">
                    <table>
                        <thead>
                            <tr>
        """);

        foreach (var column in visibleColumns)
        {
            builder.AppendLine($"""<th>{WebUtility.HtmlEncode(ResolveColumnHeader(column.ColumnName))}</th>""");
        }

        builder.AppendLine("""
                            </tr>
                        </thead>
                        <tbody>
        """);

        foreach (DataRow row in table.Rows)
        {
            builder.AppendLine("<tr>");
            foreach (var column in visibleColumns)
            {
                builder.AppendLine($"""<td>{WebUtility.HtmlEncode(FormatCellValue(row[column], column.ColumnName))}</td>""");
            }

            builder.AppendLine("</tr>");
        }

        builder.AppendLine("""
                        </tbody>
                    </table>
                </section>
            </main>
        </body>
        </html>
        """);

        return builder.ToString();
    }

    private static string BuildAnalyticsReferenceHtml(AnalyticsOverview overview, IReadOnlyList<AnalyticsMetricRow> rows)
    {
        var safeTitle = WebUtility.HtmlEncode(LanguageManager.Get("AppTitle"));
        var safeSubtitle = WebUtility.HtmlEncode(LanguageManager.Get("ReportAnalyticsHeroSubtitle"));
        var safePeriod = WebUtility.HtmlEncode($"{LanguageManager.Get("ReportPeriodLabel")}: {overview.PeriodLabel}");

        var builder = new StringBuilder();
        builder.AppendLine($$"""
        <!DOCTYPE html>
        <html lang="{{(LanguageManager.CurrentLanguage == AppLanguage.Russian ? "ru" : "en")}}">
        <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1" />
            <title>{{WebUtility.HtmlEncode(LanguageManager.Get("ReportAnalyticsHeroTitle"))}}</title>
            <style>
                :root {
                    --paper: #ffffff;
                    --paper-edge: #f5dbe3;
                    --accent: #ef7fa0;
                    --accent-soft: #fff4f7;
                    --accent-soft-2: #fff8fa;
                    --text: #4a4146;
                    --muted: #7f7277;
                    --line: #f0d9e0;
                }
                * { box-sizing: border-box; }
                body {
                    margin: 0;
                    background: #faf6f8;
                    font-family: "Nunito", "Quicksand", "Poppins", "Segoe UI", sans-serif;
                    color: var(--text);
                    padding: 40px 0;
                }
                .page {
                    width: 1000px;
                    margin: 0 auto;
                    background:
                        radial-gradient(circle at 0% 0%, rgba(255, 231, 238, 0.7), transparent 30%),
                        radial-gradient(circle at 100% 100%, rgba(255, 243, 247, 0.9), transparent 34%),
                        linear-gradient(135deg, #ffffff 0%, #fffafc 50%, #fff5f8 100%);
                    border: 1px solid var(--paper-edge);
                    box-shadow: 0 24px 48px rgba(148, 83, 106, 0.12);
                    padding: 28px 28px 18px;
                }
                .hero {
                    display: grid;
                    grid-template-columns: 72px 1fr;
                    gap: 12px;
                    align-items: start;
                    margin-bottom: 18px;
                }
                .bear {
                    width: 64px;
                    height: 64px;
                    border-radius: 20px;
                    background: linear-gradient(180deg, #ffd6e2 0%, #fff1f5 100%);
                    display: grid;
                    place-items: center;
                    font-size: 38px;
                    color: var(--accent);
                }
                .brand {
                    font-size: 28px;
                    line-height: 1;
                    color: var(--accent);
                    font-weight: 900;
                    margin: 4px 0 6px;
                }
                .subtitle {
                    font-size: 18px;
                    font-weight: 700;
                    margin: 0 0 6px;
                }
                .period {
                    font-size: 13px;
                    color: var(--muted);
                }
                .kpis {
                    display: grid;
                    grid-template-columns: repeat(4, 1fr);
                    gap: 12px;
                    margin: 18px 0 16px;
                }
                .kpi {
                    border: 1px solid var(--paper-edge);
                    border-radius: 8px;
                    background: rgba(255,255,255,0.92);
                    padding: 14px 14px 16px;
                    min-height: 126px;
                }
                .kpi-icon {
                    width: 36px;
                    height: 36px;
                    border-radius: 50%;
                    background: linear-gradient(180deg, #fff4f7 0%, #ffe5ee 100%);
                    color: var(--accent);
                    display: grid;
                    place-items: center;
                    font-size: 18px;
                    margin-bottom: 14px;
                }
                .kpi-label {
                    color: var(--muted);
                    font-size: 14px;
                    margin-bottom: 8px;
                }
                .kpi-value {
                    color: var(--accent);
                    font-size: 19px;
                    font-weight: 900;
                }
                .table-wrap {
                    border: 1px solid var(--paper-edge);
                    margin-top: 6px;
                    overflow: hidden;
                }
                table {
                    width: 100%;
                    border-collapse: collapse;
                }
                thead th {
                    background: linear-gradient(90deg, #fff4f7 0%, #fffafb 100%);
                    padding: 13px 12px;
                    font-size: 14px;
                    font-weight: 800;
                    color: var(--text);
                    border-bottom: 1px solid var(--paper-edge);
                    border-right: 1px solid var(--paper-edge);
                }
                thead th:last-child,
                tbody td:last-child {
                    border-right: 0;
                }
                tbody td {
                    padding: 13px 12px;
                    font-size: 13.5px;
                    border-bottom: 1px solid #f6e8ed;
                    border-right: 1px solid #f3e3e8;
                    vertical-align: top;
                }
                tbody tr:nth-child(even) td {
                    background: #fffdfd;
                }
                .conclusion {
                    margin-top: 16px;
                    border: 1px solid var(--accent);
                    border-radius: 8px;
                    background: linear-gradient(180deg, #fffafb 0%, #fff7f9 100%);
                    padding: 14px 16px;
                    display: grid;
                    grid-template-columns: 34px 1fr;
                    gap: 12px;
                    align-items: start;
                }
                .conclusion-icon {
                    width: 30px;
                    height: 30px;
                    border-radius: 50%;
                    background: #ffe7ef;
                    color: var(--accent);
                    display: grid;
                    place-items: center;
                    font-size: 16px;
                    margin-top: 2px;
                }
                .conclusion-title {
                    color: var(--accent);
                    font-weight: 900;
                    font-size: 15px;
                    margin-bottom: 4px;
                }
                .conclusion-text {
                    font-size: 13.5px;
                    line-height: 1.55;
                }
                .footer {
                    margin-top: 18px;
                    padding-top: 10px;
                    border-top: 1px solid #f0dfe5;
                    color: var(--muted);
                    font-size: 11px;
                    display: flex;
                    justify-content: space-between;
                }
                @media print {
                    body { background: #fff; padding: 0; }
                    .page { box-shadow: none; margin: 0; width: auto; }
                }
            </style>
        </head>
        <body>
            <main class="page">
                <section class="hero">
                    <div class="bear">🧸</div>
                    <div>
                        <div class="brand">{{safeTitle}}</div>
                        <div class="subtitle">{{safeSubtitle}}</div>
                        <div class="period">{{safePeriod}}</div>
                    </div>
                </section>
                <section class="kpis">
                    {{BuildAnalyticsKpiHtml(LanguageManager.Get("ReportMetricTotalOrders"), overview.TotalOrders.ToString("N0", CultureInfo.CurrentCulture), "🛒")}}
                    {{BuildAnalyticsKpiHtml(LanguageManager.Get("ReportMetricRevenue"), FormatMdl(overview.TotalRevenue), "💲")}}
                    {{BuildAnalyticsKpiHtml(LanguageManager.Get("ReportMetricAverageOrder"), FormatMdl(overview.AverageOrder), "📊")}}
                    {{BuildAnalyticsKpiHtml(LanguageManager.Get("ReportMetricCustomers"), overview.CustomersCount.ToString("N0", CultureInfo.CurrentCulture), "👤")}}
                </section>
                <section class="table-wrap">
                    <table>
                        <thead>
                            <tr>
                                <th>{{WebUtility.HtmlEncode(LanguageManager.Get("ReportMetricHeaderIndicator"))}}</th>
                                <th>{{WebUtility.HtmlEncode(LanguageManager.Get("ReportMetricHeaderValue"))}}</th>
                                <th>{{WebUtility.HtmlEncode(LanguageManager.Get("ReportMetricHeaderDescription"))}}</th>
                            </tr>
                        </thead>
                        <tbody>
        """);

        foreach (var row in rows)
        {
            builder.AppendLine($$"""
                            <tr>
                                <td>{{WebUtility.HtmlEncode(row.Indicator)}}</td>
                                <td>{{WebUtility.HtmlEncode(row.Value)}}</td>
                                <td>{{WebUtility.HtmlEncode(row.Description)}}</td>
                            </tr>
            """);
        }

        builder.AppendLine($$"""
                        </tbody>
                    </table>
                </section>
                <section class="conclusion">
                    <div class="conclusion-icon">★</div>
                    <div>
                        <div class="conclusion-title">{{WebUtility.HtmlEncode(LanguageManager.Get("ReportConclusionTitle"))}}</div>
                        <div class="conclusion-text">{{WebUtility.HtmlEncode(overview.Conclusion)}}</div>
                    </div>
                </section>
                <footer class="footer">
                    <span>{{WebUtility.HtmlEncode(LanguageManager.Get("ReportFooterGenerated"))}}</span>
                    <span>{{WebUtility.HtmlEncode($"{LanguageManager.Get("ReportFooterPage")} 1")}}</span>
                </footer>
            </main>
        </body>
        </html>
        """);

        return builder.ToString();
    }

    private static string BuildAnalyticsKpiHtml(string label, string value, string icon)
    {
        return $"""
            <article class="kpi">
                <div class="kpi-icon">{WebUtility.HtmlEncode(icon)}</div>
                <div class="kpi-label">{WebUtility.HtmlEncode(label)}</div>
                <div class="kpi-value">{WebUtility.HtmlEncode(value)}</div>
            </article>
            """;
    }

    private static FlowDocument CreateBaseDocument(string title, string subtitle)
    {
        var document = new FlowDocument
        {
            FontFamily = ReportFont,
            FontSize = 13,
            PagePadding = new Thickness(42, 34, 42, 40),
            ColumnWidth = 960,
            TextAlignment = TextAlignment.Left
        };

        document.Blocks.Add(new Paragraph(new Run(LanguageManager.Get("AppTitle")))
        {
            Foreground = AccentBrush,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        document.Blocks.Add(new Paragraph(new Run(title))
        {
            Foreground = TextBrush,
            FontSize = 28,
            FontWeight = FontWeights.Black,
            Margin = new Thickness(0, 0, 0, 6)
        });

        document.Blocks.Add(new Paragraph(new Run(subtitle))
        {
            Foreground = MutedBrush,
            FontSize = 14,
            Margin = new Thickness(0, 0, 0, 8)
        });

        document.Blocks.Add(new Paragraph(new Run(LanguageManager.Format("ReportGeneratedAtLine", DateTime.Now.ToString("f", CultureInfo.CurrentCulture))))
        {
            Foreground = MutedBrush,
            FontSize = 11,
            Margin = new Thickness(0, 0, 0, 18)
        });

        return document;
    }

    private static void AddMetaFacts(FlowDocument document, string sectionTitle, IReadOnlyList<(string Label, string Value)> facts)
    {
        AddSectionTitle(document, sectionTitle);

        var table = new Table
        {
            CellSpacing = 0,
            Margin = new Thickness(0, 0, 0, 18)
        };

        table.Columns.Add(new TableColumn { Width = new GridLength(190) });
        table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

        var rowGroup = new TableRowGroup();
        foreach (var fact in facts)
        {
            var row = new TableRow();
            row.Cells.Add(CreateTextCell(fact.Label, isHeader: true));
            row.Cells.Add(CreateTextCell(fact.Value));
            rowGroup.Rows.Add(row);
        }

        table.RowGroups.Add(rowGroup);
        document.Blocks.Add(table);
    }

    private static void AddMetricCards(FlowDocument document, IReadOnlyList<(string Label, string Value)> cards)
    {
        var panel = new WrapPanel
        {
            Margin = new Thickness(0, 0, 0, 16)
        };

        foreach (var card in cards)
        {
            panel.Children.Add(CreateMetricCard(card.Label, card.Value));
        }

        document.Blocks.Add(new BlockUIContainer(panel));
    }

    private static Border CreateMetricCard(string label, string value)
    {
        return new Border
        {
            Width = 196,
            Margin = new Thickness(0, 0, 14, 14),
            Padding = new Thickness(16, 14, 16, 14),
            Background = SurfaceBrush,
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = label,
                        Foreground = MutedBrush,
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold
                    },
                    new TextBlock
                    {
                        Text = value,
                        Foreground = TextBrush,
                        FontSize = 22,
                        FontWeight = FontWeights.Black,
                        Margin = new Thickness(0, 6, 0, 0)
                    }
                }
            }
        };
    }

    private static void AddFilterChips(FlowDocument document, IReadOnlyList<(string Label, string Value)> filters)
    {
        AddSectionTitle(document, LanguageManager.Get("ReportSectionFilters"));

        var panel = new WrapPanel
        {
            Margin = new Thickness(0, 0, 0, 18)
        };

        foreach (var filter in filters)
        {
            panel.Children.Add(new Border
            {
                Background = AccentSoftBrush,
                BorderBrush = BorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 10, 10),
                Padding = new Thickness(12, 8, 12, 8),
                Child = new TextBlock
                {
                    Text = $"{filter.Label}: {filter.Value}",
                    Foreground = TextBrush,
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold
                }
            });
        }

        document.Blocks.Add(new BlockUIContainer(panel));
    }

    private static void AddInsightSection(
        FlowDocument document,
        IReadOnlyList<DashboardSeriesPoint> statusPoints,
        IReadOnlyList<DashboardSeriesPoint> categoryPoints,
        IReadOnlyList<DashboardSeriesPoint> monthPoints)
    {
        AddSectionTitle(document, LanguageManager.Get("ReportSectionInsights"));

        var insights = BuildInsightLines(statusPoints, categoryPoints, monthPoints);
        foreach (var insight in insights)
        {
            document.Blocks.Add(new Paragraph(new Run(insight))
            {
                Foreground = TextBrush,
                FontSize = 12.5,
                Margin = new Thickness(0, 0, 0, 6)
            });
        }

        document.Blocks.Add(new Paragraph
        {
            Margin = new Thickness(0, 0, 0, 8)
        });
    }

    private static IReadOnlyList<string> BuildInsightLines(
        IReadOnlyList<DashboardSeriesPoint> statusPoints,
        IReadOnlyList<DashboardSeriesPoint> categoryPoints,
        IReadOnlyList<DashboardSeriesPoint> monthPoints)
    {
        var insights = new List<string>();
        var topStatus = statusPoints.OrderByDescending(point => point.Value).FirstOrDefault();
        var topCategory = categoryPoints.OrderByDescending(point => point.Value).FirstOrDefault();
        var topMonth = monthPoints.OrderByDescending(point => point.Value).FirstOrDefault();

        if (topStatus is not null)
        {
            insights.Add(LanguageManager.Format("ReportInsightTopStatus", topStatus.Label, topStatus.Value.ToString("N0", CultureInfo.CurrentCulture)));
        }

        if (topCategory is not null)
        {
            insights.Add(LanguageManager.Format("ReportInsightTopCategory", topCategory.Label, FormatMoney(topCategory.Value)));
        }

        if (topMonth is not null)
        {
            insights.Add(LanguageManager.Format("ReportInsightTopMonth", topMonth.Label, FormatMoney(topMonth.Value)));
        }

        if (insights.Count == 0)
        {
            insights.Add(LanguageManager.Get("ReportNoData"));
        }

        return insights;
    }

    private static void AddSeriesSection(
        FlowDocument document,
        string sectionTitle,
        IReadOnlyList<DashboardSeriesPoint> points,
        string labelHeader,
        string valueHeader,
        string secondaryHeader)
    {
        AddSectionTitle(document, sectionTitle);

        if (points.Count == 0)
        {
            document.Blocks.Add(CreateMutedParagraph(LanguageManager.Get("ReportNoData")));
            return;
        }

        var table = new Table
        {
            CellSpacing = 0,
            Margin = new Thickness(0, 0, 0, 18)
        };

        table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(140) });
        table.Columns.Add(new TableColumn { Width = new GridLength(140) });

        var headerGroup = new TableRowGroup();
        var header = new TableRow();
        header.Cells.Add(CreateTextCell(labelHeader, isHeader: true));
        header.Cells.Add(CreateTextCell(valueHeader, isHeader: true));
        header.Cells.Add(CreateTextCell(secondaryHeader, isHeader: true));
        headerGroup.Rows.Add(header);
        table.RowGroups.Add(headerGroup);

        var bodyGroup = new TableRowGroup();
        foreach (var point in points)
        {
            var row = new TableRow();
            row.Cells.Add(CreateTextCell(point.Label));
            row.Cells.Add(CreateTextCell(FormatMoney(point.Value)));
            row.Cells.Add(CreateTextCell(point.SecondaryValue.ToString("N0", CultureInfo.CurrentCulture)));
            bodyGroup.Rows.Add(row);
        }

        table.RowGroups.Add(bodyGroup);
        document.Blocks.Add(table);
    }

    private static void AddDataSection(FlowDocument document, string sectionTitle, DataTable table, IReadOnlyList<DataColumn> visibleColumns)
    {
        AddSectionTitle(document, sectionTitle);

        if (table.Rows.Count == 0 || visibleColumns.Count == 0)
        {
            document.Blocks.Add(CreateMutedParagraph(LanguageManager.Get("ReportNoData")));
            return;
        }

        var flowTable = new Table
        {
            CellSpacing = 0,
            Margin = new Thickness(0, 0, 0, 18)
        };

        foreach (var _ in visibleColumns)
        {
            flowTable.Columns.Add(new TableColumn { Width = GridLength.Auto });
        }

        var headerGroup = new TableRowGroup();
        var headerRow = new TableRow();
        foreach (var column in visibleColumns)
        {
            headerRow.Cells.Add(CreateTextCell(ResolveColumnHeader(column.ColumnName), isHeader: true));
        }

        headerGroup.Rows.Add(headerRow);
        flowTable.RowGroups.Add(headerGroup);

        var bodyGroup = new TableRowGroup();
        foreach (DataRow row in table.Rows)
        {
            var tableRow = new TableRow();
            foreach (var column in visibleColumns)
            {
                tableRow.Cells.Add(CreateTextCell(FormatCellValue(row[column], column.ColumnName)));
            }

            bodyGroup.Rows.Add(tableRow);
        }

        flowTable.RowGroups.Add(bodyGroup);
        document.Blocks.Add(flowTable);
    }

    private static TableCell CreateTextCell(string value, bool isHeader = false)
    {
        return new TableCell(new Paragraph(new Run(value))
        {
            Margin = new Thickness(0)
        })
        {
            Padding = new Thickness(10, 8, 10, 8),
            Background = isHeader ? AccentSoftBrush : SurfaceBrush,
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(1),
            FontWeight = isHeader ? FontWeights.Bold : FontWeights.Normal,
            Foreground = TextBrush
        };
    }

    private static Paragraph CreateMutedParagraph(string value)
    {
        return new Paragraph(new Run(value))
        {
            Foreground = MutedBrush,
            FontStyle = FontStyles.Italic,
            Margin = new Thickness(0, 0, 0, 14)
        };
    }

    private static void AddSectionTitle(FlowDocument document, string title)
    {
        document.Blocks.Add(new Paragraph(new Run(title))
        {
            Foreground = TextBrush,
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 10)
        });
    }

    private static IReadOnlyList<DataColumn> GetVisibleColumns(DataTable table, bool hideEmptyColumns = false)
    {
        var columns = table.Columns
            .Cast<DataColumn>()
            .Where(IsReportVisibleColumn)
            .ToList();

        if (!hideEmptyColumns)
        {
            return columns;
        }

        return columns
            .Where(column => table.Rows.Cast<DataRow>().Any(row => HasMeaningfulValue(row[column])))
            .ToList();
    }

    private static bool IsReportVisibleColumn(DataColumn column)
    {
        var localizedImageHeader = LanguageManager.GetColumnName("image_path");

        if (column.ColumnName.Equals("audit_id", StringComparison.OrdinalIgnoreCase) ||
            column.ColumnName.Equals("entity_id", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (column.ColumnName.StartsWith("__src_", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (column.ColumnName.StartsWith("__preview_", StringComparison.OrdinalIgnoreCase) ||
            column.ColumnName.Equals("image_path", StringComparison.OrdinalIgnoreCase) ||
            column.ColumnName.Equals("Image Path", StringComparison.OrdinalIgnoreCase) ||
            column.ColumnName.Equals("Image", StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrWhiteSpace(localizedImageHeader) &&
             column.ColumnName.Equals(localizedImageHeader, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return !column.ColumnName.EndsWith("_id", StringComparison.OrdinalIgnoreCase) &&
               !column.ColumnName.Equals("id", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveColumnHeader(string columnName)
    {
        if (columnName.Equals("audit_id", StringComparison.OrdinalIgnoreCase))
        {
            return LanguageManager.CurrentLanguage == AppLanguage.Russian ? "ID события" : "Audit ID";
        }

        if (columnName.Equals("created_at", StringComparison.OrdinalIgnoreCase))
        {
            return LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Дата и время" : "Date and Time";
        }

        if (columnName.Equals("username", StringComparison.OrdinalIgnoreCase))
        {
            return LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Пользователь" : "Username";
        }

        if (columnName.Equals("role_name", StringComparison.OrdinalIgnoreCase))
        {
            return LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Роль" : "Role";
        }

        if (columnName.Equals("action_type", StringComparison.OrdinalIgnoreCase))
        {
            return LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Код действия" : "Action Code";
        }

        if (columnName.Equals("action_label", StringComparison.OrdinalIgnoreCase))
        {
            return LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Действие" : "Action";
        }

        if (columnName.Equals("entity_name", StringComparison.OrdinalIgnoreCase))
        {
            return LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Объект" : "Entity";
        }

        if (columnName.Equals("entity_id", StringComparison.OrdinalIgnoreCase))
        {
            return LanguageManager.CurrentLanguage == AppLanguage.Russian ? "ID объекта" : "Entity ID";
        }

        if (columnName.Equals("action_description", StringComparison.OrdinalIgnoreCase))
        {
            return LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Описание операции" : "Operation Description";
        }

        return LanguageManager.GetColumnName(columnName) ?? columnName;
    }

    private static string FormatCellValue(object? value, string columnName)
    {
        if (value is null || value is DBNull)
        {
            return "—";
        }

        if (value is DateTime dateTime)
        {
            if (columnName.Equals("created_at", StringComparison.OrdinalIgnoreCase))
            {
                return dateTime.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.CurrentCulture);
            }

            return dateTime.ToString("dd.MM.yyyy", CultureInfo.CurrentCulture);
        }

        if (value is decimal decimalValue)
        {
            return ShouldFormatAsMoney(columnName)
                ? FormatMoney(decimalValue)
                : decimalValue.ToString("N2", CultureInfo.CurrentCulture);
        }

        if (value is double doubleValue)
        {
            return doubleValue.ToString("N2", CultureInfo.CurrentCulture);
        }

        var text = Convert.ToString(value, CultureInfo.CurrentCulture);
        return string.IsNullOrWhiteSpace(text) ? "—" : text;
    }

    private static bool HasMeaningfulValue(object? value)
    {
        if (value is null || value is DBNull)
        {
            return false;
        }

        if (value is string text)
        {
            return !string.IsNullOrWhiteSpace(text);
        }

        return true;
    }

    private static bool ShouldFormatAsMoney(string columnName)
    {
        return columnName.Contains("total", StringComparison.OrdinalIgnoreCase) ||
               columnName.Contains("price", StringComparison.OrdinalIgnoreCase) ||
               columnName.Contains("cost", StringComparison.OrdinalIgnoreCase) ||
               columnName.Contains("sum", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatMoney(decimal value)
    {
        return value.ToString("N2", CultureInfo.CurrentCulture);
    }

    private static string FormatMdl(decimal value)
    {
        var formatted = value.ToString("N2", CultureInfo.CurrentCulture);
        var decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        formatted = formatted.EndsWith($"{decimalSeparator}00", StringComparison.Ordinal)
            ? formatted[..^3]
            : formatted;

        return $"{formatted} MDL";
    }

    private static string ResolvePeriodLabel(DashboardFilter filter, DataTable data)
    {
        var start = filter.DateFrom;
        var end = filter.DateTo;

        if ((!start.HasValue || !end.HasValue) && data.Rows.Count > 0 && data.Columns.Contains("start_date"))
        {
            var dates = data.Rows
                .Cast<DataRow>()
                .Select(row => row["start_date"])
                .Where(value => value is not DBNull)
                .Select(value => Convert.ToDateTime(value, CultureInfo.CurrentCulture))
                .OrderBy(value => value)
                .ToList();

            if (dates.Count > 0)
            {
                start ??= dates.First();
                end ??= dates.Last();
            }
        }

        if (start.HasValue && end.HasValue)
        {
            return $"{start.Value:dd.MM.yyyy} — {end.Value:dd.MM.yyyy}";
        }

        if (start.HasValue)
        {
            return $"{start.Value:dd.MM.yyyy} — {start.Value:dd.MM.yyyy}";
        }

        if (end.HasValue)
        {
            return $"{end.Value:dd.MM.yyyy} — {end.Value:dd.MM.yyyy}";
        }

        return LanguageManager.Get("ReportFilterNone");
    }

    private static int GetStatusCount(IReadOnlyList<DashboardSeriesPoint> statusPoints, string status)
    {
        var point = statusPoints.FirstOrDefault(item => item.Label.Equals(status, StringComparison.OrdinalIgnoreCase));
        return point is null ? 0 : decimal.ToInt32(point.Value);
    }

    private static int GetDistinctCount(DataTable data, string columnName)
    {
        if (!data.Columns.Contains(columnName))
        {
            return 0;
        }

        return data.Rows
            .Cast<DataRow>()
            .Select(row => Convert.ToString(row[columnName], CultureInfo.CurrentCulture)?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
    }

    private static string BuildAnalyticsConclusion(decimal revenue, int completedOrders, int totalOrders, string topPopularProduct)
    {
        var completedShare = totalOrders <= 0
            ? 0m
            : Math.Round(completedOrders / (decimal)totalOrders * 100m, 0);

        return LanguageManager.Format(
            "ReportAnalyticsConclusionText",
            FormatMdl(revenue),
            completedOrders.ToString("N0", CultureInfo.CurrentCulture),
            completedShare.ToString("N0", CultureInfo.CurrentCulture),
            topPopularProduct);
    }

    private static IReadOnlyList<(string Label, string Value)> DescribeDashboardFilter(DashboardFilter filter)
    {
        var items = new List<(string Label, string Value)>();

        void Add(string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                items.Add((label, value));
            }
        }

        Add(LanguageManager.Get("DashboardClient"), filter.ClientName);
        Add(LanguageManager.Get("DashboardStatus"), FormatStatusLabel(filter.Status));
        Add(LanguageManager.Get("DashboardProduct"), filter.ProductTitle);
        Add(LanguageManager.Get("DashboardFabricType"), filter.FabricType);
        Add(LanguageManager.Get("DashboardDateFrom"), filter.DateFrom?.ToString("dd.MM.yyyy", CultureInfo.CurrentCulture));
        Add(LanguageManager.Get("DashboardDateTo"), filter.DateTo?.ToString("dd.MM.yyyy", CultureInfo.CurrentCulture));
        Add(LanguageManager.Get("DashboardMinAmount"), filter.MinPrice.HasValue ? FormatMoney(filter.MinPrice.Value) : null);
        Add(LanguageManager.Get("DashboardMaxAmount"), filter.MaxPrice.HasValue ? FormatMoney(filter.MaxPrice.Value) : null);

        return items.Count > 0
            ? items
            :
            [
                (LanguageManager.Get("ReportFiltersLabel"), LanguageManager.Get("ReportFilterNone"))
            ];
    }

    private static IReadOnlyList<(string Label, string Value)> DescribeAuditFilter(AuditReportFilter filter)
    {
        var items = new List<(string Label, string Value)>();

        void Add(string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                items.Add((label, value));
            }
        }

        Add(LanguageManager.Get("DashboardDateFrom"), filter.DateFrom?.ToString("dd.MM.yyyy", CultureInfo.CurrentCulture));
        Add(LanguageManager.Get("DashboardDateTo"), filter.DateTo?.ToString("dd.MM.yyyy", CultureInfo.CurrentCulture));
        Add(LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Пользователь" : "User", filter.Username);
        Add(LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Тип действия" : "Action Type", filter.ActionType);

        return items.Count > 0
            ? items
            :
            [
                (LanguageManager.Get("ReportFiltersLabel"), LanguageManager.Get("ReportFilterNone"))
            ];
    }

    private static string FormatStatusLabel(string? status)
    {
        return status switch
        {
            null or "" => string.Empty,
            "Pending" => LanguageManager.Get("StatusPending"),
            "Shipped" => LanguageManager.Get("StatusShipped"),
            "Completed" => LanguageManager.Get("StatusCompleted"),
            _ => status
        };
    }

    private static string BuildFileName(string prefix, string suffix)
    {
        var safeSuffix = new string(suffix
            .ToLowerInvariant()
            .Where(character => char.IsLetterOrDigit(character) || character == '-')
            .ToArray());

        if (string.IsNullOrWhiteSpace(safeSuffix))
        {
            safeSuffix = "report";
        }

        return $"babyshop-{prefix}-{safeSuffix}-{DateTime.Now:yyyyMMdd-HHmmss}";
    }

    private static string BuildHtmlShell(string title, string subtitle, string bodyContent)
    {
        var generatedAt = WebUtility.HtmlEncode(DateTime.Now.ToString("f", CultureInfo.CurrentCulture));
        var safeTitle = WebUtility.HtmlEncode(title);
        var safeSubtitle = WebUtility.HtmlEncode(subtitle);

        return $$"""
        <!DOCTYPE html>
        <html lang="{{(LanguageManager.CurrentLanguage == AppLanguage.Russian ? "ru" : "en")}}">
        <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1" />
            <title>{{safeTitle}}</title>
            <style>
                :root {
                    color-scheme: light;
                    --bg: #fff8f0;
                    --surface: #ffffff;
                    --surface-soft: #fff1f5;
                    --line: #eaded4;
                    --ink: #37403a;
                    --muted: #7b817a;
                    --accent: #d77d91;
                    --accent-deep: #9e5569;
                }

                * { box-sizing: border-box; }
                body {
                    margin: 0;
                    padding: 32px;
                    background: linear-gradient(135deg, #fffdf9 0%, var(--bg) 100%);
                    color: var(--ink);
                    font-family: "Nunito", "Quicksand", "Poppins", "Segoe UI", sans-serif;
                }

                .page {
                    max-width: 1180px;
                    margin: 0 auto;
                }

                .hero,
                .section,
                .table-wrap {
                    background: rgba(255,255,255,0.96);
                    border: 1px solid var(--line);
                    border-radius: 8px;
                    box-shadow: 0 20px 40px -28px rgba(158, 85, 105, 0.26);
                }

                .hero {
                    padding: 28px;
                    background: linear-gradient(135deg, #ffffff 0%, #fff6ea 45%, #ffe7ee 100%);
                    margin-bottom: 22px;
                }

                .eyebrow {
                    display: inline-block;
                    padding: 6px 10px;
                    border-radius: 999px;
                    background: var(--surface-soft);
                    color: var(--accent-deep);
                    font-size: 11px;
                    font-weight: 800;
                    letter-spacing: 0.08em;
                    text-transform: uppercase;
                    margin-bottom: 14px;
                }

                h1 {
                    margin: 0;
                    font-size: 42px;
                    line-height: 1;
                    color: var(--ink);
                }

                .subtitle {
                    margin: 12px 0 8px;
                    color: var(--muted);
                    font-size: 16px;
                    max-width: 760px;
                    line-height: 1.55;
                }

                .meta {
                    color: var(--muted);
                    font-size: 12px;
                }

                .summary-grid {
                    display: grid;
                    grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
                    gap: 16px;
                    margin-bottom: 22px;
                }

                .summary-card {
                    padding: 18px;
                    border: 1px solid var(--line);
                    border-radius: 8px;
                    background: var(--surface);
                }

                .summary-card .label {
                    color: var(--muted);
                    font-size: 12px;
                    font-weight: 700;
                    margin-bottom: 8px;
                }

                .summary-card .value {
                    font-size: 28px;
                    font-weight: 900;
                    color: var(--ink);
                }

                .section {
                    padding: 22px;
                    margin-bottom: 20px;
                }

                h2 {
                    margin: 0 0 14px;
                    font-size: 24px;
                    line-height: 1.1;
                }

                .chips {
                    display: flex;
                    flex-wrap: wrap;
                    gap: 10px;
                }

                .chip {
                    padding: 10px 12px;
                    border-radius: 8px;
                    border: 1px solid var(--line);
                    background: var(--surface-soft);
                    font-size: 12px;
                    font-weight: 700;
                }

                .muted {
                    color: var(--muted);
                    line-height: 1.6;
                }

                .table-wrap {
                    padding: 0;
                    overflow: hidden;
                    margin-bottom: 22px;
                }

                table {
                    width: 100%;
                    border-collapse: collapse;
                }

                thead th {
                    background: #fff0e8;
                    color: var(--ink);
                    font-size: 12px;
                    font-weight: 800;
                    text-align: left;
                    padding: 12px 14px;
                    border-bottom: 1px solid var(--line);
                }

                tbody td {
                    padding: 12px 14px;
                    border-bottom: 1px solid #f4ece5;
                    font-size: 13px;
                    vertical-align: top;
                }

                tbody tr:nth-child(even) td {
                    background: #fffbf7;
                }

                .bar-list {
                    display: grid;
                    gap: 12px;
                }

                .bar-row {
                    display: grid;
                    grid-template-columns: minmax(140px, 220px) 1fr 90px;
                    gap: 12px;
                    align-items: center;
                }

                .bar-label,
                .bar-value {
                    font-size: 13px;
                    font-weight: 700;
                }

                .track {
                    height: 16px;
                    border-radius: 999px;
                    background: #ffe7ee;
                    overflow: hidden;
                }

                .fill {
                    height: 100%;
                    border-radius: 999px;
                    background: linear-gradient(90deg, var(--accent) 0%, #6fa992 100%);
                }

                @media print {
                    body { padding: 0; background: #fff; }
                    .hero, .section, .table-wrap, .summary-card { box-shadow: none; }
                }
            </style>
        </head>
        <body>
            <main class="page">
                <section class="hero">
                    <div class="eyebrow">{{WebUtility.HtmlEncode(LanguageManager.Get("ReportViewerWindowTitle"))}}</div>
                    <h1>{{safeTitle}}</h1>
                    <p class="subtitle">{{safeSubtitle}}</p>
                    <div class="meta">{{WebUtility.HtmlEncode(LanguageManager.Format("ReportGeneratedAtLine", generatedAt))}}</div>
                </section>
                {{bodyContent}}
            </main>
        </body>
        </html>
        """;
    }

    private static string BuildSummaryCardsHtml(IReadOnlyList<(string Label, string Value)> cards)
    {
        var builder = new StringBuilder();
        builder.AppendLine("""<section class="summary-grid">""");

        foreach (var card in cards)
        {
            builder.AppendLine($"""
                <article class="summary-card">
                    <div class="label">{WebUtility.HtmlEncode(card.Label)}</div>
                    <div class="value">{WebUtility.HtmlEncode(card.Value)}</div>
                </article>
                """);
        }

        builder.AppendLine("</section>");
        return builder.ToString();
    }

    private static string BuildFilterChipSectionHtml(IReadOnlyList<(string Label, string Value)> filters)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"""
            <section class="section">
                <h2>{WebUtility.HtmlEncode(LanguageManager.Get("ReportSectionFilters"))}</h2>
                <div class="chips">
            """);

        foreach (var filter in filters)
        {
            builder.AppendLine($"""
                <div class="chip">{WebUtility.HtmlEncode($"{filter.Label}: {filter.Value}")}</div>
                """);
        }

        builder.AppendLine("""
                </div>
            </section>
            """);
        return builder.ToString();
    }

    private static string BuildInsightsHtml(
        IReadOnlyList<DashboardSeriesPoint> statusPoints,
        IReadOnlyList<DashboardSeriesPoint> categoryPoints,
        IReadOnlyList<DashboardSeriesPoint> monthPoints)
    {
        var insights = BuildInsightLines(statusPoints, categoryPoints, monthPoints);
        var builder = new StringBuilder();
        builder.AppendLine($"""
            <section class="section">
                <h2>{WebUtility.HtmlEncode(LanguageManager.Get("ReportSectionInsights"))}</h2>
            """);

        foreach (var insight in insights)
        {
            builder.AppendLine($"""<p class="muted">{WebUtility.HtmlEncode(insight)}</p>""");
        }

        builder.AppendLine("</section>");
        return builder.ToString();
    }

    private static string BuildBarSectionHtml(string title, IReadOnlyList<DashboardSeriesPoint> points)
    {
        if (points.Count == 0)
        {
            return $"""
                <section class="section">
                    <h2>{WebUtility.HtmlEncode(title)}</h2>
                    <p class="muted">{WebUtility.HtmlEncode(LanguageManager.Get("ReportNoData"))}</p>
                </section>
                """;
        }

        var maxValue = points.Max(point => point.Value);
        if (maxValue <= 0)
        {
            maxValue = 1;
        }

        var builder = new StringBuilder();
        builder.AppendLine($"""
            <section class="section">
                <h2>{WebUtility.HtmlEncode(title)}</h2>
                <div class="bar-list">
            """);

        foreach (var point in points)
        {
            var width = Math.Max(4m, Math.Round(point.Value / maxValue * 100m, 2));
            builder.AppendLine($"""
                <div class="bar-row">
                    <div class="bar-label">{WebUtility.HtmlEncode(point.Label)}</div>
                    <div class="track"><div class="fill" style="width:{width.ToString(CultureInfo.InvariantCulture)}%"></div></div>
                    <div class="bar-value">{WebUtility.HtmlEncode(FormatMoney(point.Value))}</div>
                </div>
                """);
        }

        builder.AppendLine("""
                </div>
            </section>
            """);
        return builder.ToString();
    }

    private static string BuildSeriesTableHtml(
        string title,
        IReadOnlyList<DashboardSeriesPoint> points,
        string labelHeader,
        string valueHeader,
        string secondaryHeader)
    {
        if (points.Count == 0)
        {
            return $"""
                <section class="section">
                    <h2>{WebUtility.HtmlEncode(title)}</h2>
                    <p class="muted">{WebUtility.HtmlEncode(LanguageManager.Get("ReportNoData"))}</p>
                </section>
                """;
        }

        var builder = new StringBuilder();
        builder.AppendLine($"""
            <section class="section">
                <h2>{WebUtility.HtmlEncode(title)}</h2>
            </section>
            <section class="table-wrap">
                <table>
                    <thead>
                        <tr>
                            <th>{WebUtility.HtmlEncode(labelHeader)}</th>
                            <th>{WebUtility.HtmlEncode(valueHeader)}</th>
                            <th>{WebUtility.HtmlEncode(secondaryHeader)}</th>
                        </tr>
                    </thead>
                    <tbody>
            """);

        foreach (var point in points)
        {
            builder.AppendLine($"""
                <tr>
                    <td>{WebUtility.HtmlEncode(point.Label)}</td>
                    <td>{WebUtility.HtmlEncode(FormatMoney(point.Value))}</td>
                    <td>{WebUtility.HtmlEncode(point.SecondaryValue.ToString("N0", CultureInfo.CurrentCulture))}</td>
                </tr>
                """);
        }

        builder.AppendLine("""
                    </tbody>
                </table>
            </section>
            """);

        return builder.ToString();
    }

    private static string BuildTableSectionHtml(string title, DataTable table, IReadOnlyList<DataColumn> visibleColumns)
    {
        if (table.Rows.Count == 0 || visibleColumns.Count == 0)
        {
            return $"""
                <section class="section">
                    <h2>{WebUtility.HtmlEncode(title)}</h2>
                    <p class="muted">{WebUtility.HtmlEncode(LanguageManager.Get("ReportNoData"))}</p>
                </section>
                """;
        }

        var builder = new StringBuilder();
        builder.AppendLine($"""
            <section class="section">
                <h2>{WebUtility.HtmlEncode(title)}</h2>
            </section>
            <section class="table-wrap">
                <table>
                    <thead>
                        <tr>
            """);

        foreach (var column in visibleColumns)
        {
            builder.AppendLine($"""<th>{WebUtility.HtmlEncode(ResolveColumnHeader(column.ColumnName))}</th>""");
        }

        builder.AppendLine("""
                        </tr>
                    </thead>
                    <tbody>
            """);

        foreach (DataRow row in table.Rows)
        {
            builder.AppendLine("<tr>");
            foreach (var column in visibleColumns)
            {
                builder.AppendLine($"""<td>{WebUtility.HtmlEncode(FormatCellValue(row[column], column.ColumnName))}</td>""");
            }

            builder.AppendLine("</tr>");
        }

        builder.AppendLine("""
                    </tbody>
                </table>
            </section>
            """);

        return builder.ToString();
    }
}

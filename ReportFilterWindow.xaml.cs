using System.Globalization;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using BabyShop.Configuration;
using BabyShop.Infrastructure;
using BabyShop.Localization;
using BabyShop.Models;
using BabyShop.Reporting;
using BabyShop.Repositories;
using Microsoft.Win32;

namespace BabyShop;

public partial class ReportFilterWindow : Window
{
    private readonly ReportKind _reportKind;
    private readonly string? _defaultSourceTableName;
    private readonly BabyShopRepository _repository;
    private readonly ReportComposer _composer;
    private bool _isBusy;
    private List<LookupOption> _clientOptions = [];
    private List<LookupOption> _productOptions = [];
    private List<LookupOption> _fabricOptions = [];
    private List<LookupOption> _auditUserOptions = [];
    private List<LookupOption> _auditActionOptions = [];
    private ReportRenderResult? _renderResult;
    private string? _htmlFilePath;

    public ReportRequest? RequestedReport { get; private set; }

    public ReportFilterWindow(ReportKind reportKind, string? defaultSourceTableName = null)
    {
        InitializeComponent();
        WindowAppearance.ApplySharedIcon(this);
        _reportKind = reportKind;
        _defaultSourceTableName = defaultSourceTableName;
        _repository = new BabyShopRepository(new DbHelper(DatabaseSettings.BuildConnectionString()));
        _composer = new ReportComposer(_repository);
        LanguageManager.LanguageChanged += LanguageManager_LanguageChanged;
        ConfigureMode();
        ApplyLocalization();
    }

    private void ConfigureMode()
    {
        var isAllRecords = _reportKind == ReportKind.AllRecords;
        var isAudit = _reportKind == ReportKind.Audit;

        TableSelectionPanel.Visibility = isAllRecords ? Visibility.Visible : Visibility.Collapsed;
        DashboardFilterPanel.Visibility = !isAllRecords && !isAudit ? Visibility.Visible : Visibility.Collapsed;
        AuditFilterPanel.Visibility = isAudit ? Visibility.Visible : Visibility.Collapsed;

        HeaderIconPath.Data = _reportKind switch
        {
            ReportKind.AllRecords => Geometry.Parse("M4,3 L14,3 L14,17 L4,17 Z M7,7 L11,7 M7,10 L11,10 M7,13 L10,13"),
            ReportKind.FilteredData => Geometry.Parse("M3,4 L15,4 L10,10 L10,16 L8,15 L8,10 Z"),
            ReportKind.Audit => Geometry.Parse("M4,4 L14,4 L14,15 L4,15 Z M6,7 L12,7 M6,10 L10,10 M6,13 L11,13"),
            _ => Geometry.Parse("M4,15 L4,9 M9,15 L9,4 M14,15 L14,11 M3,15 L15,15")
        };

        SourceTableComboBox.ItemsSource = ReportCatalog.TableDefinitions
            .Select(definition => new LookupOption
            {
                Value = definition.SourceTableName,
                Label = LanguageManager.Get(definition.LabelKey)
            })
            .ToList();

        SourceTableComboBox.SelectedValue = ReportCatalog.GetDefaultTable(_defaultSourceTableName).SourceTableName;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_reportKind == ReportKind.AllRecords)
        {
            StatusTextBlock.Text = LanguageManager.Get("ReportFiltersReady");
            return;
        }

        if (_reportKind == ReportKind.Audit)
        {
            await LoadAuditFilterOptionsAsync();
            return;
        }

        await LoadDashboardFilterOptionsAsync();
    }

    private async Task LoadDashboardFilterOptionsAsync()
    {
        SetBusyState(true, LanguageManager.Get("ReportLoadingFilters"));

        try
        {
            var clientsTask = _repository.GetDashboardLookupOptionsAsync("c_fullname");
            var productsTask = _repository.GetDashboardLookupOptionsAsync("product_title");
            var fabricsTask = _repository.GetDashboardLookupOptionsAsync("fabric_type");

            await Task.WhenAll(clientsTask, productsTask, fabricsTask);

            _clientOptions = clientsTask.Result.ToList();
            _productOptions = productsTask.Result.ToList();
            _fabricOptions = fabricsTask.Result.ToList();

            RefreshDashboardFilterItems();
            StatusTextBlock.Text = LanguageManager.Get("ReportFiltersReady");
        }
        catch (Exception exception)
        {
            StatusTextBlock.Text = exception.Message;
            MessageBox.Show(exception.Message, LanguageManager.Get("ReportFilterWindowTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusyState(false, StatusTextBlock.Text);
        }
    }

    private void RefreshDashboardFilterItems()
    {
        var selectedClient = ClientComboBox.SelectedValue as string;
        var selectedStatus = StatusComboBox.SelectedValue as string;
        var selectedProduct = ProductComboBox.SelectedValue as string;
        var selectedFabric = FabricTypeComboBox.SelectedValue as string;

        ClientComboBox.ItemsSource = BuildOptionsWithAll(_clientOptions, "AllClients");
        StatusComboBox.ItemsSource = BuildStatusOptions();
        ProductComboBox.ItemsSource = BuildOptionsWithAll(_productOptions, "AllProducts");
        FabricTypeComboBox.ItemsSource = BuildOptionsWithAll(_fabricOptions, "AllFabricTypes");

        ClientComboBox.SelectedValue = selectedClient;
        StatusComboBox.SelectedValue = selectedStatus;
        ProductComboBox.SelectedValue = selectedProduct;
        FabricTypeComboBox.SelectedValue = selectedFabric;

        ClientComboBox.SelectedIndex = ClientComboBox.SelectedIndex < 0 ? 0 : ClientComboBox.SelectedIndex;
        StatusComboBox.SelectedIndex = StatusComboBox.SelectedIndex < 0 ? 0 : StatusComboBox.SelectedIndex;
        ProductComboBox.SelectedIndex = ProductComboBox.SelectedIndex < 0 ? 0 : ProductComboBox.SelectedIndex;
        FabricTypeComboBox.SelectedIndex = FabricTypeComboBox.SelectedIndex < 0 ? 0 : FabricTypeComboBox.SelectedIndex;
    }

    private async Task LoadAuditFilterOptionsAsync()
    {
        SetBusyState(true, LanguageManager.Get("ReportLoadingFilters"));

        try
        {
            var usersTask = _repository.GetAuditUserOptionsAsync();
            var actionsTask = _repository.GetAuditActionOptionsAsync();

            await Task.WhenAll(usersTask, actionsTask);

            _auditUserOptions = usersTask.Result.ToList();
            _auditActionOptions = actionsTask.Result.ToList();

            RefreshAuditFilterItems();
            StatusTextBlock.Text = LanguageManager.Get("ReportFiltersReady");
        }
        catch (Exception exception)
        {
            StatusTextBlock.Text = exception.Message;
            MessageBox.Show(exception.Message, LanguageManager.Get("ReportFilterWindowTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusyState(false, StatusTextBlock.Text);
        }
    }

    private void RefreshAuditFilterItems()
    {
        var selectedUser = AuditUserComboBox.SelectedValue as string;
        var selectedAction = AuditActionComboBox.SelectedValue as string;

        var auditUsers = new List<LookupOption>
        {
            new() { Value = string.Empty, Label = LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Все пользователи" : "All users" }
        };
        auditUsers.AddRange(_auditUserOptions);
        AuditUserComboBox.ItemsSource = auditUsers;

        var auditActions = new List<LookupOption>
        {
            new() { Value = string.Empty, Label = LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Все действия" : "All actions" }
        };
        auditActions.AddRange(_auditActionOptions);
        AuditActionComboBox.ItemsSource = auditActions;

        AuditUserComboBox.SelectedValue = selectedUser;
        AuditActionComboBox.SelectedValue = selectedAction;

        AuditUserComboBox.SelectedIndex = AuditUserComboBox.SelectedIndex < 0 ? 0 : AuditUserComboBox.SelectedIndex;
        AuditActionComboBox.SelectedIndex = AuditActionComboBox.SelectedIndex < 0 ? 0 : AuditActionComboBox.SelectedIndex;
    }

    private List<LookupOption> BuildOptionsWithAll(IEnumerable<LookupOption> options, string allKey)
    {
        return
        [
            new LookupOption { Value = string.Empty, Label = LanguageManager.Get(allKey) },
            .. options
        ];
    }

    private List<LookupOption> BuildStatusOptions()
    {
        return
        [
            new LookupOption { Value = string.Empty, Label = LanguageManager.Get("AllStatuses") },
            new LookupOption { Value = "Pending", Label = LanguageManager.Get("StatusPending") },
            new LookupOption { Value = "Shipped", Label = LanguageManager.Get("StatusShipped") },
            new LookupOption { Value = "Completed", Label = LanguageManager.Get("StatusCompleted") }
        ];
    }

    private async void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        try
        {
            RequestedReport = BuildRequest();
            await GenerateReportAsync(RequestedReport);
        }
        catch (Exception exception)
        {
            StatusTextBlock.Text = exception.Message;
            MessageBox.Show(exception.Message, LanguageManager.Get("ReportFilterWindowTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task GenerateReportAsync(ReportRequest request)
    {
        SetBusyState(true, LanguageManager.Get("ReportStatusLoading"));

        try
        {
            _renderResult = await _composer.BuildAsync(request);
            _htmlFilePath = null;
            GenerateActionsPanel.Visibility = Visibility.Collapsed;
            ReportActionsPanel.Visibility = Visibility.Visible;
            StatusTextBlock.Text = LanguageManager.Get("ReportStatusReady");
            MessageBox.Show(
                "Отчет сгенерирован.",
                LanguageManager.Get("ReportFilterWindowTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch
        {
            _renderResult = null;
            _htmlFilePath = null;
            GenerateActionsPanel.Visibility = Visibility.Visible;
            ReportActionsPanel.Visibility = Visibility.Collapsed;
            throw;
        }
        finally
        {
            SetBusyState(false, StatusTextBlock.Text);
        }
    }

    private ReportRequest BuildRequest()
    {
        return _reportKind switch
        {
            ReportKind.AllRecords => new ReportRequest
            {
                Kind = _reportKind,
                SourceTableName = SourceTableComboBox.SelectedValue as string ?? ReportCatalog.GetDefaultTable(_defaultSourceTableName).SourceTableName
            },
            ReportKind.FilteredData or ReportKind.Analytics => new ReportRequest
            {
                Kind = _reportKind,
                Filter = BuildDashboardFilter()
            },
            ReportKind.Audit => new ReportRequest
            {
                Kind = _reportKind,
                AuditFilter = BuildAuditFilter()
            },
            _ => throw new InvalidOperationException("The selected report type is not supported.")
        };
    }

    private DashboardFilter BuildDashboardFilter()
    {
        var dateFrom = DateFromPicker.SelectedDate;
        var dateTo = DateToPicker.SelectedDate;

        if (dateFrom.HasValue && dateTo.HasValue && dateFrom > dateTo)
        {
            throw new InvalidOperationException(LanguageManager.Get("DashboardDateRangeError"));
        }

        var minAmount = ParseNullableDecimal(MinAmountTextBox.Text, "DashboardMinAmount");
        var maxAmount = ParseNullableDecimal(MaxAmountTextBox.Text, "DashboardMaxAmount");

        if (minAmount.HasValue && maxAmount.HasValue && minAmount > maxAmount)
        {
            throw new InvalidOperationException(LanguageManager.Get("DashboardAmountRangeError"));
        }

        return new DashboardFilter
        {
            DateFrom = dateFrom,
            DateTo = dateTo,
            ClientName = ReadSelectedValue(ClientComboBox),
            Status = ReadSelectedValue(StatusComboBox),
            ProductTitle = ReadSelectedValue(ProductComboBox),
            FabricType = ReadSelectedValue(FabricTypeComboBox),
            MinPrice = minAmount,
            MaxPrice = maxAmount
        };
    }

    private AuditReportFilter BuildAuditFilter()
    {
        var dateFrom = AuditDateFromPicker.SelectedDate;
        var dateTo = AuditDateToPicker.SelectedDate;

        if (dateFrom.HasValue && dateTo.HasValue && dateFrom > dateTo)
        {
            throw new InvalidOperationException(LanguageManager.Get("DashboardDateRangeError"));
        }

        return new AuditReportFilter
        {
            DateFrom = dateFrom,
            DateTo = dateTo,
            Username = ReadSelectedValue(AuditUserComboBox),
            ActionType = ReadSelectedValue(AuditActionComboBox)
        };
    }

    private static decimal? ParseNullableDecimal(string text, string fieldKey)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var currentValue))
        {
            return currentValue;
        }

        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariantValue))
        {
            return invariantValue;
        }

        throw new InvalidOperationException(LanguageManager.Format("NumericValidation", LanguageManager.Get(fieldKey)));
    }

    private static string? ReadSelectedValue(System.Windows.Controls.ComboBox comboBox)
    {
        var value = comboBox.SelectedValue as string;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void PrintButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy || _renderResult?.Document is not IDocumentPaginatorSource source)
        {
            return;
        }

        try
        {
            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() != true)
            {
                return;
            }

            printDialog.PrintDocument(source.DocumentPaginator, _renderResult.Title);
            StatusTextBlock.Text = LanguageManager.Get("ReportPrinted");
        }
        catch (Exception exception)
        {
            StatusTextBlock.Text = exception.Message;
            MessageBox.Show(exception.Message, LanguageManager.Get("ReportFilterWindowTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ExportPdfButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy || _renderResult is null)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf",
            FileName = $"{_renderResult.SuggestedFileName}.pdf",
            AddExtension = true,
            DefaultExt = ".pdf"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        SetBusyState(true, LanguageManager.Get("ReportStatusExportingPdf"));

        try
        {
            var htmlPath = await EnsureHtmlFileAsync();
            await ExportPdfFromHtmlAsync(htmlPath, dialog.FileName);
            StatusTextBlock.Text = LanguageManager.Get("ReportPdfExported");

            Process.Start(new ProcessStartInfo
            {
                FileName = dialog.FileName,
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            StatusTextBlock.Text = exception.Message;
            MessageBox.Show(exception.Message, LanguageManager.Get("ReportFilterWindowTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusyState(false, StatusTextBlock.Text);
        }
    }

    private async void OpenBrowserButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy || _renderResult is null)
        {
            return;
        }

        SetBusyState(true, LanguageManager.Get("ReportStatusPreparingBrowser"));

        try
        {
            var htmlPath = await EnsureHtmlFileAsync();
            Process.Start(new ProcessStartInfo
            {
                FileName = htmlPath,
                UseShellExecute = true
            });
            StatusTextBlock.Text = LanguageManager.Get("ReportBrowserOpened");
        }
        catch (Exception exception)
        {
            StatusTextBlock.Text = exception.Message;
            MessageBox.Show(exception.Message, LanguageManager.Get("ReportFilterWindowTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusyState(false, StatusTextBlock.Text);
        }
    }

    private void ApplyLocalization()
    {
        Title = LanguageManager.Get("ReportFilterWindowTitle");
        HeaderTitleTextBlock.Text = GetHeaderTitleText();
        HeaderSubtitleTextBlock.Text = GetHeaderSubtitleText();
        ReportModeTagTextBlock.Text = GetModeTagText();
        SourceTableLabelTextBlock.Text = LanguageManager.Get("ReportSourceTable");
        ClientFilterLabelTextBlock.Text = LanguageManager.Get("DashboardClient");
        StatusFilterLabelTextBlock.Text = LanguageManager.Get("DashboardStatus");
        ProductFilterLabelTextBlock.Text = LanguageManager.Get("DashboardProduct");
        FabricFilterLabelTextBlock.Text = LanguageManager.Get("DashboardFabricType");
        DateFromLabelTextBlock.Text = LanguageManager.Get("DashboardDateFrom");
        DateToLabelTextBlock.Text = LanguageManager.Get("DashboardDateTo");
        AuditDateFromLabelTextBlock.Text = LanguageManager.Get("DashboardDateFrom");
        AuditDateToLabelTextBlock.Text = LanguageManager.Get("DashboardDateTo");
        AuditUserLabelTextBlock.Text = LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Пользователь" : "User";
        AuditActionLabelTextBlock.Text = LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Тип действия" : "Action type";
        MinAmountLabelTextBlock.Text = LanguageManager.Get("DashboardMinAmount");
        MaxAmountLabelTextBlock.Text = LanguageManager.Get("DashboardMaxAmount");
        FilterHintTextBlock.Text = LanguageManager.Get("ReportFilterHint");
        FooterHelpTextBlock.Text = LanguageManager.Get("ReportFilterFooter");
        GenerateButton.Content = LanguageManager.Get("ReportGenerate");
        CancelButton.Content = LanguageManager.Get("Cancel");
        PrintButton.Content = LanguageManager.Get("ReportPrint");
        ExportPdfButton.Content = LanguageManager.Get("ReportExportPdf");
        OpenBrowserButton.Content = LanguageManager.Get("ReportOpenBrowser");
        CloseButton.Content = LanguageManager.Get("Cancel");

        ConfigureMode();
        if (_reportKind != ReportKind.AllRecords)
        {
            if (_reportKind == ReportKind.Audit)
            {
                RefreshAuditFilterItems();
            }
            else
            {
                RefreshDashboardFilterItems();
            }
        }
    }

    private string GetHeaderTitleText() => _reportKind switch
    {
        ReportKind.AllRecords => LanguageManager.Get("ReportAllRecordsTitle"),
        ReportKind.FilteredData => LanguageManager.Get("ReportFilteredTitle"),
        ReportKind.Analytics => LanguageManager.Get("ReportAnalyticsTitle"),
        _ => LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Журнал аудита" : "Audit log"
    };

    private string GetHeaderSubtitleText() => _reportKind switch
    {
        ReportKind.AllRecords => LanguageManager.Get("ReportFilterAllSubtitle"),
        ReportKind.FilteredData => LanguageManager.Get("ReportFilterFilteredSubtitle"),
        ReportKind.Analytics => LanguageManager.Get("ReportFilterAnalyticsSubtitle"),
        _ => LanguageManager.CurrentLanguage == AppLanguage.Russian
            ? "Выберите период, пользователя и тип действия для отчета по аудиту."
            : "Choose the period, user, and action type for the audit report."
    };

    private string GetModeTagText() => _reportKind switch
    {
        ReportKind.AllRecords => LanguageManager.Get("ReportMenuAllRecords"),
        ReportKind.FilteredData => LanguageManager.Get("ReportMenuFiltered"),
        ReportKind.Analytics => LanguageManager.Get("ReportMenuAnalytics"),
        _ => LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Журнал аудита" : "Audit log"
    };

    private void SetBusyState(bool isBusy, string? statusText = null)
    {
        _isBusy = isBusy;
        GenerateButton.IsEnabled = !isBusy;
        CancelButton.IsEnabled = !isBusy;
        PrintButton.IsEnabled = !isBusy && _renderResult?.Document is not null;
        ExportPdfButton.IsEnabled = !isBusy && _renderResult?.Document is not null;
        OpenBrowserButton.IsEnabled = !isBusy && _renderResult?.Document is not null;
        CloseButton.IsEnabled = !isBusy;
        SourceTableComboBox.IsEnabled = !isBusy;
        ClientComboBox.IsEnabled = !isBusy;
        StatusComboBox.IsEnabled = !isBusy;
        ProductComboBox.IsEnabled = !isBusy;
        FabricTypeComboBox.IsEnabled = !isBusy;
        AuditDateFromPicker.IsEnabled = !isBusy;
        AuditDateToPicker.IsEnabled = !isBusy;
        AuditUserComboBox.IsEnabled = !isBusy;
        AuditActionComboBox.IsEnabled = !isBusy;
        DateFromPicker.IsEnabled = !isBusy;
        DateToPicker.IsEnabled = !isBusy;
        MinAmountTextBox.IsEnabled = !isBusy;
        MaxAmountTextBox.IsEnabled = !isBusy;
        Mouse.OverrideCursor = isBusy ? System.Windows.Input.Cursors.Wait : null;

        if (!string.IsNullOrWhiteSpace(statusText))
        {
            StatusTextBlock.Text = statusText;
        }
    }

    private async Task<string> EnsureHtmlFileAsync()
    {
        if (_renderResult is null)
        {
            throw new InvalidOperationException(LanguageManager.Get("ReportNotReady"));
        }

        var folderPath = Path.Combine(Path.GetTempPath(), "BabyShopReports");
        Directory.CreateDirectory(folderPath);

        _htmlFilePath ??= Path.Combine(folderPath, $"{_renderResult.SuggestedFileName}.html");
        await File.WriteAllTextAsync(_htmlFilePath, _renderResult.HtmlContent);
        return _htmlFilePath;
    }

    private static async Task ExportPdfFromHtmlAsync(string htmlPath, string pdfPath)
    {
        var edgePath = FindEdgePath();
        if (string.IsNullOrWhiteSpace(edgePath))
        {
            throw new InvalidOperationException(LanguageManager.Get("ReportEdgeMissing"));
        }

        if (File.Exists(pdfPath))
        {
            File.Delete(pdfPath);
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = edgePath,
                Arguments = $"--headless=new --disable-gpu --print-to-pdf=\"{pdfPath}\" \"{new Uri(htmlPath).AbsoluteUri}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            }
        };

        process.Start();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0 || !File.Exists(pdfPath))
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error)
                ? LanguageManager.Get("ReportPdfExportFailed")
                : error.Trim());
        }
    }

    private static string? FindEdgePath()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private void LanguageManager_LanguageChanged(object? sender, EventArgs e)
    {
        ApplyLocalization();
    }

    protected override void OnClosed(EventArgs e)
    {
        LanguageManager.LanguageChanged -= LanguageManager_LanguageChanged;
        base.OnClosed(e);
    }
}

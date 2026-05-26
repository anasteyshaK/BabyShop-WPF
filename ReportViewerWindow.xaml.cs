using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using BabyShop.Configuration;
using BabyShop.Infrastructure;
using BabyShop.Localization;
using BabyShop.Reporting;
using BabyShop.Repositories;
using Microsoft.Win32;

namespace BabyShop;

public partial class ReportViewerWindow : Window
{
    private readonly ReportRequest _request;
    private readonly ReportComposer _composer;
    private bool _isBusy;
    private ReportRenderResult? _renderResult;
    private string? _htmlFilePath;

    public ReportViewerWindow(ReportRequest request)
    {
        InitializeComponent();
        WindowAppearance.ApplySharedIcon(this);
        _request = request;
        _composer = new ReportComposer(new BabyShopRepository(new DbHelper(DatabaseSettings.BuildConnectionString())));
        LanguageManager.LanguageChanged += LanguageManager_LanguageChanged;
        ApplyLocalization();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadReportAsync();
    }

    private async Task LoadReportAsync()
    {
        SetBusyState(true, LanguageManager.Get("ReportStatusLoading"));

        try
        {
            _renderResult = await _composer.BuildAsync(_request);
            ReportDocumentViewer.Document = _renderResult.Document;
            ViewerTitleTextBlock.Text = _renderResult.Title;
            ViewerSubtitleTextBlock.Text = _renderResult.Subtitle;
            Title = $"{_renderResult.Title} - {LanguageManager.Get("AppTitle")}";
            ViewerStatusTextBlock.Text = LanguageManager.Get("ReportStatusReady");
        }
        catch (Exception exception)
        {
            ViewerStatusTextBlock.Text = exception.Message;
            MessageBox.Show(exception.Message, LanguageManager.Get("ReportViewerWindowTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusyState(false, ViewerStatusTextBlock.Text);
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
            ViewerStatusTextBlock.Text = LanguageManager.Get("ReportBrowserOpened");
        }
        catch (Exception exception)
        {
            ViewerStatusTextBlock.Text = exception.Message;
            MessageBox.Show(exception.Message, LanguageManager.Get("ReportViewerWindowTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusyState(false, ViewerStatusTextBlock.Text);
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
            ViewerStatusTextBlock.Text = LanguageManager.Get("ReportPdfExported");

            Process.Start(new ProcessStartInfo
            {
                FileName = dialog.FileName,
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            ViewerStatusTextBlock.Text = exception.Message;
            MessageBox.Show(exception.Message, LanguageManager.Get("ReportViewerWindowTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusyState(false, ViewerStatusTextBlock.Text);
        }
    }

    private void PrintButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy || ReportDocumentViewer.Document is not IDocumentPaginatorSource source)
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

            printDialog.PrintDocument(source.DocumentPaginator, ViewerTitleTextBlock.Text);
            ViewerStatusTextBlock.Text = LanguageManager.Get("ReportPrinted");
        }
        catch (Exception exception)
        {
            ViewerStatusTextBlock.Text = exception.Message;
            MessageBox.Show(exception.Message, LanguageManager.Get("ReportViewerWindowTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
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

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ApplyLocalization()
    {
        if (_renderResult is null)
        {
            Title = LanguageManager.Get("ReportViewerWindowTitle");
            ViewerTitleTextBlock.Text = LanguageManager.Get("ReportViewerWindowTitle");
            ViewerSubtitleTextBlock.Text = LanguageManager.Get(GetInitialSubtitleKey());
        }

        ViewerTagTextBlock.Text = LanguageManager.Get("ReportViewerWindowTitle");
        PrintButton.Content = LanguageManager.Get("ReportPrint");
        ExportPdfButton.Content = LanguageManager.Get("ReportExportPdf");
        OpenBrowserButton.Content = LanguageManager.Get("ReportOpenBrowser");
        CloseButton.Content = LanguageManager.Get("Cancel");
        ViewerStatusTextBlock.Text = string.IsNullOrWhiteSpace(ViewerStatusTextBlock.Text)
            ? LanguageManager.Get("ReportStatusIdle")
            : ViewerStatusTextBlock.Text;
    }

    private string GetInitialSubtitleKey() => _request.Kind switch
    {
        ReportKind.AllRecords => "ReportFilterAllSubtitle",
        ReportKind.FilteredData => "ReportFilterFilteredSubtitle",
        _ => "ReportFilterAnalyticsSubtitle"
    };

    private void SetBusyState(bool isBusy, string? statusText = null)
    {
        _isBusy = isBusy;
        PrintButton.IsEnabled = !isBusy && ReportDocumentViewer.Document is not null;
        ExportPdfButton.IsEnabled = !isBusy && ReportDocumentViewer.Document is not null;
        OpenBrowserButton.IsEnabled = !isBusy && ReportDocumentViewer.Document is not null;
        CloseButton.IsEnabled = !isBusy;
        Mouse.OverrideCursor = isBusy ? System.Windows.Input.Cursors.Wait : null;

        if (!string.IsNullOrWhiteSpace(statusText))
        {
            ViewerStatusTextBlock.Text = statusText;
        }
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

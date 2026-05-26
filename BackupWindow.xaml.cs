using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using BabyShop.Configuration;
using BabyShop.Infrastructure;
using BabyShop.Localization;
using BabyShop.Models;
using BabyShop.Repositories;
using BabyShop.Services;
using Microsoft.Win32;

namespace BabyShop;

public partial class BackupWindow : Window
{
    private readonly AppUser _currentUser;
    private readonly BackupService _backupService;
    private bool _isBusy;

    public BackupWindow(AppUser currentUser)
    {
        InitializeComponent();
        WindowAppearance.ApplySharedIcon(this);
        _currentUser = currentUser;
        var repository = new BabyShopRepository(new DbHelper(DatabaseSettings.BuildConnectionString()));
        _backupService = new BackupService(repository, currentUser);
        LanguageManager.LanguageChanged += LanguageManager_LanguageChanged;
        ApplyLocalization();
        UpdatePermissionState();
        SetLastBackup(null);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshDataAsync();
    }

    private async Task RefreshDataAsync()
    {
        SetBusyState(true);

        try
        {
            StatusTextBlock.Text = LanguageManager.Get("BackupStatusLoading");
            var lastBackupTask = _backupService.GetLastBackupAsync();
            var historyTask = _backupService.CanViewBackupHistory
                ? _backupService.GetBackupHistoryAsync()
                : Task.FromResult<IReadOnlyList<BackupHistoryEntry>>(Array.Empty<BackupHistoryEntry>());

            await Task.WhenAll(lastBackupTask, historyTask);

            SetLastBackup(lastBackupTask.Result);
            HistoryDataGrid.ItemsSource = historyTask.Result;
            StatusTextBlock.Text = LanguageManager.Get("BackupStatusIdle");
        }
        catch (Exception exception)
        {
            StatusTextBlock.Text = exception.Message;
            MessageBox.Show(exception.Message, LanguageManager.Get("BackupWindowTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusyState(false);
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        await RefreshDataAsync();
    }

    private async void CreateBackupButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = LanguageManager.Get("BackupSelectDestination"),
            Filter = LanguageManager.Get("BackupFileFilter"),
            DefaultExt = ".sql",
            AddExtension = true,
            FileName = $"{DatabaseSettings.Database}_{DateTime.Now:yyyyMMdd_HHmmss}.sql",
            InitialDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BabyShopBackups")
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        SetBusyState(true);
        try
        {
            var result = await _backupService.CreateBackupAsync(dialog.FileName);
            StatusTextBlock.Text = result.Message;
            await RefreshDataAsync();
            MessageBox.Show(
                result.Message,
                LanguageManager.Get(result.Succeeded ? "BackupSuccessTitle" : "BackupFailureTitle"),
                MessageBoxButton.OK,
                result.Succeeded ? MessageBoxImage.Information : MessageBoxImage.Error);
        }
        finally
        {
            SetBusyState(false);
        }
    }

    private async void RestoreBackupButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        var openDialog = new OpenFileDialog
        {
            Title = LanguageManager.Get("BackupSelectSource"),
            Filter = LanguageManager.Get("BackupFileFilter"),
            DefaultExt = ".sql",
            CheckFileExists = true
        };

        if (openDialog.ShowDialog(this) != true)
        {
            return;
        }

        var confirmation = MessageBox.Show(
            LanguageManager.Get("BackupRestoreWarning"),
            LanguageManager.Get("BackupRestoreWarningTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        var safetyBackupChoice = MessageBox.Show(
            LanguageManager.Get("BackupSafetyPrompt"),
            LanguageManager.Get("BackupRestoreWarningTitle"),
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);
        if (safetyBackupChoice == MessageBoxResult.Cancel)
        {
            return;
        }

        SetBusyState(true);
        try
        {
            var result = await _backupService.RestoreBackupAsync(
                openDialog.FileName,
                createSafetyBackup: safetyBackupChoice == MessageBoxResult.Yes);

            StatusTextBlock.Text = result.Message;
            await RefreshDataAsync();
            MessageBox.Show(
                result.Message,
                LanguageManager.Get(result.Succeeded ? "BackupSuccessTitle" : "BackupFailureTitle"),
                MessageBoxButton.OK,
                result.Succeeded ? MessageBoxImage.Information : MessageBoxImage.Error);
        }
        finally
        {
            SetBusyState(false);
        }
    }

    private void ApplyLocalization()
    {
        Title = LanguageManager.Get("BackupWindowTitle");
        WindowTitleTextBlock.Text = LanguageManager.Get("BackupWindowTitle");
        WindowSubtitleTextBlock.Text = LanguageManager.Get("BackupWindowSubtitle");
        CreateBackupButton.Content = LanguageManager.Get("BackupCreate");
        RestoreBackupButton.Content = LanguageManager.Get("BackupRestore");
        RefreshButton.Content = LanguageManager.Get("BackupRefresh");
        LastBackupTitleTextBlock.Text = LanguageManager.Get("BackupLastBackupTitle");
        LastBackupUserLabelTextBlock.Text = $"{LanguageManager.Get("BackupLastBackupUser")}:";
        LastBackupFileLabelTextBlock.Text = $"{LanguageManager.Get("BackupLastBackupFile")}:";
        LastBackupPathLabelTextBlock.Text = $"{LanguageManager.Get("BackupLastBackupPath")}:";
        LastBackupSizeLabelTextBlock.Text = $"{LanguageManager.Get("BackupLastBackupSize")}:";
        LastBackupStatusLabelTextBlock.Text = $"{LanguageManager.Get("BackupLastBackupStatus")}:";
        HistoryTitleTextBlock.Text = LanguageManager.Get("BackupHistoryTitle");
        HistoryAccessDeniedTextBlock.Text = LanguageManager.Get("BackupHistoryAccessDenied");
        OperationColumn.Header = LanguageManager.Get("BackupHistoryOperation");
        UserColumn.Header = LanguageManager.Get("BackupHistoryUser");
        FileColumn.Header = LanguageManager.Get("BackupHistoryFile");
        PathColumn.Header = LanguageManager.Get("BackupHistoryPath");
        SizeColumn.Header = LanguageManager.Get("BackupHistorySize");
        DatabaseColumn.Header = LanguageManager.Get("BackupHistoryDatabase");
        StatusColumn.Header = LanguageManager.Get("BackupHistoryStatus");
        MessageColumn.Header = LanguageManager.Get("BackupHistoryMessage");
        DateColumn.Header = LanguageManager.Get("BackupHistoryDate");

        if (string.IsNullOrWhiteSpace(StatusTextBlock.Text))
        {
            StatusTextBlock.Text = LanguageManager.Get("BackupStatusIdle");
        }
    }

    private void SetLastBackup(BackupHistoryEntry? entry)
    {
        if (entry is null)
        {
            LastBackupUserValueTextBlock.Text = LanguageManager.Get("BackupLastBackupEmpty");
            LastBackupFileValueTextBlock.Text = "-";
            LastBackupPathValueTextBlock.Text = "-";
            LastBackupSizeValueTextBlock.Text = "-";
            LastBackupStatusValueTextBlock.Text = "-";
            LastBackupMessageValueTextBlock.Text = "-";
            LastBackupDateValueTextBlock.Text = "-";
            return;
        }

        LastBackupUserValueTextBlock.Text = entry.Username;
        LastBackupFileValueTextBlock.Text = entry.FileName;
        LastBackupPathValueTextBlock.Text = entry.FilePath;
        LastBackupSizeValueTextBlock.Text = entry.FileSizeDisplay;
        LastBackupStatusValueTextBlock.Text = entry.Status;
        LastBackupMessageValueTextBlock.Text = $"{LanguageManager.Get("BackupLastBackupMessage")}: {entry.Message}";
        LastBackupDateValueTextBlock.Text = $"{LanguageManager.Get("BackupLastBackupDate")}: {entry.CreatedAt.ToString("g", CultureInfo.CurrentCulture)}";
    }

    private void UpdatePermissionState()
    {
        CreateBackupButton.IsEnabled = _backupService.CanCreateBackup;
        RestoreBackupButton.IsEnabled = _backupService.CanRestoreBackup;
        HistoryDataGrid.Visibility = _backupService.CanViewBackupHistory ? Visibility.Visible : Visibility.Collapsed;
        HistoryAccessDeniedTextBlock.Visibility = _backupService.CanViewBackupHistory ? Visibility.Collapsed : Visibility.Visible;
    }

    private void SetBusyState(bool isBusy)
    {
        _isBusy = isBusy;
        CreateBackupButton.IsEnabled = !isBusy && _backupService.CanCreateBackup;
        RestoreBackupButton.IsEnabled = !isBusy && _backupService.CanRestoreBackup;
        RefreshButton.IsEnabled = !isBusy;
        Mouse.OverrideCursor = isBusy ? System.Windows.Input.Cursors.Wait : null;
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

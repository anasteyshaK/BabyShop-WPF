using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using BabyShop.Configuration;
using BabyShop.Infrastructure;
using BabyShop.Models;
using BabyShop.Reporting;
using BabyShop.Repositories;
using BabyShop.Services;
using Microsoft.Win32;

namespace BabyShop;

public partial class UserOrdersWindow : Window
{
    private const int PageSize = 6;
    private readonly string _currentUsername;
    private readonly int? _currentUserId;
    private readonly bool _isGuestAccount;
    private readonly CheckoutCustomerDetails? _knownCustomerDetails;
    private readonly BabyShopRepository _repository;
    private readonly List<UserOrderSummaryViewModel> _allOrders = [];
    private readonly ObservableCollection<UserOrderSummaryViewModel> _visibleOrders = [];
    private int _currentPage = 1;
    private OrderDetailsViewModel? _selectedOrderDetails;

    public UserOrdersWindow(string currentUsername, int? currentUserId = null, bool isGuestAccount = false, CheckoutCustomerDetails? knownCustomerDetails = null)
    {
        InitializeComponent();
        WindowAppearance.ApplySharedIcon(this);

        _currentUsername = currentUsername;
        _currentUserId = currentUserId > 0 ? currentUserId : null;
        _isGuestAccount = isGuestAccount || !_currentUserId.HasValue;
        _knownCustomerDetails = knownCustomerDetails;
        _repository = new BabyShopRepository(new DbHelper(DatabaseSettings.BuildConnectionString()));

        OrdersListBox.ItemsSource = _visibleOrders;
        OrdersListBox.ItemTemplate = BuildOrderCardTemplate();
        ApplyStaticText();
        HideReceiptPreview();
        UpdateActionButtons();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadOrdersAsync();
    }

    private async Task LoadOrdersAsync()
    {
        try
        {
            if (_isGuestAccount || !_currentUserId.HasValue)
            {
                _allOrders.Clear();
                _currentPage = 1;
                RefreshPage();
                return;
            }

            var orders = await _repository.GetCustomerOrdersByUserIdAsync(_currentUserId.Value);
            _allOrders.Clear();
            _allOrders.AddRange(orders);
            _currentPage = 1;

            RefreshPage();
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            RefreshPage();
        }
    }

    private async void OrdersListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OrdersListBox.SelectedItem is not UserOrderSummaryViewModel summary)
        {
            _selectedOrderDetails = null;
            HideReceiptPreview();
            UpdateActionButtons();
            return;
        }

        try
        {
            _selectedOrderDetails = await _repository.GetOrderDetailsAsync(summary.OrderId);
            if (_selectedOrderDetails is null)
            {
                HideReceiptPreview();
                UpdateActionButtons();
                return;
            }

            ApplyReceiptPreview(_selectedOrderDetails);
            UpdateActionButtons();
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenDetailsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedOrderDetails is null)
        {
            return;
        }

        var detailsWindow = new OrderDetailsWindow(_selectedOrderDetails.OrderId)
        {
            Owner = this
        };
        detailsWindow.ShowDialog();
    }

    private async void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedOrderDetails is null)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "HTML receipt (*.html)|*.html",
            FileName = $"order-receipt-{_selectedOrderDetails.OrderId}.html"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            await File.WriteAllTextAsync(dialog.FileName, BuildReceiptHtml(_selectedOrderDetails, autoPrint: false));
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void PrintButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedOrderDetails is null)
        {
            return;
        }

        try
        {
            var folderPath = Path.Combine(Path.GetTempPath(), "BabyShopReceipts");
            Directory.CreateDirectory(folderPath);

            var safeFileName = $"order-receipt-{_selectedOrderDetails.OrderId}-{DateTime.Now:yyyyMMdd-HHmmss}.html";
            var fullPath = Path.Combine(folderPath, safeFileName);
            await File.WriteAllTextAsync(fullPath, BuildReceiptHtml(_selectedOrderDetails, autoPrint: true));

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = fullPath,
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshPage()
    {
        _visibleOrders.Clear();

        var totalPages = Math.Max(1, (int)Math.Ceiling(_allOrders.Count / (double)PageSize));
        _currentPage = Math.Max(1, Math.Min(_currentPage, totalPages));

        foreach (var order in _allOrders.Skip((_currentPage - 1) * PageSize).Take(PageSize))
        {
            _visibleOrders.Add(order);
        }

        OrdersEmptyStateBorder.Visibility = _allOrders.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        OrdersCountTextBlock.Text = $"Всего заказов: {_allOrders.Count}";
        BuildPaginationButtons(totalPages);

        if (_visibleOrders.Count > 0)
        {
            OrdersListBox.SelectedIndex = 0;
        }
        else
        {
            OrdersListBox.SelectedItem = null;
            _selectedOrderDetails = null;
            HideReceiptPreview();
            UpdateActionButtons();
        }
    }

    private void BuildPaginationButtons(int totalPages)
    {
        PaginationPanel.Children.Clear();

        if (totalPages <= 1)
        {
            return;
        }

        PaginationPanel.Children.Add(CreatePageButton("‹", _currentPage > 1, () =>
        {
            _currentPage--;
            RefreshPage();
        }));

        for (var page = 1; page <= totalPages; page++)
        {
            var pageNumber = page;
            var isCurrent = pageNumber == _currentPage;

            PaginationPanel.Children.Add(CreatePageButton(
                pageNumber.ToString(CultureInfo.InvariantCulture),
                !isCurrent,
                () =>
                {
                    _currentPage = pageNumber;
                    RefreshPage();
                },
                isCurrent));
        }

        PaginationPanel.Children.Add(CreatePageButton("›", _currentPage < totalPages, () =>
        {
            _currentPage++;
            RefreshPage();
        }));
    }

    private Button CreatePageButton(string content, bool enabled, Action action, bool isCurrent = false)
    {
        var button = new Button
        {
            Content = content,
            Width = 36,
            Height = 36,
            Margin = new Thickness(8, 0, 0, 0),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Cursor = System.Windows.Input.Cursors.Hand,
            IsEnabled = enabled,
            Background = isCurrent ? (Brush)FindResource("OrdersAccentSoftBrush") : Brushes.White,
            Foreground = isCurrent ? (Brush)FindResource("OrdersAccentStrongBrush") : (Brush)FindResource("OrdersTextBrush"),
            BorderBrush = isCurrent ? (Brush)FindResource("OrdersAccentBrush") : (Brush)FindResource("OrdersLineBrush"),
            BorderThickness = new Thickness(1)
        };

        button.Click += (_, _) => action();
        button.Template = (ControlTemplate)XamlReader.Parse(
            """
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="Button">
                <Border Background="{TemplateBinding Background}"
                        BorderBrush="{TemplateBinding BorderBrush}"
                        BorderThickness="{TemplateBinding BorderThickness}"
                        CornerRadius="12">
                    <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                </Border>
            </ControlTemplate>
            """);

        return button;
    }

    private DataTemplate BuildOrderCardTemplate()
    {
        const string template = """
        <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="104"/>
                    <ColumnDefinition Width="126"/>
                    <ColumnDefinition Width="112"/>
                    <ColumnDefinition Width="92"/>
                    <ColumnDefinition Width="18"/>
                </Grid.ColumnDefinitions>

                <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                    <Border Width="34" Height="34" CornerRadius="12" Background="#FFFFF0F5">
                        <TextBlock HorizontalAlignment="Center" VerticalAlignment="Center" FontSize="14" Foreground="#FFEF5F93" Text="◫"/>
                    </Border>
                    <TextBlock Margin="10,0,0,0" VerticalAlignment="Center" FontSize="18" FontWeight="Black" Foreground="#FF16254B" Text="{Binding OrderId, StringFormat=№ {0}}"/>
                </StackPanel>

                <TextBlock Grid.Column="1" VerticalAlignment="Center" FontSize="12" Foreground="#FF5E6781" Text="{Binding OrderDate, StringFormat=dd.MM.yyyy HH:mm}"/>

                <Border Name="StatusPill" Grid.Column="2" Padding="9,4" HorizontalAlignment="Left" VerticalAlignment="Center" CornerRadius="999" Background="#FFFFF2D8">
                    <TextBlock Name="StatusText" FontSize="11" FontWeight="Bold" Foreground="#FFB37A14" Text="{Binding StatusDisplay}"/>
                </Border>

                <StackPanel Grid.Column="3" Orientation="Horizontal" HorizontalAlignment="Right" VerticalAlignment="Center">
                        <TextBlock HorizontalAlignment="Right" FontSize="14" FontWeight="Black" Foreground="#FF16254B" Text="{Binding TotalCost, StringFormat={}{0:0.##}}"/>
                        <TextBlock HorizontalAlignment="Right" Margin="3,3,0,0" FontSize="9.5" FontWeight="SemiBold" Foreground="#FF8E97AF" Text="MDL"/>
                    <TextBlock Margin="12,0,0,0" VerticalAlignment="Center" FontSize="22" Foreground="#FFBCC3D4" Text="›"/>
                </StackPanel>
            </Grid>
            <DataTemplate.Triggers>
                <DataTrigger Binding="{Binding OrderStatus}" Value="Shipped">
                    <Setter TargetName="StatusPill" Property="Background" Value="#FFEAF2FF"/>
                    <Setter TargetName="StatusText" Property="Foreground" Value="#FF4D7FE6"/>
                </DataTrigger>
                <DataTrigger Binding="{Binding OrderStatus}" Value="Completed">
                    <Setter TargetName="StatusPill" Property="Background" Value="#FFE9F9F0"/>
                    <Setter TargetName="StatusText" Property="Foreground" Value="#FF31A96E"/>
                </DataTrigger>
            </DataTemplate.Triggers>
        </DataTemplate>
        """;

        return (DataTemplate)XamlReader.Parse(template);
    }

    private void ApplyStaticText()
    {
        Title = "Мои заказы";
        OrdersTitleTextBlock.Text = "Мои заказы";
        OrdersSubtitleTextBlock.Text = "История ваших заказов и квитанции";
        OrdersEmptyTitleTextBlock.Text = "Заказы не найдены";
        OrdersEmptySubtitleTextBlock.Text = "После оформления заказа здесь появится история ваших покупок.";
        ReceiptTitleTextBlock.Text = "Квитанция";
        ReceiptEmptyTitleTextBlock.Text = "Выберите заказ";
        ReceiptEmptySubtitleTextBlock.Text = "После выбора заказа справа отобразится квитанция, доступная для просмотра, скачивания и печати.";
        OrdersCountTextBlock.Text = "Всего заказов: 0";
    }

    private void UpdateActionButtons()
    {
        var hasSelection = _selectedOrderDetails is not null;
        OpenDetailsButton.IsEnabled = hasSelection;
        DownloadButton.IsEnabled = hasSelection;
        PrintButton.IsEnabled = hasSelection;
    }

    private void ApplyReceiptPreview(OrderDetailsViewModel order)
    {
        ReceiptRibbonTextBlock.Text = $"№ {order.OrderId.ToString(CultureInfo.CurrentCulture)}";
        ReceiptOrderNumberValueTextBlock.Text = order.OrderId.ToString(CultureInfo.CurrentCulture);
        ReceiptClientValueTextBlock.Text = order.ClientName;
        ReceiptAddressValueTextBlock.Text = string.IsNullOrWhiteSpace(order.DeliveryAddress) ? "—" : order.DeliveryAddress;
        ReceiptStartDateValueTextBlock.Text = FormatDate(order.StartDate);
        ReceiptEndDateValueTextBlock.Text = FormatDate(order.EndDate);
        ReceiptStatusTextBlock.Text = LocalizeStatus(order.OrderStatus);
        ReceiptTotalValueTextBlock.Text = FormatCurrency(order.TotalCost);
        ReceiptPositionsValueTextBlock.Text = order.TotalPositions.ToString(CultureInfo.CurrentCulture);
        ReceiptQuantityValueTextBlock.Text = order.TotalQuantity.ToString(CultureInfo.CurrentCulture);
        ReceiptSummaryTotalValueTextBlock.Text = FormatCurrency(order.TotalCost);
        ReceiptGeneratedTextBlock.Text = $"Сформировано: {DateTime.Now:dd.MM.yyyy HH:mm}";

        ReceiptItemsControl.ItemsSource = order.Items.Select(item => new ReceiptPreviewItem
        {
            ProductTitle = item.ProductTitle,
            Subtitle = string.IsNullOrWhiteSpace(item.FabricType)
                ? item.CategoryName
                : $"{item.CategoryName} • {item.FabricType}",
            ColorLine = string.IsNullOrWhiteSpace(item.Color) ? "Цвет: —" : $"Цвет: {item.Color}",
            Tag = string.IsNullOrWhiteSpace(item.CategoryName) ? "Товар" : item.CategoryName,
            Quantity = item.Quantity.ToString(CultureInfo.CurrentCulture),
            UnitPriceText = FormatCurrency(item.UnitPrice),
            LineTotalText = FormatCurrency(item.LineTotal),
            ResolvedImagePath = ResolveImagePath(item.ImagePath)
        }).ToList();

        ApplyStatusBadge(order.OrderStatus);
        ReceiptPreviewScrollViewer.Visibility = Visibility.Visible;
        ReceiptEmptyStateBorder.Visibility = Visibility.Collapsed;
        ReceiptPreviewScrollViewer.ScrollToHome();
    }

    private void HideReceiptPreview()
    {
        ReceiptPreviewScrollViewer.Visibility = Visibility.Collapsed;
        ReceiptEmptyStateBorder.Visibility = Visibility.Visible;
    }

    private void ApplyStatusBadge(string status)
    {
        switch (status.Trim())
        {
            case "Shipped":
                ReceiptStatusBorder.Background = new SolidColorBrush(Color.FromRgb(232, 243, 255));
                ReceiptStatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(77, 127, 230));
                break;
            case "Completed":
                ReceiptStatusBorder.Background = new SolidColorBrush(Color.FromRgb(230, 247, 238));
                ReceiptStatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(49, 169, 110));
                break;
            default:
                ReceiptStatusBorder.Background = new SolidColorBrush(Color.FromRgb(255, 242, 216));
                ReceiptStatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(179, 122, 20));
                break;
        }
    }

    private static string BuildReceiptHtml(OrderDetailsViewModel order, bool autoPrint)
    {
        return OrderReceiptComposer.BuildHtml(order, autoPrint);
    }

    private static string LocalizeStatus(string status)
    {
        return status.Trim() switch
        {
            "Pending" => "Ожидает",
            "Shipped" => "Отправлен",
            "Completed" => "Завершён",
            _ => string.IsNullOrWhiteSpace(status) ? "Неизвестно" : status
        };
    }

    private static string FormatDate(DateTime? value)
    {
        return value?.ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture) ?? "—";
    }

    private static string FormatCurrency(decimal value)
    {
        return value.ToString("N2", CultureInfo.CurrentCulture);
    }

    private static string ResolveImagePath(string imagePath)
    {
        if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
        {
            return imagePath;
        }

        return StorefrontAssetResolver.ResolveProductImagePath(imagePath);
    }

    private sealed class ReceiptPreviewItem
    {
        public string ProductTitle { get; init; } = string.Empty;
        public string Subtitle { get; init; } = string.Empty;
        public string ColorLine { get; init; } = string.Empty;
        public string Tag { get; init; } = string.Empty;
        public string Quantity { get; init; } = string.Empty;
        public string UnitPriceText { get; init; } = string.Empty;
        public string LineTotalText { get; init; } = string.Empty;
        public string ResolvedImagePath { get; init; } = string.Empty;
    }
}

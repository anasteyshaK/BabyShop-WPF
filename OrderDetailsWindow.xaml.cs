using System.Globalization;
using System.Windows;
using System.Windows.Media;
using BabyShop.Configuration;
using BabyShop.Infrastructure;
using BabyShop.Localization;
using BabyShop.Models;
using BabyShop.Repositories;
using BabyShop.Services;

namespace BabyShop;

public partial class OrderDetailsWindow : Window
{
    private readonly int _orderId;
    private readonly BabyShopRepository _repository;
    private OrderDetailsViewModel? _orderDetails;

    public OrderDetailsWindow(int orderId)
    {
        InitializeComponent();
        WindowAppearance.ApplySharedIcon(this);
        _orderId = orderId;
        _repository = new BabyShopRepository(new DbHelper(DatabaseSettings.BuildConnectionString()));
        LanguageManager.LanguageChanged += LanguageManager_LanguageChanged;
        ApplyStaticLocalization();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadOrderAsync();
    }

    private async Task LoadOrderAsync()
    {
        SetBusyState(true, LanguageManager.CurrentLanguage == AppLanguage.Russian
            ? "Р—Р°РіСЂСѓР·РєР° РґР°РЅРЅС‹С… Р·Р°РєР°Р·Р°..."
            : "Loading order details...");

        try
        {
            _orderDetails = await _repository.GetOrderDetailsAsync(_orderId);
            if (_orderDetails is null)
            {
                MessageBox.Show(
                    LanguageManager.CurrentLanguage == AppLanguage.Russian
                        ? $"Р—Р°РєР°Р· в„–{_orderId} РЅРµ РЅР°Р№РґРµРЅ."
                        : $"Order #{_orderId} was not found.",
                    Title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                Close();
                return;
            }

            ApplyOrder(_orderDetails);
            SetBusyState(false, LanguageManager.CurrentLanguage == AppLanguage.Russian
                ? "Р”РµС‚Р°Р»Рё Р·Р°РєР°Р·Р° Р·Р°РіСЂСѓР¶РµРЅС‹."
                : "Order details are ready.");
        }
        catch (Exception exception)
        {
            SetBusyState(false, exception.Message);
            MessageBox.Show(exception.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    private void ApplyOrder(OrderDetailsViewModel order)
    {
        Title = $"{(LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Р”РµС‚Р°Р»Рё Р·Р°РєР°Р·Р°" : "Order Details")} #{order.OrderId}";
        OrderTitleTextBlock.Text = LanguageManager.CurrentLanguage == AppLanguage.Russian
            ? $"Р—Р°РєР°Р· в„–{order.OrderId}"
            : $"Order #{order.OrderId}";
        CustomerValueTextBlock.Text = order.ClientName;
        AddressValueTextBlock.Text = order.DeliveryAddress;
        StartDateValueTextBlock.Text = FormatDate(order.StartDate);
        EndDateValueTextBlock.Text = FormatDate(order.EndDate);
        TotalValueTextBlock.Text = order.TotalCost.ToString("N2", CultureInfo.CurrentCulture);
        FooterTotalValueTextBlock.Text = order.TotalCost.ToString("N2", CultureInfo.CurrentCulture);
        FooterPositionsTextBlock.Text = LanguageManager.CurrentLanguage == AppLanguage.Russian
            ? $"Р’СЃРµРіРѕ С‚РѕРІР°СЂРѕРІ: {order.TotalPositions}"
            : $"Total positions: {order.TotalPositions}";
        FooterQuantityTextBlock.Text = LanguageManager.CurrentLanguage == AppLanguage.Russian
            ? $"РћР±С‰РµРµ РєРѕР»РёС‡РµСЃС‚РІРѕ: {order.TotalQuantity} С€С‚."
            : $"Total quantity: {order.TotalQuantity} pcs.";
        StatusBadgeTextBlock.Text = LocalizeStatus(order.OrderStatus);
        ApplyStatusBadge(order.OrderStatus);
        OrderItemsControl.ItemsSource = order.Items;

        if (LanguageManager.CurrentLanguage == AppLanguage.Russian)
        {
            Title = $"Детали заказа #{order.OrderId}";
            OrderTitleTextBlock.Text = $"Заказ №{order.OrderId}";
            FooterPositionsTextBlock.Text = $"Всего товаров: {order.TotalPositions}";
            FooterQuantityTextBlock.Text = $"Общее количество: {order.TotalQuantity} шт.";
        }
    }

    private void ApplyStatusBadge(string status)
    {
        var isRussian = LanguageManager.CurrentLanguage == AppLanguage.Russian;
        switch (status.Trim())
        {
            case "Pending":
                StatusBadgeBorder.Background = (Brush)FindResource("PendingBgBrush");
                StatusBadgeTextBlock.Foreground = (Brush)FindResource("PendingFgBrush");
                break;
            case "Shipped":
                StatusBadgeBorder.Background = (Brush)FindResource("ShippedBgBrush");
                StatusBadgeTextBlock.Foreground = (Brush)FindResource("ShippedFgBrush");
                break;
            case "Completed":
                StatusBadgeBorder.Background = (Brush)FindResource("CompletedBgBrush");
                StatusBadgeTextBlock.Foreground = (Brush)FindResource("CompletedFgBrush");
                break;
            default:
                StatusBadgeBorder.Background = (Brush)FindResource("AccentSoftBrush");
                StatusBadgeTextBlock.Foreground = (Brush)FindResource("AccentStrongBrush");
                StatusBadgeTextBlock.Text = isRussian ? "РќРµРёР·РІРµСЃС‚РЅРѕ" : "Unknown";
                break;
        }
    }

    private void ApplyStaticLocalization()
    {
        var isRussian = LanguageManager.CurrentLanguage == AppLanguage.Russian;
        Title = isRussian ? "Р”РµС‚Р°Р»Рё Р·Р°РєР°Р·Р°" : "Order Details";
        CustomerLabelTextBlock.Text = isRussian ? "РљР»РёРµРЅС‚" : "Client";
        AddressLabelTextBlock.Text = isRussian ? "РђРґСЂРµСЃ РґРѕСЃС‚Р°РІРєРё" : "Delivery address";
        StartDateLabelTextBlock.Text = isRussian ? "Р”Р°С‚Р° РЅР°С‡Р°Р»Р°" : "Start date";
        EndDateLabelTextBlock.Text = isRussian ? "Р”Р°С‚Р° РѕРєРѕРЅС‡Р°РЅРёСЏ" : "End date";
        TotalLabelTextBlock.Text = isRussian ? "РС‚РѕРіРѕ Рє РѕРїР»Р°С‚Рµ" : "Total due";
        ItemsSectionTitleTextBlock.Text = isRussian ? "РЎРѕСЃС‚Р°РІ Р·Р°РєР°Р·Р°" : "Order items";
        FooterTotalLabelTextBlock.Text = isRussian ? "РС‚РѕРіРѕ Рє РѕРїР»Р°С‚Рµ" : "Total due";
        CurrencyTextBlock.Text = "MDL";
        ToolTip = isRussian ? "Окно деталей заказа" : "Order details";

        if (isRussian)
        {
            Title = "Детали заказа";
            CustomerLabelTextBlock.Text = "Клиент";
            AddressLabelTextBlock.Text = "Адрес доставки";
            StartDateLabelTextBlock.Text = "Дата начала";
            EndDateLabelTextBlock.Text = "Дата окончания";
            TotalLabelTextBlock.Text = "Итого к оплате";
            ItemsSectionTitleTextBlock.Text = "Состав заказа";
            FooterTotalLabelTextBlock.Text = "Итого к оплате";
        }

        if (_orderDetails is not null)
        {
            ApplyOrder(_orderDetails);
        }
    }

    private static string LocalizeStatus(string status)
    {
        if (LanguageManager.CurrentLanguage == AppLanguage.Russian)
        {
            return status.Trim() switch
            {
                "Pending" => "Ожидает",
                "Shipped" => "Отправлен",
                "Completed" => "Завершён",
                _ => status
            };
        }

        return status.Trim() switch
        {
            "Pending" => LanguageManager.CurrentLanguage == AppLanguage.Russian ? "РћР¶РёРґР°РµС‚" : "Pending",
            "Shipped" => LanguageManager.CurrentLanguage == AppLanguage.Russian ? "РћС‚РїСЂР°РІР»РµРЅ" : "Shipped",
            "Completed" => LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Р—Р°РІРµСЂС€С‘РЅ" : "Completed",
            _ => status
        };
    }

    private static string FormatDate(DateTime? value)
    {
        return value?.ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture) ?? "—";
    }

    private void SetBusyState(bool isBusy, string statusMessage)
    {
        PrintButton.IsEnabled = !isBusy && _orderDetails is not null;
        ToolTip = statusMessage;
    }

    private async void PrintButton_Click(object sender, RoutedEventArgs e)
    {
        if (_orderDetails is null)
        {
            return;
        }

        try
        {
            SetBusyState(true, LanguageManager.CurrentLanguage == AppLanguage.Russian
                ? "РџРѕРґРіРѕС‚РѕРІРєР° С‡РµРєР° Рє РїРµС‡Р°С‚Рё..."
                : "Preparing receipt for printing...");
            await OrderReceiptService.PrintAsync(_orderDetails);
            SetBusyState(false, LanguageManager.CurrentLanguage == AppLanguage.Russian
                ? "Р§РµРє РѕС‚РєСЂС‹С‚ РґР»СЏ РїРµС‡Р°С‚Рё."
                : "Receipt opened for printing.");
        }
        catch (Exception exception)
        {
            SetBusyState(false, exception.Message);
            MessageBox.Show(exception.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void LanguageManager_LanguageChanged(object? sender, EventArgs e)
    {
        ApplyStaticLocalization();
    }

    protected override void OnClosed(EventArgs e)
    {
        LanguageManager.LanguageChanged -= LanguageManager_LanguageChanged;
        base.OnClosed(e);
    }
}

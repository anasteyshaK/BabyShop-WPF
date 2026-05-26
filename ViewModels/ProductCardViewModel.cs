using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BabyShop.Localization;

namespace BabyShop.ViewModels;

public sealed class ProductCardViewModel : INotifyPropertyChanged
{
    private static readonly Brush CartDefaultBackgroundBrush = CreateBrush("#FFEC5A8D");
    private static readonly Brush CartAddedBackgroundBrush = CreateBrush("#FF32B47A");
    private static readonly Brush CartForegroundBrush = Brushes.White;

    private BitmapImage? _productImage;
    private bool _isFavorite;
    private bool _isInCart;

    public int ProductId { get; init; }
    public string ProductTitle { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public string FabricType { get; init; } = string.Empty;
    public string Color { get; init; } = string.Empty;
    public decimal FabricAmount { get; init; }
    public decimal PricePerM { get; init; }
    public string ImagePath { get; init; } = string.Empty;
    public decimal UnitPrice => PricePerM * FabricAmount;
    public string DisplayPrice => $"{UnitPrice:0.##} MDL";
    public string FabricAmountLabel => LanguageManager.CurrentLanguage == AppLanguage.Russian
        ? $"{FabricAmount:0.##} \u043c \u0442\u043a\u0430\u043d\u0438"
        : $"{FabricAmount:0.##} m fabric";
    public string AvailabilityLabel => LanguageManager.CurrentLanguage == AppLanguage.Russian ? "\u0412 \u043d\u0430\u043b\u0438\u0447\u0438\u0438" : "In stock";
    public string DisplayCategoryName => LocalizeCategoryName(CategoryName);
    public Brush CategoryBrush { get; set; } = Brushes.MistyRose;
    public Brush CategoryTextBrush { get; set; } = Brushes.DeepPink;
    public string CartButtonText => LanguageManager.CurrentLanguage == AppLanguage.Russian
        ? (_isInCart ? "\u0414\u043e\u0431\u0430\u0432\u043b\u0435\u043d\u043e" : "\u0417\u0430\u043a\u0430\u0437\u0430\u0442\u044c")
        : (_isInCart ? "Added" : "Order");
    public string CartButtonIconGlyph => _isInCart ? "\uE73E" : "\uE7BF";
    public Brush CartButtonBackground => _isInCart ? CartAddedBackgroundBrush : CartDefaultBackgroundBrush;
    public Brush CartButtonForeground => CartForegroundBrush;

    public BitmapImage? ProductImage
    {
        get => _productImage;
        set
        {
            if (Equals(_productImage, value))
            {
                return;
            }

            _productImage = value;
            OnPropertyChanged();
        }
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (_isFavorite == value)
            {
                return;
            }

            _isFavorite = value;
            OnPropertyChanged();
        }
    }

    public bool IsInCart
    {
        get => _isInCart;
        set
        {
            if (_isInCart == value)
            {
                return;
            }

            _isInCart = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CartButtonText));
            OnPropertyChanged(nameof(CartButtonIconGlyph));
            OnPropertyChanged(nameof(CartButtonBackground));
            OnPropertyChanged(nameof(CartButtonForeground));
        }
    }

    public string FavoriteMetaText
    {
        get
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(FabricType))
            {
                parts.Add(FabricType);
            }

            if (!string.IsNullOrWhiteSpace(Color))
            {
                parts.Add(Color);
            }

            return parts.Count == 0
                ? (LanguageManager.CurrentLanguage == AppLanguage.Russian ? "\u0422\u043e\u0432\u0430\u0440 \u0434\u043b\u044f \u043c\u0430\u043b\u044b\u0448\u0430" : "Baby product")
                : string.Join(" \u2022 ", parts);
        }
    }

    public void NotifyLocalizationChanged()
    {
        OnPropertyChanged(nameof(FabricAmountLabel));
        OnPropertyChanged(nameof(AvailabilityLabel));
        OnPropertyChanged(nameof(DisplayCategoryName));
        OnPropertyChanged(nameof(CartButtonText));
        OnPropertyChanged(nameof(FavoriteMetaText));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private static Brush CreateBrush(string hexColor)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hexColor)!;
        brush.Freeze();
        return brush;
    }

    private static string LocalizeCategoryName(string? rawCategoryName)
    {
        var normalized = rawCategoryName?.Trim().ToLowerInvariant() ?? string.Empty;

        return normalized switch
        {
            "\u0438\u0433\u0440\u0443\u0448\u043a\u0438" or "toys" => LanguageManager.CurrentLanguage == AppLanguage.Russian ? "\u0418\u0433\u0440\u0443\u0448\u043a\u0438" : "Toys",
            "\u043e\u0434\u0435\u0436\u0434\u0430" or "clothing" => LanguageManager.CurrentLanguage == AppLanguage.Russian ? "\u041e\u0434\u0435\u0436\u0434\u0430" : "Clothing",
            "\u0434\u043b\u044f \u0441\u043d\u0430" or "sleep" => LanguageManager.CurrentLanguage == AppLanguage.Russian ? "\u0414\u043b\u044f \u0441\u043d\u0430" : "Sleep",
            "\u043a\u043e\u0440\u043c\u043b\u0435\u043d\u0438\u0435" or "feeding" => LanguageManager.CurrentLanguage == AppLanguage.Russian ? "\u041a\u043e\u0440\u043c\u043b\u0435\u043d\u0438\u0435" : "Feeding",
            "\u0433\u0438\u0433\u0438\u0435\u043d\u0430" or "hygiene" => LanguageManager.CurrentLanguage == AppLanguage.Russian ? "\u0413\u0438\u0433\u0438\u0435\u043d\u0430" : "Hygiene",
            "\u043e\u0431\u0443\u0432\u044c" or "footwear" => LanguageManager.CurrentLanguage == AppLanguage.Russian ? "\u041e\u0431\u0443\u0432\u044c" : "Footwear",
            "\u043d\u0430\u0431\u043e\u0440\u044b" or "sets" => LanguageManager.CurrentLanguage == AppLanguage.Russian ? "\u041d\u0430\u0431\u043e\u0440\u044b" : "Sets",
            _ => string.IsNullOrWhiteSpace(rawCategoryName)
                ? (LanguageManager.CurrentLanguage == AppLanguage.Russian ? "\u0422\u043e\u0432\u0430\u0440\u044b" : "Products")
                : rawCategoryName
        };
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}


using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using BabyShop.Configuration;
using BabyShop.Infrastructure;
using BabyShop.Localization;
using BabyShop.Models;
using BabyShop.Repositories;
using BabyShop.ViewModels;

namespace BabyShop;

public partial class UserMainWindow : Window, INotifyPropertyChanged
{
    private sealed class GuideSlideDescriptor(string imagePath, string russianLabel, string englishLabel)
    {
        public string ImagePath { get; } = imagePath;
        public string RussianLabel { get; } = russianLabel;
        public string EnglishLabel { get; } = englishLabel;
    }

    public sealed class GuideDotViewModel
    {
        public GuideDotViewModel(Brush fill, double width)
        {
            Fill = fill;
            Width = width;
        }

        public Brush Fill { get; }
        public double Width { get; }
    }

    private enum PriceDragTarget
    {
        None,
        Minimum,
        Maximum
    }

    private static readonly Brush DefaultBadgeBrush = CreateBrush("#FFFFE7EE");
    private static readonly Brush DefaultBadgeTextBrush = CreateBrush("#FFEC5A8D");
    private static readonly Brush SuccessStatusBrush = CreateBrush("#FF2F9E69");
    private static readonly Brush GuideDotActiveBrush = CreateBrush("#FFEC5A8D");
    private static readonly Brush GuideDotInactiveBrush = CreateBrush("#FFF5D3DF");

    private readonly BabyShopRepository _repository;
    private readonly List<ProductCardViewModel> _allProducts = [];
    private readonly List<GuideSlideDescriptor> _guideSlides = [];
    private HashSet<int> _photoFilterProductIds = [];
    private HashSet<int> _hitFilterProductIds = [];
    private HashSet<int> _under100FilterProductIds = [];
    private HashSet<int> _muslinFilterProductIds = [];
    private readonly string _currentUsername;
    private readonly int? _currentUserId;
    private readonly bool _isGuestAccount;
    private bool _isUpdatingPriceControls;
    private decimal _priceSliderMinimum;
    private decimal _priceSliderMaximum = 1m;
    private decimal _selectedMinPrice;
    private decimal _selectedMaxPrice = 1m;
    private PriceDragTarget _priceDragTarget;
    private CheckoutCustomerDetails? _lastCheckoutCustomerDetails;
    private string _notificationBadgeText = "0";
    private string _notificationOrderTitle = "Order created";
    private string _notificationOrderBody = "We received your order and will contact you soon.";
    private string _notificationPhoneNumber = "069 906 396";

    private string _searchQuery = string.Empty;
    private string _productSummaryText = "Loading catalog...";
    private string _catalogStatusText = "Connecting products from the database.";
    private string _cartBadgeText = "0";
    private string _cartItemsCountText = "0 items";
    private string _cartTotalText = "0 MDL";
    private string _profileInitials;
    private Visibility _searchPlaceholderVisibility = Visibility.Visible;
    private Visibility _cartOverlayVisibility = Visibility.Collapsed;
    private Visibility _productDetailsOverlayVisibility = Visibility.Collapsed;
    private Visibility _guideOverlayVisibility = Visibility.Collapsed;
    private Visibility _emptyStateVisibility = Visibility.Collapsed;
    private Visibility _cartBadgeVisibility = Visibility.Collapsed;
    private Visibility _cartEmptyVisibility = Visibility.Visible;
    private Visibility _cartItemsVisibility = Visibility.Collapsed;
    private Visibility _notificationBadgeVisibility = Visibility.Collapsed;
    private Visibility _notificationsListVisibility = Visibility.Collapsed;
    private Visibility _notificationEmptyVisibility = Visibility.Visible;
    private BitmapImage? _heroBannerImage;
    private BitmapImage? _promoBannerImage;
    private BitmapImage? _currentGuideSlideImage;
    private bool _isCartOpen;
    private bool _canCheckout;
    private bool _isNotificationsOpen;
    private bool _isApplyingFavoriteSnapshot;
    private ProductCardViewModel? _selectedProductPreview;
    private string _guideOverlayTitle = "Графический гид";
    private string _guideSlideCaption = "Слайд 1 · Руководство";
    private string _guidePreviousButtonText = "Назад";
    private string _guideNextButtonText = "Далее";
    private bool _canGoGuidePrevious;
    private bool _canGoGuideNext = true;
    private int _currentGuideSlideIndex;

    public UserMainWindow(string username, int? userId = null, bool isGuestAccount = false)
    {
        InitializeComponent();
        DataContext = this;
        WindowAppearance.ApplySharedIcon(this);
        _productSummaryText = "Loading catalog...";
        _catalogStatusText = "Connecting products from the database.";
        _cartItemsCountText = "0 items";
        _cartTotalText = "0 MDL";

        var dbHelper = new DbHelper(DatabaseSettings.BuildConnectionString());
        _repository = new BabyShopRepository(dbHelper);
        _currentUsername = username;
        _currentUserId = userId > 0 ? userId : null;
        _isGuestAccount = isGuestAccount ||
                          !_currentUserId.HasValue ||
                          string.Equals(username, "Guest", StringComparison.OrdinalIgnoreCase);
        _lastCheckoutCustomerDetails = UserCheckoutProfileStore.Load(username);

        _profileInitials = string.IsNullOrWhiteSpace(username)
            ? "G"
            : string.Concat(username.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Take(2)
                .Select(part => char.ToUpperInvariant(part[0])));

        HeroBannerImage = LoadBitmap(StorefrontAssetResolver.GetHeroBannerPath());
        PromoBannerImage = LoadBitmap(StorefrontAssetResolver.GetPromoBannerPath());
        InitializeGuideSlides();
        ApplyLocalization();
    }

    public ObservableCollection<ProductCardViewModel> VisibleProducts { get; } = [];
    public ObservableCollection<ProductCardViewModel> PrimaryVisibleProducts { get; } = [];
    public ObservableCollection<ProductCardViewModel> SecondaryVisibleProducts { get; } = [];
    public ObservableCollection<CategoryFilterViewModel> Categories { get; } = [];
    public ObservableCollection<CartItemViewModel> CartItems { get; } = [];
    public ObservableCollection<GuideDotViewModel> GuideDots { get; } = [];

    public ProductCardViewModel? SelectedProductPreview
    {
        get => _selectedProductPreview;
        private set
        {
            if (ReferenceEquals(_selectedProductPreview, value))
            {
                return;
            }

            _selectedProductPreview = value;
            OnPropertyChanged();
        }
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (_searchQuery == value)
            {
                return;
            }

            _searchQuery = value;
            SearchPlaceholderVisibility = string.IsNullOrWhiteSpace(value) ? Visibility.Visible : Visibility.Collapsed;
            OnPropertyChanged();
            ApplyFilters();
        }
    }

    public string ProductSummaryText
    {
        get => _productSummaryText;
        private set
        {
            if (_productSummaryText == value)
            {
                return;
            }

            _productSummaryText = value;
            OnPropertyChanged();
        }
    }

    public string CatalogStatusText
    {
        get => _catalogStatusText;
        private set
        {
            if (_catalogStatusText == value)
            {
                return;
            }

            _catalogStatusText = value;
            OnPropertyChanged();
        }
    }

    public string CartBadgeText
    {
        get => _cartBadgeText;
        private set
        {
            if (_cartBadgeText == value)
            {
                return;
            }

            _cartBadgeText = value;
            OnPropertyChanged();
        }
    }

    public string CartItemsCountText
    {
        get => _cartItemsCountText;
        private set
        {
            if (_cartItemsCountText == value)
            {
                return;
            }

            _cartItemsCountText = value;
            OnPropertyChanged();
        }
    }

    public string CartTotalText
    {
        get => _cartTotalText;
        private set
        {
            if (_cartTotalText == value)
            {
                return;
            }

            _cartTotalText = value;
            OnPropertyChanged();
        }
    }

    public Visibility ProductDetailsOverlayVisibility
    {
        get => _productDetailsOverlayVisibility;
        private set
        {
            if (_productDetailsOverlayVisibility == value)
            {
                return;
            }

            _productDetailsOverlayVisibility = value;
            OnPropertyChanged();
        }
    }

    public Visibility GuideOverlayVisibility
    {
        get => _guideOverlayVisibility;
        private set
        {
            if (_guideOverlayVisibility == value)
            {
                return;
            }

            _guideOverlayVisibility = value;
            OnPropertyChanged();
        }
    }

    public BitmapImage? CurrentGuideSlideImage
    {
        get => _currentGuideSlideImage;
        private set
        {
            if (ReferenceEquals(_currentGuideSlideImage, value))
            {
                return;
            }

            _currentGuideSlideImage = value;
            OnPropertyChanged();
        }
    }

    public string GuideOverlayTitle
    {
        get => _guideOverlayTitle;
        private set
        {
            if (_guideOverlayTitle == value)
            {
                return;
            }

            _guideOverlayTitle = value;
            OnPropertyChanged();
        }
    }

    public string GuideSlideCaption
    {
        get => _guideSlideCaption;
        private set
        {
            if (_guideSlideCaption == value)
            {
                return;
            }

            _guideSlideCaption = value;
            OnPropertyChanged();
        }
    }

    public string GuidePreviousButtonText
    {
        get => _guidePreviousButtonText;
        private set
        {
            if (_guidePreviousButtonText == value)
            {
                return;
            }

            _guidePreviousButtonText = value;
            OnPropertyChanged();
        }
    }

    public string GuideNextButtonText
    {
        get => _guideNextButtonText;
        private set
        {
            if (_guideNextButtonText == value)
            {
                return;
            }

            _guideNextButtonText = value;
            OnPropertyChanged();
        }
    }

    public bool CanGoGuidePrevious
    {
        get => _canGoGuidePrevious;
        private set
        {
            if (_canGoGuidePrevious == value)
            {
                return;
            }

            _canGoGuidePrevious = value;
            OnPropertyChanged();
        }
    }

    public bool CanGoGuideNext
    {
        get => _canGoGuideNext;
        private set
        {
            if (_canGoGuideNext == value)
            {
                return;
            }

            _canGoGuideNext = value;
            OnPropertyChanged();
        }
    }

    public string NotificationBadgeText
    {
        get => _notificationBadgeText;
        private set
        {
            if (_notificationBadgeText == value)
            {
                return;
            }

            _notificationBadgeText = value;
            OnPropertyChanged();
        }
    }

    public string NotificationOrderTitle
    {
        get => _notificationOrderTitle;
        private set
        {
            if (_notificationOrderTitle == value)
            {
                return;
            }

            _notificationOrderTitle = value;
            OnPropertyChanged();
        }
    }

    public string NotificationOrderBody
    {
        get => _notificationOrderBody;
        private set
        {
            if (_notificationOrderBody == value)
            {
                return;
            }

            _notificationOrderBody = value;
            OnPropertyChanged();
        }
    }

    public string NotificationPhoneNumber
    {
        get => _notificationPhoneNumber;
        private set
        {
            if (_notificationPhoneNumber == value)
            {
                return;
            }

            _notificationPhoneNumber = value;
            OnPropertyChanged();
        }
    }

    public string ProfileInitials
    {
        get => _profileInitials;
        private set
        {
            if (_profileInitials == value)
            {
                return;
            }

            _profileInitials = value;
            OnPropertyChanged();
        }
    }

    public Visibility SearchPlaceholderVisibility
    {
        get => _searchPlaceholderVisibility;
        private set
        {
            if (_searchPlaceholderVisibility == value)
            {
                return;
            }

            _searchPlaceholderVisibility = value;
            OnPropertyChanged();
        }
    }

    public Visibility CartOverlayVisibility
    {
        get => _cartOverlayVisibility;
        private set
        {
            if (_cartOverlayVisibility == value)
            {
                return;
            }

            _cartOverlayVisibility = value;
            OnPropertyChanged();
        }
    }

    public Visibility EmptyStateVisibility
    {
        get => _emptyStateVisibility;
        private set
        {
            if (_emptyStateVisibility == value)
            {
                return;
            }

            _emptyStateVisibility = value;
            OnPropertyChanged();
        }
    }

    public Visibility CartBadgeVisibility
    {
        get => _cartBadgeVisibility;
        private set
        {
            if (_cartBadgeVisibility == value)
            {
                return;
            }

            _cartBadgeVisibility = value;
            OnPropertyChanged();
        }
    }

    public Visibility CartEmptyVisibility
    {
        get => _cartEmptyVisibility;
        private set
        {
            if (_cartEmptyVisibility == value)
            {
                return;
            }

            _cartEmptyVisibility = value;
            OnPropertyChanged();
        }
    }

    public Visibility CartItemsVisibility
    {
        get => _cartItemsVisibility;
        private set
        {
            if (_cartItemsVisibility == value)
            {
                return;
            }

            _cartItemsVisibility = value;
            OnPropertyChanged();
        }
    }

    public Visibility NotificationBadgeVisibility
    {
        get => _notificationBadgeVisibility;
        private set
        {
            if (_notificationBadgeVisibility == value)
            {
                return;
            }

            _notificationBadgeVisibility = value;
            OnPropertyChanged();
        }
    }

    public Visibility NotificationsListVisibility
    {
        get => _notificationsListVisibility;
        private set
        {
            if (_notificationsListVisibility == value)
            {
                return;
            }

            _notificationsListVisibility = value;
            OnPropertyChanged();
        }
    }

    public Visibility NotificationEmptyVisibility
    {
        get => _notificationEmptyVisibility;
        private set
        {
            if (_notificationEmptyVisibility == value)
            {
                return;
            }

            _notificationEmptyVisibility = value;
            OnPropertyChanged();
        }
    }

    public BitmapImage? HeroBannerImage
    {
        get => _heroBannerImage;
        private set
        {
            if (Equals(_heroBannerImage, value))
            {
                return;
            }

            _heroBannerImage = value;
            OnPropertyChanged();
        }
    }

    public BitmapImage? PromoBannerImage
    {
        get => _promoBannerImage;
        private set
        {
            if (Equals(_promoBannerImage, value))
            {
                return;
            }

            _promoBannerImage = value;
            OnPropertyChanged();
        }
    }

    public bool IsCartOpen
    {
        get => _isCartOpen;
        private set
        {
            if (_isCartOpen == value)
            {
                return;
            }

            _isCartOpen = value;
            CartOverlayVisibility = value ? Visibility.Visible : Visibility.Collapsed;
            OnPropertyChanged();
        }
    }

    public bool CanCheckout
    {
        get => _canCheckout;
        private set
        {
            if (_canCheckout == value)
            {
                return;
            }

            _canCheckout = value;
            OnPropertyChanged();
        }
    }

    public bool IsNotificationsOpen
    {
        get => _isNotificationsOpen;
        private set
        {
            if (_isNotificationsOpen == value)
            {
                return;
            }

            _isNotificationsOpen = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private static string T(string russian, string english)
    {
        return LanguageManager.CurrentLanguage == AppLanguage.Russian ? russian : english;
    }

    private void InitializeGuideSlides()
    {
        _guideSlides.Clear();
        _guideSlides.Add(new GuideSlideDescriptor(
            StorefrontAssetResolver.GetGuideCoverPath(),
            "Пользовательское руководство",
            "User guide"));
        _guideSlides.Add(new GuideSlideDescriptor(
            StorefrontAssetResolver.GetGuideLoginPath(),
            "Вход в приложение",
            "App sign in"));
        _guideSlides.Add(new GuideSlideDescriptor(
            StorefrontAssetResolver.GetGuideHomePath(),
            "Главная страница",
            "Home page"));
        _guideSlides.Add(new GuideSlideDescriptor(
            StorefrontAssetResolver.GetGuideFavoritesPath(),
            "Избранное",
            "Favorites"));
        _guideSlides.Add(new GuideSlideDescriptor(
            StorefrontAssetResolver.GetGuideOrdersPath(),
            "Мои заказы",
            "My orders"));
        _guideSlides.Add(new GuideSlideDescriptor(
            StorefrontAssetResolver.GetGuideCartPath(),
            "Корзина и оформление",
            "Cart and checkout"));
        _guideSlides.Add(new GuideSlideDescriptor(
            StorefrontAssetResolver.GetGuideFiltersPath(),
            "Фильтры, сортировка и поиск",
            "Filters, sorting, and search"));

        ShowGuideSlide(0);
    }

    private void ShowGuideSlide(int index)
    {
        if (_guideSlides.Count == 0)
        {
            return;
        }

        _currentGuideSlideIndex = Math.Clamp(index, 0, _guideSlides.Count - 1);
        CurrentGuideSlideImage = LoadBitmap(_guideSlides[_currentGuideSlideIndex].ImagePath);
        RefreshGuideSlideState();
    }

    private void RefreshGuideSlideState()
    {
        if (_guideSlides.Count == 0)
        {
            GuideSlideCaption = string.Empty;
            CanGoGuidePrevious = false;
            CanGoGuideNext = false;
            GuideDots.Clear();
            return;
        }

        var activeSlide = _guideSlides[_currentGuideSlideIndex];
        var slideLabel = LanguageManager.CurrentLanguage == AppLanguage.Russian
            ? activeSlide.RussianLabel
            : activeSlide.EnglishLabel;

        GuideSlideCaption = T(
            $"Слайд {_currentGuideSlideIndex + 1} · {slideLabel}",
            $"Slide {_currentGuideSlideIndex + 1} · {slideLabel}");

        CanGoGuidePrevious = _currentGuideSlideIndex > 0;
        CanGoGuideNext = _currentGuideSlideIndex < _guideSlides.Count - 1;

        GuideDots.Clear();
        for (var index = 0; index < _guideSlides.Count; index++)
        {
            var isActive = index == _currentGuideSlideIndex;
            GuideDots.Add(new GuideDotViewModel(
                isActive ? GuideDotActiveBrush : GuideDotInactiveBrush,
                isActive ? 28 : 10));
        }
    }

    private void OpenGuideOverlay()
    {
        GuideOverlayVisibility = Visibility.Visible;
        UpdateOverlayBlurState();
    }

    private void CloseGuideOverlay()
    {
        GuideOverlayVisibility = Visibility.Collapsed;
        UpdateOverlayBlurState();
    }

    private void UpdateOverlayBlurState()
    {
        if (RootScrollViewer is null)
        {
            return;
        }

        var shouldBlur = GuideOverlayVisibility == Visibility.Visible ||
                         ProductDetailsOverlayVisibility == Visibility.Visible;

        RootScrollViewer.Effect = shouldBlur
            ? new BlurEffect
            {
                Radius = 16,
                KernelType = KernelType.Gaussian
            }
            : null;
    }

private static string LocalizeCategoryName(string? categoryName)
{
    var normalized = categoryName?.Trim().ToLowerInvariant() ?? string.Empty;

    return normalized switch
    {
        "\u0432\u0441\u0435 \u0442\u043e\u0432\u0430\u0440\u044b" or "all products" or "all items" => T("\u0412\u0441\u0435 \u0442\u043e\u0432\u0430\u0440\u044b", "All products"),
        "\u0438\u0433\u0440\u0443\u0448\u043a\u0438" or "toys" => T("\u0418\u0433\u0440\u0443\u0448\u043a\u0438", "Toys"),
        "\u043e\u0434\u0435\u0436\u0434\u0430" or "clothing" => T("\u041e\u0434\u0435\u0436\u0434\u0430", "Clothing"),
        "\u0434\u043b\u044f \u0441\u043d\u0430" or "sleep" => T("\u0414\u043b\u044f \u0441\u043d\u0430", "Sleep"),
        "\u043a\u043e\u0440\u043c\u043b\u0435\u043d\u0438\u0435" or "feeding" => T("\u041a\u043e\u0440\u043c\u043b\u0435\u043d\u0438\u0435", "Feeding"),
        "\u0433\u0438\u0433\u0438\u0435\u043d\u0430" or "hygiene" => T("\u0413\u0438\u0433\u0438\u0435\u043d\u0430", "Hygiene"),
        "\u043e\u0431\u0443\u0432\u044c" or "footwear" => T("\u041e\u0431\u0443\u0432\u044c", "Footwear"),
        "\u043d\u0430\u0431\u043e\u0440\u044b" or "sets" => T("\u041d\u0430\u0431\u043e\u0440\u044b", "Sets"),
        _ => string.IsNullOrWhiteSpace(categoryName) ? T("\u0411\u0435\u0437 \u043a\u0430\u0442\u0435\u0433\u043e\u0440\u0438\u0438", "Uncategorized") : categoryName
    };
}

private static string FormatItemsCount(int count)
{
    return LanguageManager.CurrentLanguage == AppLanguage.Russian
        ? $"{count} \u0442\u043e\u0432\u0430\u0440(\u043e\u0432)"
        : $"{count} item(s)";
}

private static string GetNotificationStatusText(string? orderStatus)
{
    if (string.IsNullOrWhiteSpace(orderStatus))
    {
        return T("\u0432 \u043e\u0431\u0440\u0430\u0431\u043e\u0442\u043a\u0435", "is being processed");
    }

    return orderStatus.Trim().ToLowerInvariant() switch
    {
        "pending" => T("\u043e\u0436\u0438\u0434\u0430\u0435\u0442 \u043f\u043e\u0434\u0442\u0432\u0435\u0440\u0436\u0434\u0435\u043d\u0438\u044f", "is waiting for confirmation"),
        "completed" => T("\u0443\u0436\u0435 \u043f\u043e\u0434\u0442\u0432\u0435\u0440\u0436\u0434\u0451\u043d", "is already confirmed"),
        "shipped" => T("\u043f\u0435\u0440\u0435\u0434\u0430\u043d \u0432 \u0434\u043e\u0441\u0442\u0430\u0432\u043a\u0443", "is already in delivery"),
        _ => LanguageManager.CurrentLanguage == AppLanguage.Russian
            ? $"\u0438\u043c\u0435\u0435\u0442 \u0441\u0442\u0430\u0442\u0443\u0441 \"{orderStatus}\""
            : $"has status \"{orderStatus}\""
    };
}

private static string BuildNotificationTitle(int orderId)
{
    return LanguageManager.CurrentLanguage == AppLanguage.Russian
        ? $"\u0417\u0430\u043a\u0430\u0437 \u2116{orderId} \u043e\u0444\u043e\u0440\u043c\u043b\u0435\u043d"
        : $"Order #{orderId} was created";
}

private static string BuildNotificationBody(DateTime? orderDate, string? orderStatus)
{
    var statusText = GetNotificationStatusText(orderStatus);

    if (orderDate.HasValue)
    {
        return LanguageManager.CurrentLanguage == AppLanguage.Russian
            ? $"\u041c\u044b \u043f\u043e\u043b\u0443\u0447\u0438\u043b\u0438 \u0437\u0430\u043a\u0430\u0437 \u043e\u0442 {orderDate.Value:dd.MM.yyyy HH:mm}. \u0421\u0435\u0439\u0447\u0430\u0441 \u043e\u043d {statusText}."
            : $"We received the order on {orderDate.Value:dd.MM.yyyy HH:mm}. It {statusText}.";
    }

    return LanguageManager.CurrentLanguage == AppLanguage.Russian
        ? $"\u041c\u044b \u043f\u043e\u043b\u0443\u0447\u0438\u043b\u0438 \u0432\u0430\u0448 \u0437\u0430\u043a\u0430\u0437. \u0421\u0435\u0439\u0447\u0430\u0441 \u043e\u043d {statusText}."
        : $"We received your order. It {statusText}.";
}

private void ApplyLocalization()
{
    SearchPlaceholderTextBlock.Text = T("\u041f\u043e\u0438\u0441\u043a \u0442\u043e\u0432\u0430\u0440\u043e\u0432 \u0434\u043b\u044f \u043c\u0430\u043b\u044b\u0448\u0435\u0439...", "Search baby products...");
    HomeNavTextBlock.Text = T("\u0413\u043b\u0430\u0432\u043d\u0430\u044f", "Home");
    FavoritesNavTextBlock.Text = T("\u0418\u0437\u0431\u0440\u0430\u043d\u043d\u043e\u0435", "Favorites");
    MyOrdersNavTextBlock.Text = T("\u041c\u043e\u0438 \u0437\u0430\u043a\u0430\u0437\u044b", "My orders");
    CartHeaderButtonTextBlock.Text = T("\u041a\u043e\u0440\u0437\u0438\u043d\u0430", "Cart");

    NotificationPanelTitleTextBlock.Text = T("\u0411\u044b\u0441\u0442\u0440\u0430\u044f \u0441\u0432\u044f\u0437\u044c", "Quick contact");
    NotificationPanelSubtitleTextBlock.Text = T("\u041f\u043e\u0437\u0432\u043e\u043d\u0438\u0442\u0435 \u043d\u0430\u043c, \u0435\u0441\u043b\u0438 \u043d\u0443\u0436\u043d\u043e \u0431\u044b\u0441\u0442\u0440\u043e \u0443\u0442\u043e\u0447\u043d\u0438\u0442\u044c \u0437\u0430\u043a\u0430\u0437 \u0438\u043b\u0438 \u0434\u0435\u0442\u0430\u043b\u0438 \u0434\u043e\u0441\u0442\u0430\u0432\u043a\u0438.", "Call us if you need to quickly confirm order or delivery details.");
    NotificationPhoneTitleTextBlock.Text = T("\u041d\u043e\u043c\u0435\u0440 \u0434\u043b\u044f \u0431\u044b\u0441\u0442\u0440\u043e\u0439 \u0441\u0432\u044f\u0437\u0438", "Quick contact number");
    NotificationPhoneSubtitleTextBlock.Text = T("\u041f\u043e\u0437\u0432\u043e\u043d\u0438\u0442\u0435 \u043d\u0430\u043c, \u0438 \u043c\u044b \u0431\u044b\u0441\u0442\u0440\u043e \u0443\u0442\u043e\u0447\u043d\u0438\u043c \u0437\u0430\u043a\u0430\u0437.", "Call us and we will quickly clarify your order.");
    NotificationPickupTitleTextBlock.Text = T("\u0410\u0434\u0440\u0435\u0441 \u0441\u0430\u043c\u043e\u0432\u044b\u0432\u043e\u0437\u0430", "Pickup address");
    NotificationPickupSubtitleTextBlock.Text = T("\u0417\u0430\u0431\u0440\u0430\u0442\u044c \u0437\u0430\u043a\u0430\u0437 \u043c\u043e\u0436\u043d\u043e \u043f\u043e \u043d\u0430\u0448\u0435\u043c\u0443 \u0430\u0434\u0440\u0435\u0441\u0443.", "You can pick up the order at our address.");
    NotificationEmptyTitleTextBlock.Text = T("\u041f\u043e\u043a\u0430 \u0431\u0435\u0437 \u043d\u043e\u0432\u044b\u0445 \u043e\u0431\u043d\u043e\u0432\u043b\u0435\u043d\u0438\u0439", "No new updates yet");
    NotificationEmptySubtitleTextBlock.Text = T("\u041f\u043e\u0441\u043b\u0435 \u043e\u0444\u043e\u0440\u043c\u043b\u0435\u043d\u0438\u044f \u0437\u0430\u043a\u0430\u0437\u0430 \u0437\u0434\u0435\u0441\u044c \u043f\u043e\u044f\u0432\u0438\u0442\u0441\u044f \u0435\u0433\u043e \u0441\u0442\u0430\u0442\u0443\u0441, \u0430 \u043d\u043e\u043c\u0435\u0440 \u0434\u043b\u044f \u0441\u0432\u044f\u0437\u0438 \u0434\u043e\u0441\u0442\u0443\u043f\u0435\u043d \u0443\u0436\u0435 \u0441\u0435\u0439\u0447\u0430\u0441.", "After checkout, the order status will appear here, and the contact number is already available.");

    HeroTitleTextBlock.Text = T("\u0412\u0441\u0451 \u0434\u043b\u044f \u043d\u0435\u0436\u043d\u043e\u0433\u043e \u0434\u0435\u0442\u0441\u0442\u0432\u0430", "Everything for a gentle childhood");
    HeroSubtitleTextBlock.Text = T("\u0412\u044b\u0431\u0438\u0440\u0430\u0439\u0442\u0435 \u043b\u0443\u0447\u0448\u0435\u0435 \u0434\u043b\u044f \u0441\u0430\u043c\u043e\u0433\u043e \u0432\u0430\u0436\u043d\u043e\u0433\u043e \u0441 \u043b\u044e\u0431\u043e\u0432\u044c\u044e \u0438 \u0437\u0430\u0431\u043e\u0442\u043e\u0439", "Choose the best for what matters most with love and care");
    HeroButtonTextBlock.Text = T("\u0421\u043c\u043e\u0442\u0440\u0435\u0442\u044c \u043a\u0430\u0442\u0430\u043b\u043e\u0433", "Browse catalog");
    HeroFeatureSafeTitleTextBlock.Text = T("100% \u0431\u0435\u0437\u043e\u043f\u0430\u0441\u043d\u043e\u0441\u0442\u044c", "100% safety");
    HeroFeatureSafeSubtitleTextBlock.Text = T("\u041f\u0440\u043e\u0432\u0435\u0440\u0435\u043d\u043d\u044b\u0435 \u0442\u043e\u0432\u0430\u0440\u044b", "Verified products");
    HeroFeatureDeliveryTitleTextBlock.Text = T("\u0411\u044b\u0441\u0442\u0440\u0430\u044f \u0434\u043e\u0441\u0442\u0430\u0432\u043a\u0430", "Fast delivery");
    HeroFeatureDeliverySubtitleTextBlock.Text = T("1-2 \u0434\u043d\u044f \u043f\u043e \u0432\u0441\u0435\u0439 \u0441\u0442\u0440\u0430\u043d\u0435", "1-2 days nationwide");
    HeroFeatureSupportTitleTextBlock.Text = T("\u041f\u043e\u0434\u0434\u0435\u0440\u0436\u043a\u0430 24/7", "24/7 support");
    HeroFeatureSupportSubtitleTextBlock.Text = T("\u041c\u044b \u0432\u0441\u0435\u0433\u0434\u0430 \u0440\u044f\u0434\u043e\u043c", "We are always here");
    GuideOverlayTitle = T("\u0413\u0440\u0430\u0444\u0438\u0447\u0435\u0441\u043a\u0438\u0439 \u0433\u0438\u0434", "Visual guide");
    GuidePreviousButtonText = T("\u041d\u0430\u0437\u0430\u0434", "Back");
    GuideNextButtonText = T("\u0414\u0430\u043b\u0435\u0435", "Next");
    RefreshGuideSlideState();

    ProductsSectionTitleTextBlock.Text = T("\u0412\u0441\u0435 \u0442\u043e\u0432\u0430\u0440\u044b", "All products");
    SortLabelTextBlock.Text = T("\u0421\u043e\u0440\u0442\u0438\u0440\u043e\u0432\u043a\u0430:", "Sort by:");
    SortPopularItem.Content = T("\u0421\u043d\u0430\u0447\u0430\u043b\u0430 \u043d\u043e\u0432\u044b\u0435", "Newest first");
    SortPriceAscItem.Content = T("\u0426\u0435\u043d\u0430: \u0441\u043d\u0430\u0447\u0430\u043b\u0430 \u0434\u0435\u0448\u0435\u0432\u043b\u0435", "Price: low to high");
    SortPriceDescItem.Content = T("\u0426\u0435\u043d\u0430: \u0441\u043d\u0430\u0447\u0430\u043b\u0430 \u0434\u043e\u0440\u043e\u0436\u0435", "Price: high to low");
    SortTitleItem.Content = T("\u041f\u043e \u043d\u0430\u0437\u0432\u0430\u043d\u0438\u044e", "By name");
    EmptyCatalogTitleTextBlock.Text = T("\u041d\u0438\u0447\u0435\u0433\u043e \u043d\u0435 \u043d\u0430\u0439\u0434\u0435\u043d\u043e", "Nothing found");
    CategoriesTitleTextBlock.Text = T("\u041a\u0430\u0442\u0435\u0433\u043e\u0440\u0438\u0438", "Categories");
    QuickFiltersTitleTextBlock.Text = T("\u0411\u044b\u0441\u0442\u0440\u044b\u0435 \u0444\u0438\u043b\u044c\u0442\u0440\u044b", "Quick filters");
    PhotoFilterCheckBox.Content = T("\u0421 \u0444\u043e\u0442\u043e", "With photo");
    HitsFilterCheckBox.Content = T("\u0425\u0438\u0442\u044b", "Hits");
    Under100FilterCheckBox.Content = T("\u0414\u043e 100 MDL", "Up to 100 MDL");
    MuslinFilterCheckBox.Content = T("\u041c\u0443\u0441\u043b\u0438\u043d", "Muslin");
    PriceLabelTextBlock.Text = T("\u0426\u0435\u043d\u0430", "Price");
    PromoTitleTextBlock.Text = T("\u041c\u0443\u0441\u043b\u0438\u043d", "Muslin");
    PromoDiscountTextBlock.Text = T("\u041d\u0435\u0436\u043d\u044b\u0439 \u043c\u0443\u0441\u043b\u0438\u043d", "Gentle muslin");
    PromoSubtitleTextBlock.Text = T("\u041c\u044f\u0433\u043a\u0438\u0435 \u0442\u043e\u0432\u0430\u0440\u044b \u0434\u043b\u044f \u043c\u0430\u043b\u044b\u0448\u0435\u0439 \u043d\u0430 \u043a\u0430\u0436\u0434\u044b\u0439 \u0434\u0435\u043d\u044c", "Soft essentials for little ones every day");
    PromoButtonTextBlock.Text = T("\u0421\u043c\u043e\u0442\u0440\u0435\u0442\u044c \u0442\u043e\u0432\u0430\u0440\u044b", "View products");

    CartOverlayTitleTextBlock.Text = T("\u041a\u043e\u0440\u0437\u0438\u043d\u0430", "Cart");
    CartEmptyTitleTextBlock.Text = T("\u041a\u043e\u0440\u0437\u0438\u043d\u0430 \u043f\u043e\u043a\u0430 \u043f\u0443\u0441\u0442\u0430", "Your cart is empty");
    CartEmptySubtitleTextBlock.Text = T("\u0414\u043e\u0431\u0430\u0432\u044c\u0442\u0435 \u0442\u043e\u0432\u0430\u0440\u044b \u0438\u0437 \u043a\u0430\u0442\u0430\u043b\u043e\u0433\u0430, \u0438 \u0437\u0434\u0435\u0441\u044c \u043f\u043e\u044f\u0432\u044f\u0442\u0441\u044f \u043a\u043e\u043b\u0438\u0447\u0435\u0441\u0442\u0432\u043e, \u0446\u0435\u043d\u0430 \u0438 \u0438\u0442\u043e\u0433\u043e\u0432\u0430\u044f \u0441\u0443\u043c\u043c\u0430.", "Add products from the catalog and quantity, price, and total will appear here.");
    CheckoutPanelTitleTextBlock.Text = T("\u041e\u0444\u043e\u0440\u043c\u043b\u0435\u043d\u0438\u0435", "Checkout");
    CartTotalLabelTextBlock.Text = T("\u0418\u0442\u043e\u0433\u043e\u0432\u0430\u044f \u0441\u0443\u043c\u043c\u0430", "Total amount");
    CheckoutOrderButtonTextBlock.Text = T("\u041e\u0444\u043e\u0440\u043c\u0438\u0442\u044c \u0437\u0430\u043a\u0430\u0437", "Place order");
    ProductDetailsIntroTextBlock.Text = T("\u041d\u0435\u0436\u043d\u044b\u0439 \u0442\u043e\u0432\u0430\u0440 \u0434\u043b\u044f \u043c\u0430\u043b\u044b\u0448\u0430 \u0441 \u043c\u044f\u0433\u043a\u043e\u0439 \u0442\u043a\u0430\u043d\u044c\u044e, \u043f\u0440\u0438\u044f\u0442\u043d\u043e\u0439 \u0444\u0430\u043a\u0442\u0443\u0440\u043e\u0439 \u0438 \u0430\u043a\u043a\u0443\u0440\u0430\u0442\u043d\u043e\u0439 \u043e\u0442\u0434\u0435\u043b\u043a\u043e\u0439.", "A gentle baby essential with soft fabric, pleasant texture, and a neat finish.");
    ProductDetailsMaterialLabelTextBlock.Text = T("\u041c\u0430\u0442\u0435\u0440\u0438\u0430\u043b", "Material");
    ProductDetailsColorLabelTextBlock.Text = T("\u0426\u0432\u0435\u0442", "Color");
    ProductDetailsAmountLabelTextBlock.Text = T("\u0420\u0430\u0441\u0445\u043e\u0434 \u0442\u043a\u0430\u043d\u0438", "Fabric amount");
    ProductDetailsPricePerMeterLabelTextBlock.Text = T("\u0426\u0435\u043d\u0430 \u0437\u0430 \u043c\u0435\u0442\u0440", "Price per meter");
    ProductDetailsTotalLabelTextBlock.Text = T("\u0418\u0442\u043e\u0433\u043e\u0432\u0430\u044f \u0441\u0442\u043e\u0438\u043c\u043e\u0441\u0442\u044c", "Total price");
    ProductDetailsCloseButtonTextBlock.Text = T("\u0417\u0430\u043a\u0440\u044b\u0442\u044c", "Close");

    RussianLanguageButton.Opacity = LanguageManager.CurrentLanguage == AppLanguage.Russian ? 1 : 0.55;
    EnglishLanguageButton.Opacity = LanguageManager.CurrentLanguage == AppLanguage.English ? 1 : 0.55;

    foreach (var product in _allProducts)
    {
        product.NotifyLocalizationChanged();
    }
}

    private async Task ChangeLanguageAsync(AppLanguage language)
    {
        LanguageManager.SetLanguage(language);
        ApplyLocalization();
        await LoadCatalogAsync();
        RefreshCartState();
        await RefreshNotificationsAsync();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        FitWindowToWorkArea();
        UpdateViewportLayout();
        if (NotificationsPopup is not null && NotificationsButton is not null)
        {
            NotificationsPopup.PlacementTarget = NotificationsButton;
            NotificationsPopup.DataContext = this;
        }
        ApplyLocalization();
        await LoadCatalogAsync();
        await RefreshNotificationsAsync();
    }

    private void FitWindowToWorkArea()
    {
        var workArea = SystemParameters.WorkArea;
        var horizontalMargin = 28d;
        var verticalMargin = 28d;

        var targetWidth = Math.Min(Width, Math.Max(MinWidth, workArea.Width - horizontalMargin * 2));
        var targetHeight = Math.Min(Height, Math.Max(MinHeight, workArea.Height - verticalMargin * 2));

        Width = targetWidth;
        Height = targetHeight;
        Left = workArea.Left + Math.Max(horizontalMargin, (workArea.Width - targetWidth) / 2);
        Top = workArea.Top + Math.Max(verticalMargin, (workArea.Height - targetHeight) / 2);
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateViewportLayout();
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        UpdateViewportLayout();
    }

    private void RootScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateViewportLayout();
    }

    private void UpdateViewportLayout()
    {
        if (!IsLoaded || ViewportContentGrid is null || RootScrollViewer is null || MainContentGrid is null)
        {
            return;
        }

        var viewportWidth = RootScrollViewer.ViewportWidth;
        if (double.IsNaN(viewportWidth) || viewportWidth <= 0)
        {
            viewportWidth = RootScrollViewer.ActualWidth;
        }

        var isEdgeToEdge = WindowState == WindowState.Maximized ||
                           ActualWidth >= SystemParameters.WorkArea.Width - 8;

        var outerHorizontalInset = isEdgeToEdge ? 0d : 48d;
        var targetWidth = Math.Max(MinWidth - outerHorizontalInset, viewportWidth - outerHorizontalInset);

        ViewportContentGrid.Width = targetWidth;
        MainContentGrid.Margin = isEdgeToEdge
            ? new Thickness(0, 20, 0, 0)
            : new Thickness(24, 20, 24, 28);
    }

private async Task LoadCatalogAsync()
{
    try
    {
        CatalogStatusText = T("\u0417\u0430\u0433\u0440\u0443\u0436\u0430\u0435\u043c \u0442\u043e\u0432\u0430\u0440\u044b \u0438 \u043a\u0430\u0442\u0435\u0433\u043e\u0440\u0438\u0438...", "Loading products and categories...");

        var productsTask = _repository.GetProductsForCustomerCatalogAsync();
        var categoriesTask = _repository.GetCustomerCatalogCategoriesAsync();
        var favoriteIdsTask = _isGuestAccount || !_currentUserId.HasValue
            ? Task.FromResult<IReadOnlySet<int>>(new HashSet<int>())
            : _repository.GetFavoriteProductIdsAsync(_currentUserId.Value);
        var photoFilterTask = _repository.GetCatalogProductIdsWithPhotosAsync();
        var hitFilterTask = _repository.GetCatalogHitProductIdsAsync();
        var under100FilterTask = _repository.GetCatalogAffordableProductIdsAsync(100m);
        var muslinFilterTask = _repository.GetCatalogMuslinProductIdsAsync();

        await Task.WhenAll(productsTask, categoriesTask, favoriteIdsTask, photoFilterTask, hitFilterTask, under100FilterTask, muslinFilterTask);

        _photoFilterProductIds = photoFilterTask.Result.ToHashSet();
        _hitFilterProductIds = hitFilterTask.Result.ToHashSet();
        _under100FilterProductIds = under100FilterTask.Result.ToHashSet();
        _muslinFilterProductIds = muslinFilterTask.Result.ToHashSet();

        foreach (var product in _allProducts)
        {
            product.PropertyChanged -= ProductCard_PropertyChanged;
        }

        _allProducts.Clear();
        _allProducts.AddRange(productsTask.Result.Select(DecorateProductCard));

        ApplyFavoriteSnapshot(favoriteIdsTask.Result);

        foreach (var product in _allProducts)
        {
            product.PropertyChanged += ProductCard_PropertyChanged;
        }

        Categories.Clear();
        Categories.Add(new CategoryFilterViewModel
        {
            CategoryId = null,
            CategoryName = LocalizeCategoryName("\u0412\u0441\u0435 \u0442\u043e\u0432\u0430\u0440\u044b"),
            ProductCount = _allProducts.Count,
            IconSource = BuildCategoryIconSource("\u0412\u0441\u0435 \u0442\u043e\u0432\u0430\u0440\u044b"),
            IsSelected = true
        });

        foreach (var category in categoriesTask.Result)
        {
            Categories.Add(new CategoryFilterViewModel
            {
                CategoryId = category.CategoryId,
                CategoryName = LocalizeCategoryName(category.CategoryName),
                ProductCount = category.ProductCount,
                IconSource = BuildCategoryIconSource(category.CategoryName),
                IsSelected = false
            });
        }

        ConfigurePriceSlider(
            _allProducts.Count == 0 ? 0m : _allProducts.Min(product => product.UnitPrice),
            _allProducts.Count == 0 ? 100m : _allProducts.Max(product => product.UnitPrice));

        CatalogStatusText = T("\u0422\u043e\u0432\u0430\u0440\u044b \u0437\u0430\u0433\u0440\u0443\u0436\u0435\u043d\u044b \u0438\u0437 MySQL.", "Products loaded from MySQL.");
        ApplyFilters();
    }
    catch (Exception exception)
    {
        VisibleProducts.Clear();
        EmptyStateVisibility = Visibility.Visible;
        ProductSummaryText = T("\u0412\u0441\u0435 \u0442\u043e\u0432\u0430\u0440\u044b", "All products");
        CatalogStatusText = exception.Message;
    }
}

private ProductCardViewModel DecorateProductCard(ProductCardViewModel product)
    {
        var resolvedProductImagePath = StorefrontAssetResolver.ResolveProductImagePath(product.ImagePath);
        product.ProductImage = TryLoadBitmap(resolvedProductImagePath)
            ?? TryLoadBitmap(StorefrontAssetResolver.GetDefaultProductImagePath());

        (product.CategoryBrush, product.CategoryTextBrush) = GetCategoryPalette(product.CategoryName);
        return product;
    }

private void ApplyFavoriteSnapshot(IReadOnlySet<int> favoriteIds)
{
    _isApplyingFavoriteSnapshot = true;

    try
    {
        foreach (var product in _allProducts)
        {
            product.IsFavorite = favoriteIds.Contains(product.ProductId);
        }
    }
    finally
    {
        _isApplyingFavoriteSnapshot = false;
    }
}

private async void ProductCard_PropertyChanged(object? sender, PropertyChangedEventArgs e)
{
    if (_isApplyingFavoriteSnapshot ||
        _isGuestAccount ||
        !_currentUserId.HasValue ||
        e.PropertyName != nameof(ProductCardViewModel.IsFavorite) ||
        sender is not ProductCardViewModel product)
    {
        return;
    }

    try
    {
        await _repository.SetFavoriteProductStateAsync(_currentUserId.Value, product.ProductId, product.IsFavorite);
    }
    catch (Exception exception)
    {
        MessageBox.Show(exception.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);

        _isApplyingFavoriteSnapshot = true;
        try
        {
            product.IsFavorite = !product.IsFavorite;
        }
        finally
        {
            _isApplyingFavoriteSnapshot = false;
        }
    }
}

private void ApplyFilters()
{
    if (!IsLoaded)
    {
        return;
    }

    var selectedCategoryName = Categories.FirstOrDefault(item => item.IsSelected)?.CategoryName;
    var allProductsCategoryLabel = T("\u0412\u0441\u0435 \u0442\u043e\u0432\u0430\u0440\u044b", "All products");
    var normalizedQuery = SearchQuery.Trim();
    var minPrice = ParsePrice(PriceFromTextBox?.Text);
    var maxPrice = ParsePrice(PriceToTextBox?.Text);

    IEnumerable<ProductCardViewModel> filtered = _allProducts;

    if (!string.IsNullOrWhiteSpace(normalizedQuery))
    {
        filtered = filtered.Where(product =>
            product.ProductTitle.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase));
    }

    if (!string.IsNullOrWhiteSpace(selectedCategoryName) &&
        !string.Equals(selectedCategoryName, allProductsCategoryLabel, StringComparison.OrdinalIgnoreCase))
    {
        filtered = filtered.Where(product =>
            string.Equals(LocalizeCategoryName(product.CategoryName), selectedCategoryName, StringComparison.OrdinalIgnoreCase));
    }

    if (minPrice.HasValue)
    {
        filtered = filtered.Where(product => product.UnitPrice >= minPrice.Value);
    }

    if (maxPrice.HasValue)
    {
        filtered = filtered.Where(product => product.UnitPrice <= maxPrice.Value);
    }

    if (PhotoFilterCheckBox?.IsChecked == true)
    {
        filtered = filtered.Where(product => _photoFilterProductIds.Contains(product.ProductId));
    }

    if (HitsFilterCheckBox?.IsChecked == true)
    {
        filtered = filtered.Where(product => _hitFilterProductIds.Contains(product.ProductId));
    }

    if (Under100FilterCheckBox?.IsChecked == true)
    {
        filtered = filtered.Where(product => _under100FilterProductIds.Contains(product.ProductId));
    }

    if (MuslinFilterCheckBox?.IsChecked == true)
    {
        filtered = filtered.Where(product => _muslinFilterProductIds.Contains(product.ProductId));
    }

    filtered = GetSortMode() switch
    {
        "price_asc" => filtered.OrderBy(product => product.UnitPrice).ThenBy(product => product.ProductTitle),
        "price_desc" => filtered.OrderByDescending(product => product.UnitPrice).ThenBy(product => product.ProductTitle),
        "title" => filtered.OrderBy(product => product.ProductTitle),
        _ => filtered.OrderByDescending(product => product.ProductId)
    };

    var filteredList = filtered.ToList();

    VisibleProducts.Clear();
    PrimaryVisibleProducts.Clear();
    SecondaryVisibleProducts.Clear();

    foreach (var product in filteredList)
    {
        VisibleProducts.Add(product);
    }

    foreach (var product in filteredList.Take(4))
    {
        PrimaryVisibleProducts.Add(product);
    }

    foreach (var product in filteredList.Skip(4))
    {
        SecondaryVisibleProducts.Add(product);
    }

    ProductsSectionTitleTextBlock.Text = string.IsNullOrWhiteSpace(selectedCategoryName)
        ? allProductsCategoryLabel
        : selectedCategoryName;

    ProductSummaryText = LanguageManager.CurrentLanguage == AppLanguage.Russian
        ? $"\u041d\u0430\u0439\u0434\u0435\u043d\u043e \u0442\u043e\u0432\u0430\u0440\u043e\u0432: {VisibleProducts.Count}"
        : $"Products found: {VisibleProducts.Count}";

    EmptyStateVisibility = VisibleProducts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    if (VisibleProducts.Count == 0)
    {
        CatalogStatusText = T("\u041f\u043e\u043f\u0440\u043e\u0431\u0443\u0439\u0442\u0435 \u0438\u0437\u043c\u0435\u043d\u0438\u0442\u044c \u043f\u043e\u0438\u0441\u043a \u0438\u043b\u0438 \u0444\u0438\u043b\u044c\u0442\u0440 \u043a\u0430\u0442\u0435\u0433\u043e\u0440\u0438\u0438.", "Try changing the search or category filter.");
    }
    else if (_allProducts.Count > 0)
    {
        CatalogStatusText = T("\u041a\u0430\u0442\u0430\u043b\u043e\u0433 \u043e\u0431\u043d\u043e\u0432\u043b\u0451\u043d \u0431\u0435\u0437 \u043f\u043e\u0432\u0442\u043e\u0440\u043d\u043e\u0439 \u0437\u0430\u0433\u0440\u0443\u0437\u043a\u0438 \u0438\u0437 \u0431\u0430\u0437\u044b.", "Catalog updated without reloading from the database.");
    }
}

private static decimal? ParsePrice(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var normalized = rawValue.Trim().Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private void ConfigurePriceSlider(decimal minimum, decimal maximum)
    {
        if (maximum <= minimum)
        {
            maximum = minimum + 1m;
        }

        _priceSliderMinimum = Math.Max(0m, decimal.Floor(minimum));
        _priceSliderMaximum = decimal.Ceiling(maximum);
        _selectedMinPrice = _priceSliderMinimum;
        _selectedMaxPrice = _priceSliderMaximum;

        _isUpdatingPriceControls = true;
        PriceFromTextBox.Text = FormatPriceValue(_selectedMinPrice);
        PriceToTextBox.Text = FormatPriceValue(_selectedMaxPrice);
        UpdatePriceRangeVisual();
        _isUpdatingPriceControls = false;
    }

    private void PriceThumb_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _priceDragTarget = ReferenceEquals(sender, MinPriceThumb)
            ? PriceDragTarget.Minimum
            : PriceDragTarget.Maximum;
        PriceRangeCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void PriceRangeCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var clickValue = PriceFromCanvasX(e.GetPosition(PriceRangeCanvas).X);
        _priceDragTarget = Math.Abs(clickValue - _selectedMinPrice) <= Math.Abs(clickValue - _selectedMaxPrice)
            ? PriceDragTarget.Minimum
            : PriceDragTarget.Maximum;

        SetSelectedPrice(_priceDragTarget, clickValue, updateTextBoxes: true);
        PriceRangeCanvas.CaptureMouse();
    }

    private void PriceRangeCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_priceDragTarget == PriceDragTarget.None || Mouse.Captured != PriceRangeCanvas)
        {
            return;
        }

        SetSelectedPrice(_priceDragTarget, PriceFromCanvasX(e.GetPosition(PriceRangeCanvas).X), updateTextBoxes: true);
    }

    private void PriceRangeCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _priceDragTarget = PriceDragTarget.None;
        PriceRangeCanvas.ReleaseMouseCapture();
    }

    private void PriceRangeCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdatePriceRangeVisual();
    }

    private void SetSelectedPrice(PriceDragTarget target, decimal value, bool updateTextBoxes)
    {
        value = ClampToPriceRange(value);

        if (target == PriceDragTarget.Minimum)
        {
            _selectedMinPrice = Math.Min(value, _selectedMaxPrice);
        }
        else if (target == PriceDragTarget.Maximum)
        {
            _selectedMaxPrice = Math.Max(value, _selectedMinPrice);
        }

        _isUpdatingPriceControls = true;
        if (updateTextBoxes)
        {
            PriceFromTextBox.Text = FormatPriceValue(_selectedMinPrice);
            PriceToTextBox.Text = FormatPriceValue(_selectedMaxPrice);
        }

        UpdatePriceRangeVisual();
        _isUpdatingPriceControls = false;
    }

    private decimal ClampToPriceRange(decimal value)
    {
        return Math.Min(_priceSliderMaximum, Math.Max(_priceSliderMinimum, value));
    }

    private void UpdatePriceRangeVisual()
    {
        if (PriceRangeCanvas is null || PriceTrackBorder is null || PriceSelectedRangeBorder is null)
        {
            return;
        }

        var canvasWidth = PriceRangeCanvas.ActualWidth;
        if (canvasWidth <= 22)
        {
            return;
        }

        const double thumbSize = 20;
        var trackLeft = thumbSize / 2;
        var trackTop = 11d;
        var trackWidth = Math.Max(1, canvasWidth - thumbSize);

        PriceTrackBorder.Width = trackWidth;
        Canvas.SetLeft(PriceTrackBorder, trackLeft);
        Canvas.SetTop(PriceTrackBorder, trackTop);

        var minX = PriceToCanvasX(_selectedMinPrice, trackLeft, trackWidth);
        var maxX = PriceToCanvasX(_selectedMaxPrice, trackLeft, trackWidth);

        PriceSelectedRangeBorder.Width = Math.Max(0, maxX - minX);
        Canvas.SetLeft(PriceSelectedRangeBorder, minX);
        Canvas.SetTop(PriceSelectedRangeBorder, trackTop);

        Canvas.SetLeft(MinPriceThumb, minX - thumbSize / 2);
        Canvas.SetLeft(MaxPriceThumb, maxX - thumbSize / 2);
    }

    private double PriceToCanvasX(decimal value, double trackLeft, double trackWidth)
    {
        var range = _priceSliderMaximum - _priceSliderMinimum;
        if (range <= 0)
        {
            return trackLeft;
        }

        var ratio = (double)((value - _priceSliderMinimum) / range);
        return trackLeft + ratio * trackWidth;
    }

    private decimal PriceFromCanvasX(double x)
    {
        const double thumbSize = 20;
        var trackLeft = thumbSize / 2;
        var trackWidth = Math.Max(1, PriceRangeCanvas.ActualWidth - thumbSize);
        var ratio = Math.Min(1d, Math.Max(0d, (x - trackLeft) / trackWidth));
        return _priceSliderMinimum + (_priceSliderMaximum - _priceSliderMinimum) * (decimal)ratio;
    }

    private static string FormatPriceValue(decimal value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private string GetSortMode()
    {
        if (SortComboBox?.SelectedItem is ComboBoxItem selectedItem &&
            selectedItem.Tag is string selectedTag &&
            !string.IsNullOrWhiteSpace(selectedTag))
        {
            return selectedTag;
        }

        return "popular";
    }

    private void CategoryFilterButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CategoryFilterViewModel selectedCategory })
        {
            return;
        }

        foreach (var category in Categories)
        {
            category.IsSelected = ReferenceEquals(category, selectedCategory);
        }

        ApplyFilters();
    }

    private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void PriceFilterTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingPriceControls)
        {
            ApplyFilters();
            return;
        }

        _isUpdatingPriceControls = true;

        if (string.IsNullOrWhiteSpace(PriceFromTextBox.Text))
        {
            _selectedMinPrice = _priceSliderMinimum;
        }
        else if (ParsePrice(PriceFromTextBox.Text) is { } minPrice)
        {
            _selectedMinPrice = ClampToPriceRange(minPrice);
        }

        if (string.IsNullOrWhiteSpace(PriceToTextBox.Text))
        {
            _selectedMaxPrice = _priceSliderMaximum;
        }
        else if (ParsePrice(PriceToTextBox.Text) is { } maxPrice)
        {
            _selectedMaxPrice = ClampToPriceRange(maxPrice);
        }

        if (_selectedMinPrice > _selectedMaxPrice)
        {
            if (ReferenceEquals(sender, PriceFromTextBox))
            {
                _selectedMaxPrice = _selectedMinPrice;
                PriceToTextBox.Text = FormatPriceValue(_selectedMaxPrice);
            }
            else
            {
                _selectedMinPrice = _selectedMaxPrice;
                PriceFromTextBox.Text = FormatPriceValue(_selectedMinPrice);
            }
        }

        UpdatePriceRangeVisual();
        _isUpdatingPriceControls = false;
        ApplyFilters();
    }

    private void QuickFilterChanged(object sender, RoutedEventArgs e)
    {
        ApplyFilters();
    }

    private void ProductCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source &&
            FindVisualAncestor<ButtonBase>(source) is not null)
        {
            return;
        }

        if (sender is FrameworkElement { DataContext: ProductCardViewModel product })
        {
            OpenProductDetails(product);
        }
    }

    private void ProductDetailsOverlay_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource == sender)
        {
            CloseProductDetails();
        }
    }

    private void CloseProductDetailsButton_Click(object sender, RoutedEventArgs e)
    {
        CloseProductDetails();
    }

    private void AddSelectedProductToCartButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProductPreview is null)
        {
            return;
        }

        AddProductToCart(SelectedProductPreview);
    }

    private void AddToCartButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ProductCardViewModel product })
        {
            return;
        }

        AddProductToCart(product, sender as Button);
    }

    private void AddProductToCart(ProductCardViewModel product, Button? sourceButton = null)
    {
        var existingItem = CartItems.FirstOrDefault(item => item.Product.ProductId == product.ProductId);
        if (existingItem is null)
        {
            CartItems.Add(new CartItemViewModel
            {
                Product = product,
                Quantity = 1
            });
        }
        else
        {
            existingItem.Quantity++;
        }

        RefreshCartState();

        if (sourceButton is not null)
        {
            AnimateAddToCartButton(sourceButton);
        }
    }

    private void FavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ProductCardViewModel product })
        {
            product.IsFavorite = !product.IsFavorite;
        }
    }

    private void FavoritesButton_Click(object sender, RoutedEventArgs e)
    {
        OpenFavoritesWindow();
    }

    private async void RussianLanguageButton_Click(object sender, RoutedEventArgs e)
    {
        await ChangeLanguageAsync(AppLanguage.Russian);
    }

    private async void EnglishLanguageButton_Click(object sender, RoutedEventArgs e)
    {
        await ChangeLanguageAsync(AppLanguage.English);
    }

    private void ToggleCartButton_Click(object sender, RoutedEventArgs e)
    {
        IsCartOpen = !IsCartOpen;
    }

    private async void MyOrdersButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isGuestAccount || !_currentUserId.HasValue)
        {
            MessageBox.Show(
                T(
                    "\u0412\u043e\u0439\u0434\u0438\u0442\u0435 \u0432 \u0430\u043a\u043a\u0430\u0443\u043d\u0442, \u0447\u0442\u043e\u0431\u044b \u0445\u0440\u0430\u043d\u0438\u0442\u044c \u0438 \u0441\u043c\u043e\u0442\u0440\u0435\u0442\u044c \u0441\u0432\u043e\u0438 \u0437\u0430\u043a\u0430\u0437\u044b.",
                    "Sign in to save and view your orders."),
                Title,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            var customerDetails = await GetSuggestedCheckoutCustomerDetailsAsync();

            var ordersWindow = new UserOrdersWindow(_currentUsername, _currentUserId, _isGuestAccount, customerDetails)
            {
                Owner = this
            };
            ordersWindow.ShowDialog();
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void NotificationsButton_Click(object sender, RoutedEventArgs e)
    {
        if (NotificationsPopup is null)
        {
            return;
        }

        var nextState = !NotificationsPopup.IsOpen;
        NotificationsPopup.IsOpen = nextState;
        IsNotificationsOpen = nextState;
        AnimateNotificationsBell();
    }

    private void NotificationsPopup_Closed(object sender, EventArgs e)
    {
        IsNotificationsOpen = false;
    }

    private void OpenFavoritesWindow()
    {
        var favoritesWindow = new FavoritesWindow(
            _allProducts,
            AddProductToCartFromFavorites,
            AddProductsToCartFromFavorites)
        {
            Owner = this
        };

        favoritesWindow.ShowDialog();
    }

    private void CloseCartButton_Click(object sender, RoutedEventArgs e)
    {
        IsCartOpen = false;
    }

    private void CartOverlay_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.OriginalSource == sender)
        {
            IsCartOpen = false;
        }
    }

    private void HeroHelpBadge_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        OpenGuideOverlay();
        e.Handled = true;
    }

    private void GuideOverlay_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource == sender)
        {
            CloseGuideOverlay();
        }
    }

    private void CloseGuideOverlayButton_Click(object sender, RoutedEventArgs e)
    {
        CloseGuideOverlay();
    }

    private void GuidePreviousButton_Click(object sender, RoutedEventArgs e)
    {
        if (CanGoGuidePrevious)
        {
            ShowGuideSlide(_currentGuideSlideIndex - 1);
        }
    }

    private void GuideNextButton_Click(object sender, RoutedEventArgs e)
    {
        if (CanGoGuideNext)
        {
            ShowGuideSlide(_currentGuideSlideIndex + 1);
        }
    }

    private void OpenProductDetails(ProductCardViewModel product)
    {
        SelectedProductPreview = product;
        ProductDetailsOverlayVisibility = Visibility.Visible;
        UpdateOverlayBlurState();
    }

    private void CloseProductDetails()
    {
        ProductDetailsOverlayVisibility = Visibility.Collapsed;
        SelectedProductPreview = null;
        UpdateOverlayBlurState();
    }

    private void IncreaseCartItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CartItemViewModel item })
        {
            item.Quantity++;
            RefreshCartState();
        }
    }

    private void DecreaseCartItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CartItemViewModel item })
        {
            return;
        }

        item.Quantity--;
        if (item.Quantity <= 0)
        {
            CartItems.Remove(item);
        }

        RefreshCartState();
    }

    private void RemoveCartItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CartItemViewModel item })
        {
            CartItems.Remove(item);
            RefreshCartState();
        }
    }

private void CheckoutButton_Click(object sender, RoutedEventArgs e)
{
    if (CartItems.Count == 0)
    {
        return;
    }

    MessageBox.Show(
        T(
            $"\u0412 \u043a\u043e\u0440\u0437\u0438\u043d\u0435 {CartItems.Sum(item => item.Quantity)} \u0442\u043e\u0432\u0430\u0440(\u043e\u0432) \u043d\u0430 \u0441\u0443\u043c\u043c\u0443 {CartTotalText}.",
            $"Your cart contains {CartItems.Sum(item => item.Quantity)} item(s) totaling {CartTotalText}."),
        T("\u041e\u0444\u043e\u0440\u043c\u043b\u0435\u043d\u0438\u0435 \u0437\u0430\u043a\u0430\u0437\u0430", "Checkout"),
        MessageBoxButton.OK,
        MessageBoxImage.Information);
}

private async void CheckoutOrderButton_Click(object sender, RoutedEventArgs e)
{
    if (CartItems.Count == 0)
    {
        return;
    }

    if (_isGuestAccount || !_currentUserId.HasValue)
    {
        SetCheckoutStatus(
            T(
                "\u0413\u043e\u0441\u0442\u0435\u0432\u043e\u0439 \u0440\u0435\u0436\u0438\u043c \u043d\u0435 \u0441\u043e\u0445\u0440\u0430\u043d\u044f\u0435\u0442 \u0437\u0430\u043a\u0430\u0437\u044b. \u0412\u043e\u0439\u0434\u0438\u0442\u0435 \u0432 \u0430\u043a\u043a\u0430\u0443\u043d\u0442, \u0447\u0442\u043e\u0431\u044b \u043e\u0444\u043e\u0440\u043c\u0438\u0442\u044c \u0438\u0445.",
                "Guest mode does not save orders. Sign in to place and keep orders."),
            DefaultBadgeTextBrush);

        MessageBox.Show(
            T(
                "\u0412 \u0433\u043e\u0441\u0442\u0435\u0432\u043e\u043c \u0440\u0435\u0436\u0438\u043c\u0435 \u0437\u0430\u043a\u0430\u0437\u044b \u0432 \u0431\u0430\u0437\u0443 \u043d\u0435 \u0441\u043e\u0445\u0440\u0430\u043d\u044f\u044e\u0442\u0441\u044f. \u0412\u043e\u0439\u0434\u0438\u0442\u0435 \u0432 \u043e\u0431\u044b\u0447\u043d\u044b\u0439 \u0430\u043a\u043a\u0430\u0443\u043d\u0442.",
                "Orders are not saved in guest mode. Sign in with a regular account."),
            Title,
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        return;
    }

    try
    {
        ClearCheckoutStatus();
        var suggestedCustomerDetails = await GetSuggestedCheckoutCustomerDetailsAsync();

        var checkoutForm = new CheckoutForm(_repository, _currentUsername, suggestedCustomerDetails)
        {
            Owner = this
        };

        if (checkoutForm.ShowDialog() != true || checkoutForm.Result is null)
        {
            return;
        }

        var request = BuildCheckoutRequest(checkoutForm.Result);
        CanCheckout = false;

        var result = await _repository.CreateCheckoutOrderAsync(request);
        _lastCheckoutCustomerDetails = checkoutForm.Result;
        UserCheckoutProfileStore.Save(_currentUsername, checkoutForm.Result);
        ApplyOrderNotificationState(result.OrderId, request.OrderStatus, request.StartDate);

        SetCheckoutStatus(
            T(
                $"\u0417\u0430\u043a\u0430\u0437 \u2116{result.OrderId} \u0443\u0441\u043f\u0435\u0448\u043d\u043e \u0441\u043e\u0445\u0440\u0430\u043d\u0451\u043d.",
                $"Order #{result.OrderId} was saved successfully."),
            SuccessStatusBrush);

        MessageBox.Show(
            T(
                $"\u0417\u0430\u043a\u0430\u0437 \u2116{result.OrderId} \u0443\u0441\u043f\u0435\u0448\u043d\u043e \u043e\u0444\u043e\u0440\u043c\u043b\u0435\u043d.\n\u0418\u0442\u043e\u0433: {result.TotalCost:0.##} MDL",
                $"Order #{result.OrderId} has been created.\nTotal: {result.TotalCost:0.##} MDL"),
            T("\u0417\u0430\u043a\u0430\u0437 \u043e\u0444\u043e\u0440\u043c\u043b\u0435\u043d", "Order created"),
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        CartItems.Clear();
        RefreshCartState();
        IsCartOpen = false;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            OpenNotificationsPopup();
            AnimateNotificationsBell();
        }), DispatcherPriority.Background);
    }
    catch (Exception exception)
    {
        SetCheckoutStatus(exception.Message, DefaultBadgeTextBrush);
        RefreshCartState();
    }
}

    private void ScrollToCatalogButton_Click(object sender, RoutedEventArgs e)
    {
        RootScrollViewer.ScrollToVerticalOffset(420);
    }

    private void PromoButton_Click(object sender, RoutedEventArgs e)
    {
        MuslinFilterCheckBox.IsChecked = true;
        RootScrollViewer.ScrollToVerticalOffset(420);
    }

private void RefreshCartState()
{
    var totalItems = CartItems.Sum(item => item.Quantity);
    var totalPrice = CartItems.Sum(item => item.LineTotal);
    var productIdsInCart = CartItems
        .Select(item => item.Product.ProductId)
        .ToHashSet();

    CartBadgeText = totalItems.ToString(CultureInfo.InvariantCulture);
    CartItemsCountText = FormatItemsCount(totalItems);
    CartTotalText = $"{totalPrice:0.##} MDL";
    CartBadgeVisibility = totalItems > 0 ? Visibility.Visible : Visibility.Collapsed;
    CartEmptyVisibility = totalItems == 0 ? Visibility.Visible : Visibility.Collapsed;
    CartItemsVisibility = totalItems > 0 ? Visibility.Visible : Visibility.Collapsed;
    CanCheckout = totalItems > 0;

    foreach (var product in _allProducts)
    {
        product.IsInCart = productIdsInCart.Contains(product.ProductId);
    }
}

private void AddProductToCartFromFavorites(ProductCardViewModel product)
    {
        var existingItem = CartItems.FirstOrDefault(item => item.Product.ProductId == product.ProductId);
        if (existingItem is null)
        {
            CartItems.Add(new CartItemViewModel
            {
                Product = product,
                Quantity = 1
            });
        }
        else
        {
            existingItem.Quantity++;
        }

        RefreshCartState();
    }

    private void AddProductsToCartFromFavorites(IReadOnlyList<ProductCardViewModel> products)
    {
        foreach (var product in products)
        {
            var existingItem = CartItems.FirstOrDefault(item => item.Product.ProductId == product.ProductId);
            if (existingItem is null)
            {
                CartItems.Add(new CartItemViewModel
                {
                    Product = product,
                    Quantity = 1
                });
            }
            else
            {
                existingItem.Quantity++;
            }
        }

        RefreshCartState();
    }

    private static void AnimateAddToCartButton(Button button)
    {
        if (button.RenderTransform is not ScaleTransform existingScaleTransform || existingScaleTransform.IsFrozen)
        {
            var scaleTransform = new ScaleTransform(1, 1);
            button.RenderTransformOrigin = new Point(0.5, 0.5);
            button.RenderTransform = scaleTransform;
        }

        var transform = (ScaleTransform)button.RenderTransform;

        var scaleAnimation = new DoubleAnimation
        {
            To = 1.06,
            Duration = TimeSpan.FromMilliseconds(220),
            AutoReverse = true,
            EasingFunction = new CubicEase
            {
                EasingMode = EasingMode.EaseOut
            }
        };

        transform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
    }

private async Task RefreshNotificationsAsync()
{
    var customerDetails = _lastCheckoutCustomerDetails ?? UserCheckoutProfileStore.Load(_currentUsername);
    if (customerDetails is null || string.IsNullOrWhiteSpace(customerDetails.FullName))
    {
        SetNotificationEmptyState();
        return;
    }

    try
    {
        var orders = await _repository.GetCustomerOrdersByIdentityAsync(customerDetails.FullName, customerDetails.PhoneDigits);
        var latestOrder = orders
            .OrderByDescending(order => order.OrderDate ?? DateTime.MinValue)
            .ThenByDescending(order => order.OrderId)
            .FirstOrDefault();

        if (latestOrder is null)
        {
            SetNotificationEmptyState();
            return;
        }

        NotificationBadgeText = "2";
        NotificationBadgeVisibility = Visibility.Visible;
        NotificationsListVisibility = Visibility.Visible;
        NotificationEmptyVisibility = Visibility.Collapsed;
        NotificationPhoneNumber = "069 906 396";
        NotificationOrderTitle = BuildNotificationTitle(latestOrder.OrderId);
        NotificationOrderBody = BuildNotificationBody(latestOrder.OrderDate, latestOrder.OrderStatus);
    }
    catch
    {
        SetNotificationEmptyState();
    }
}

private void ApplyOrderNotificationState(int orderId, string? orderStatus, DateTime? orderDate)
{
    NotificationBadgeText = "2";
    NotificationBadgeVisibility = Visibility.Visible;
    NotificationsListVisibility = Visibility.Visible;
    NotificationEmptyVisibility = Visibility.Collapsed;
    NotificationPhoneNumber = "069 906 396";
    NotificationOrderTitle = BuildNotificationTitle(orderId);
    NotificationOrderBody = BuildNotificationBody(orderDate, orderStatus);
}

    private void OpenNotificationsPopup()
    {
        if (NotificationsPopup is null)
        {
            return;
        }

        NotificationsPopup.DataContext = this;
        NotificationsPopup.IsOpen = false;
        NotificationsPopup.IsOpen = true;
        IsNotificationsOpen = true;
    }

    private void AnimateNotificationsBell()
    {
        if (NotificationsButton is null)
        {
            return;
        }

        var rotateTransform = NotificationsButton.RenderTransform as RotateTransform;
        if (rotateTransform is null || rotateTransform.IsFrozen)
        {
            rotateTransform = new RotateTransform(0);
            NotificationsButton.RenderTransformOrigin = new Point(0.5, 0.25);
            NotificationsButton.RenderTransform = rotateTransform;
        }

        var ringAnimation = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(640)
        };

        ringAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        ringAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(-18, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(90))));
        ringAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(16, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180))));
        ringAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(-12, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(280))));
        ringAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(9, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(390))));
        ringAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(-5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(500))));
        ringAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(640))));

        rotateTransform.BeginAnimation(RotateTransform.AngleProperty, ringAnimation);
    }

private void SetNotificationEmptyState()
{
    NotificationBadgeText = "0";
    NotificationBadgeVisibility = Visibility.Collapsed;
    NotificationsListVisibility = Visibility.Collapsed;
    NotificationEmptyVisibility = Visibility.Visible;
    NotificationOrderTitle = T("\u0417\u0430\u043a\u0430\u0437 \u043e\u0444\u043e\u0440\u043c\u043b\u0435\u043d", "Order created");
    NotificationOrderBody = T("\u041a\u0430\u043a \u0442\u043e\u043b\u044c\u043a\u043e \u043f\u043e\u044f\u0432\u0438\u0442\u0441\u044f \u0437\u0430\u043a\u0430\u0437, \u043c\u044b \u043f\u043e\u043a\u0430\u0436\u0435\u043c \u0435\u0433\u043e \u0441\u0442\u0430\u0442\u0443\u0441 \u0437\u0434\u0435\u0441\u044c.", "As soon as an order appears, we will show its status here.");
    NotificationPhoneNumber = "069 906 396";
}

private async Task<CheckoutCustomerDetails?> GetSuggestedCheckoutCustomerDetailsAsync()
    {
        var knownDetails = _lastCheckoutCustomerDetails ?? UserCheckoutProfileStore.Load(_currentUsername);

        if (_isGuestAccount || !_currentUserId.HasValue)
        {
            return knownDetails;
        }

        var refreshedDetails = await _repository.GetLatestCheckoutCustomerDetailsByUserIdAsync(_currentUserId.Value);
        _lastCheckoutCustomerDetails = refreshedDetails ?? knownDetails;
        return _lastCheckoutCustomerDetails;
    }

    private CheckoutOrderRequest BuildCheckoutRequest(CheckoutCustomerDetails customerDetails)
    {
        return new CheckoutOrderRequest
        {
            AccountUserId = _currentUserId,
            CustomerFullName = customerDetails.FullName,
            CustomerPhoneDigits = customerDetails.PhoneDigits,
            DeliveryAddress = customerDetails.DeliveryAddress,
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays(5),
            OrderStatus = "Pending",
            Items = CartItems
                .Select(item => new CheckoutOrderItem
                {
                    ProductId = item.Product.ProductId,
                    Quantity = item.Quantity
                })
                .ToList()
        };
    }

    private void ClearCheckoutStatus()
    {
        if (CheckoutStatusTextBlock is null)
        {
            return;
        }

        CheckoutStatusTextBlock.Text = string.Empty;
        CheckoutStatusTextBlock.Foreground = DefaultBadgeTextBrush;
    }

    private void SetCheckoutStatus(string message, Brush foreground)
    {
        if (CheckoutStatusTextBlock is null)
        {
            return;
        }

        CheckoutStatusTextBlock.Text = message;
        CheckoutStatusTextBlock.Foreground = foreground;
    }

    private static TControl? FindVisualAncestor<TControl>(DependencyObject? source)
        where TControl : DependencyObject
    {
        while (source is not null)
        {
            if (source is TControl match)
            {
                return match;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private static Uri BuildCategoryIconSource(string? categoryName)
    {
        var normalized = categoryName?.Trim().ToLowerInvariant() ?? string.Empty;

        var iconFileName = normalized switch
        {
            "\u0432\u0441\u0435 \u0442\u043e\u0432\u0430\u0440\u044b" or "all products" or "all items" => "flower1.svg",
            "\u0438\u0433\u0440\u0443\u0448\u043a\u0438" or "toys" => "shapes.svg",
            "\u043e\u0434\u0435\u0436\u0434\u0430" or "clothing" => "shirt.svg",
            "\u0434\u043b\u044f \u0441\u043d\u0430" or "sleep" => "moon.svg",
            "\u043a\u043e\u0440\u043c\u043b\u0435\u043d\u0438\u0435" or "feeding" => "milk.svg",
            "\u0433\u0438\u0433\u0438\u0435\u043d\u0430" or "hygiene" => "droplets2.svg",
            "\u043e\u0431\u0443\u0432\u044c" or "footwear" => "footprints.svg",
            "\u043d\u0430\u0431\u043e\u0440\u044b" or "sets" => "shopping-bag.svg",
            _ => "flower1.svg"
        };

        return new Uri(StorefrontAssetResolver.GetStorefrontIconPath(iconFileName), UriKind.Absolute);
    }

    private static (Brush Background, Brush Foreground) GetCategoryPalette(string? categoryName)
    {
        var normalized = categoryName?.Trim().ToLowerInvariant() ?? string.Empty;

        return normalized switch
        {
            "\u043e\u0434\u0435\u0436\u0434\u0430" or "clothing" => (CreateBrush("#FFFFE7EE"), CreateBrush("#FFEC5A8D")),
            "\u0438\u0433\u0440\u0443\u0448\u043a\u0438" or "toys" => (CreateBrush("#FFFFF1DF"), CreateBrush("#FFF29A2E")),
            "\u0434\u043b\u044f \u0441\u043d\u0430" or "sleep" => (CreateBrush("#FFF0EBFF"), CreateBrush("#FF7F65F5")),
            "\u043a\u043e\u0440\u043c\u043b\u0435\u043d\u0438\u0435" or "feeding" => (CreateBrush("#FFE7F8EE"), CreateBrush("#FF45AA6D")),
            "\u0433\u0438\u0433\u0438\u0435\u043d\u0430" or "hygiene" => (CreateBrush("#FFE6F9F6"), CreateBrush("#FF21AA93")),
            "\u043e\u0431\u0443\u0432\u044c" or "footwear" => (CreateBrush("#FFEAF3FF"), CreateBrush("#FF5E8EE8")),
            "\u043d\u0430\u0431\u043e\u0440\u044b" or "sets" => (CreateBrush("#FFF3ECFF"), CreateBrush("#FF8A63F6")),
            _ => (DefaultBadgeBrush, DefaultBadgeTextBrush)
        };
    }

    private static BitmapImage? TryLoadBitmap(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private static BitmapImage LoadBitmap(string path)
    {
        var resolvedBitmap = TryLoadBitmap(path);
        if (resolvedBitmap is not null)
        {
            return resolvedBitmap;
        }

        var fallbackBitmap = new BitmapImage();
        fallbackBitmap.BeginInit();
        fallbackBitmap.CacheOption = BitmapCacheOption.OnLoad;
        fallbackBitmap.UriSource = new Uri(StorefrontAssetResolver.GetDefaultProductImagePath(), UriKind.Absolute);
        fallbackBitmap.EndInit();
        fallbackBitmap.Freeze();
        return fallbackBitmap;
    }

    private static SolidColorBrush CreateBrush(string hexColor)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hexColor)!;
        brush.Freeze();
        return brush;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using BabyShop.Infrastructure;
using BabyShop.ViewModels;

namespace BabyShop;

public partial class FavoritesWindow : Window, INotifyPropertyChanged
{
    private readonly IReadOnlyList<ProductCardViewModel> _sourceProducts;
    private readonly Action<ProductCardViewModel> _addSingleToCart;
    private readonly Action<IReadOnlyList<ProductCardViewModel>> _addAllToCart;
    private string _searchQuery = string.Empty;
    private Visibility _searchPlaceholderVisibility = Visibility.Visible;
    private Visibility _emptyStateVisibility = Visibility.Collapsed;
    private int _favoritesCount;

    public FavoritesWindow(
        IReadOnlyList<ProductCardViewModel> sourceProducts,
        Action<ProductCardViewModel> addSingleToCart,
        Action<IReadOnlyList<ProductCardViewModel>> addAllToCart)
    {
        InitializeComponent();
        WindowAppearance.ApplySharedIcon(this);
        DataContext = this;

        _sourceProducts = sourceProducts;
        _addSingleToCart = addSingleToCart;
        _addAllToCart = addAllToCart;

        foreach (var product in _sourceProducts)
        {
            product.PropertyChanged += Product_PropertyChanged;
        }
    }

    public ObservableCollection<ProductCardViewModel> VisibleFavorites { get; } = [];

    public int FavoritesCount
    {
        get => _favoritesCount;
        private set
        {
            if (_favoritesCount == value)
            {
                return;
            }

            _favoritesCount = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FavoritesCountLabel));
        }
    }

    public string FavoritesCountLabel => BuildFavoritesCountLabel(FavoritesCount);

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

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshFavorites();
    }

    private void SearchTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _searchQuery = SearchTextBox.Text.Trim();
        SearchPlaceholderVisibility = string.IsNullOrWhiteSpace(_searchQuery) ? Visibility.Visible : Visibility.Collapsed;
        RefreshFavorites();
    }

    private void SortComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        RefreshFavorites();
    }

    private void AddSingleToCartButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ProductCardViewModel product })
        {
            return;
        }

        _addSingleToCart(product);
        product.IsFavorite = false;
        RefreshFavorites();
    }

    private void AddAllToCartButton_Click(object sender, RoutedEventArgs e)
    {
        var favorites = GetFavoriteProducts().ToList();
        if (favorites.Count == 0)
        {
            return;
        }

        _addAllToCart(favorites);

        foreach (var product in favorites)
        {
            product.IsFavorite = false;
        }

        RefreshFavorites();
    }

    private void ClearFavoritesButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var product in _sourceProducts.Where(product => product.IsFavorite))
        {
            product.IsFavorite = false;
        }

        RefreshFavorites();
    }

    private void Product_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProductCardViewModel.IsFavorite) ||
            e.PropertyName == nameof(ProductCardViewModel.IsInCart))
        {
            Dispatcher.Invoke(RefreshFavorites);
        }
    }

    private IEnumerable<ProductCardViewModel> GetFavoriteProducts()
    {
        IEnumerable<ProductCardViewModel> favorites = _sourceProducts.Where(product => product.IsFavorite);

        if (!string.IsNullOrWhiteSpace(_searchQuery))
        {
            favorites = favorites.Where(product =>
                product.ProductTitle.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                product.CategoryName.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                product.FabricType.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                product.Color.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase));
        }

        return GetSortMode() switch
        {
            "oldest" => favorites.OrderBy(product => product.ProductId),
            "title" => favorites.OrderBy(product => product.ProductTitle),
            "price" => favorites.OrderByDescending(product => product.UnitPrice),
            _ => favorites.OrderByDescending(product => product.ProductId)
        };
    }

    private void RefreshFavorites()
    {
        var favorites = GetFavoriteProducts().ToList();

        VisibleFavorites.Clear();
        foreach (var favorite in favorites)
        {
            VisibleFavorites.Add(favorite);
        }

        FavoritesCount = _sourceProducts.Count(product => product.IsFavorite);
        FavoritesBadgeTextBlock.Text = FavoritesCountLabel;
        EmptyStateVisibility = VisibleFavorites.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private string GetSortMode()
    {
        if (SortComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem { Tag: string tag } &&
            !string.IsNullOrWhiteSpace(tag))
        {
            return tag;
        }

        return "newest";
    }

    private static string BuildFavoritesCountLabel(int count) => $"{count.ToString(CultureInfo.CurrentCulture)} товаров";

    private static string FormatItemsLabel(int count)
    {
        return $"{count.ToString(CultureInfo.CurrentCulture)} товаров";
    }

    private void OnPropertyChanged(string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected override void OnClosed(EventArgs e)
    {
        foreach (var product in _sourceProducts)
        {
            product.PropertyChanged -= Product_PropertyChanged;
        }

        base.OnClosed(e);
    }
}

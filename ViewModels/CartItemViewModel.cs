using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace BabyShop.ViewModels;

public sealed class CartItemViewModel : INotifyPropertyChanged
{
    private int _quantity;

    public ProductCardViewModel Product { get; init; } = new();
    public BitmapImage? ProductImage => Product.ProductImage;
    public string ProductTitle => Product.ProductTitle;
    public string ProductMetaText => Product.FavoriteMetaText;
    public decimal UnitPrice => Product.UnitPrice;
    public string DisplayUnitPrice => $"{UnitPrice:0.##} MDL";
    public decimal LineTotal => UnitPrice * Quantity;
    public string DisplayLineTotal => $"{LineTotal:0.##} MDL";

    public int Quantity
    {
        get => _quantity;
        set
        {
            var normalized = Math.Max(0, value);
            if (_quantity == normalized)
            {
                return;
            }

            _quantity = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LineTotal));
            OnPropertyChanged(nameof(DisplayLineTotal));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

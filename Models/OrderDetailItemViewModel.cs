namespace BabyShop.Models;

public sealed class OrderDetailItemViewModel
{
    public int ProductId { get; init; }

    public string ProductTitle { get; init; } = string.Empty;

    public string CategoryName { get; init; } = string.Empty;

    public string FabricType { get; init; } = string.Empty;

    public string Color { get; init; } = string.Empty;

    public string ImagePath { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public decimal UnitPrice { get; init; }

    public decimal LineTotal { get; init; }
}

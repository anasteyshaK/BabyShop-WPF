namespace BabyShop.Models;

public sealed class OrderDetailsViewModel
{
    public int OrderId { get; init; }

    public int CustomerId { get; init; }

    public string ClientName { get; init; } = string.Empty;

    public string DeliveryAddress { get; init; } = string.Empty;

    public DateTime? StartDate { get; init; }

    public DateTime? EndDate { get; init; }

    public string OrderStatus { get; init; } = string.Empty;

    public decimal TotalCost { get; init; }

    public IReadOnlyList<OrderDetailItemViewModel> Items { get; set; } = Array.Empty<OrderDetailItemViewModel>();

    public int TotalPositions => Items.Count;

    public int TotalQuantity => Items.Sum(item => item.Quantity);
}

namespace BabyShop.Models;

public sealed class CheckoutOrderRequest
{
    public int? AccountUserId { get; init; }
    public string CustomerFullName { get; init; } = string.Empty;
    public string CustomerPhoneDigits { get; init; } = string.Empty;
    public string DeliveryAddress { get; init; } = string.Empty;
    public DateTime StartDate { get; init; } = DateTime.Today;
    public DateTime EndDate { get; init; } = DateTime.Today.AddDays(5);
    public string OrderStatus { get; init; } = "Pending";
    public IReadOnlyList<CheckoutOrderItem> Items { get; init; } = Array.Empty<CheckoutOrderItem>();
}

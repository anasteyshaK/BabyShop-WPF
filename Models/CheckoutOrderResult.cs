namespace BabyShop.Models;

public sealed class CheckoutOrderResult
{
    public int CustomerId { get; init; }
    public int OrderId { get; init; }
    public decimal TotalCost { get; init; }
}

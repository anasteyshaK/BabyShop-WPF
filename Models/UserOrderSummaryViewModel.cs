namespace BabyShop.Models;

public sealed class UserOrderSummaryViewModel
{
    public int OrderId { get; init; }

    public DateTime? OrderDate { get; init; }

    public string OrderStatus { get; init; } = string.Empty;

    public decimal TotalCost { get; init; }

    public string StatusDisplay => OrderStatus.Trim() switch
    {
        "Pending" => "Ожидает",
        "Shipped" => "Отправлен",
        "Completed" => "Завершён",
        _ => string.IsNullOrWhiteSpace(OrderStatus) ? "Неизвестно" : OrderStatus
    };
}

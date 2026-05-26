namespace BabyShop.Models;

public sealed class DashboardSummary
{
    public decimal TotalSum { get; init; }
    public int OrderCount { get; init; }
    public decimal AverageValue { get; init; }
    public decimal MinValue { get; init; }
    public decimal MaxValue { get; init; }
}

namespace BabyShop.Models;

public sealed class DashboardSeriesPoint
{
    public required string Label { get; init; }
    public decimal Value { get; init; }
    public decimal SecondaryValue { get; init; }
}

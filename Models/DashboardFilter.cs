namespace BabyShop.Models;

public sealed class DashboardFilter
{
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }
    public string? Status { get; init; }
    public string? ClientName { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public string? ProductTitle { get; init; }
    public string? FabricType { get; init; }
}

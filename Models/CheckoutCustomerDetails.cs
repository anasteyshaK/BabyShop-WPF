namespace BabyShop.Models;

public sealed class CheckoutCustomerDetails
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string PhoneDigits { get; init; } = string.Empty;
    public string DeliveryAddress { get; init; } = string.Empty;

    public string FullName => $"{FirstName} {LastName}".Trim();
}

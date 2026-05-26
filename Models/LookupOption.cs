namespace BabyShop.Models;

public sealed class LookupOption
{
    public required string Value { get; init; }
    public required string Label { get; init; }

    public override string ToString()
    {
        return Label;
    }
}

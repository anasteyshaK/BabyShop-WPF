namespace BabyShop.Models;

public sealed class BackupOperationResult
{
    public required bool Succeeded { get; init; }
    public required string Message { get; init; }
    public BackupHistoryEntry? HistoryEntry { get; init; }
    public string? FilePath { get; init; }
}

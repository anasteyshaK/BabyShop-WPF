using System.Globalization;

namespace BabyShop.Models;

public sealed class BackupHistoryEntry
{
    public int BackupId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string OperationType { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
    public decimal FileSizeKb { get; init; }
    public string DatabaseName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }

    public string FileSizeDisplay => FileSizeKb <= 0
        ? "-"
        : FileSizeKb.ToString("N2", CultureInfo.CurrentCulture);
}

using System.Diagnostics;
using System.Globalization;
using System.IO;
using BabyShop.Models;
using BabyShop.Reporting;

namespace BabyShop.Services;

public static class OrderReceiptService
{
    public static async Task PrintAsync(OrderDetailsViewModel order, CancellationToken cancellationToken = default)
    {
        var folderPath = Path.Combine(Path.GetTempPath(), "BabyShopReceipts");
        Directory.CreateDirectory(folderPath);

        var safeFileName = $"order-receipt-{order.OrderId}-{DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.html";
        var fullPath = Path.Combine(folderPath, safeFileName);
        var html = OrderReceiptComposer.BuildHtml(order, autoPrint: true);
        await File.WriteAllTextAsync(fullPath, html, cancellationToken);

        Process.Start(new ProcessStartInfo
        {
            FileName = fullPath,
            UseShellExecute = true
        });
    }
}

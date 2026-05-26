using System;
using System.Windows;
using System.Windows.Media.Imaging;

namespace BabyShop.Infrastructure;

internal static class WindowAppearance
{
    private static readonly Uri AppIconUri = new("pack://application:,,,/BabyShop;component/iconn.ico", UriKind.Absolute);

    public static void ApplySharedIcon(Window window)
    {
        try
        {
            window.Icon = BitmapFrame.Create(AppIconUri);
        }
        catch
        {
            // Keep the window usable even if the icon cannot be resolved.
        }
    }

    public static void ApplyLoginIcon(Window window)
    {
        ApplySharedIcon(window);
    }

    public static void ApplyCheckoutIcon(Window window)
    {
        ApplySharedIcon(window);
    }
}

using System.IO;

namespace BabyShop.Infrastructure;

public static class StorefrontAssetResolver
{
    private const string ProjectFileName = "BabyShop.csproj";

    public static string GetHeroBannerPath()
    {
        return GetAssetPath("Assets", "storefront", "hero-banner.png");
    }

    public static string GetPromoBannerPath()
    {
        return GetAssetPath("Assets", "storefront", "promo-muslin-banner.png");
    }

    public static string GetGuideCoverPath()
    {
        return GetAssetPath("Assets", "guide", "guide-cover.png");
    }

    public static string GetGuideLoginPath()
    {
        return GetAssetPath("Assets", "guide", "guide-login.png");
    }

    public static string GetGuideHomePath()
    {
        return GetAssetPath("Assets", "guide", "guide-home.png");
    }

    public static string GetGuideFavoritesPath()
    {
        return GetAssetPath("Assets", "guide", "guide-favorites.png");
    }

    public static string GetGuideOrdersPath()
    {
        return GetAssetPath("Assets", "guide", "guide-orders.png");
    }

    public static string GetGuideCartPath()
    {
        return GetAssetPath("Assets", "guide", "guide-cart.png");
    }

    public static string GetGuideFiltersPath()
    {
        return GetAssetPath("Assets", "guide", "guide-filters.png");
    }

    public static string GetStorefrontIconPath(string fileName)
    {
        return GetAssetPath("Assets", "storefront", fileName);
    }

    public static string GetDefaultProductImagePath()
    {
        return GetAssetPath("Assets", "Products", "default_product.png");
    }

    public static string ResolveProductImagePath(string? storedPath)
    {
        var imagePath = ProductImageStorage.ResolveImageAbsolutePath(storedPath);
        if (!string.IsNullOrWhiteSpace(imagePath))
        {
            return imagePath;
        }

        if (!string.IsNullOrWhiteSpace(storedPath))
        {
            var normalized = storedPath.Trim()
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);

            var directProjectPath = Path.Combine(GetProjectRoot(), normalized);
            if (File.Exists(directProjectPath))
            {
                return directProjectPath;
            }

            var fileName = Path.GetFileName(normalized);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                var productsPath = GetAssetPath("Assets", "Products", fileName);
                if (File.Exists(productsPath))
                {
                    return productsPath;
                }
            }
        }

        return GetDefaultProductImagePath();
    }

    private static string GetAssetPath(params string[] segments)
    {
        var parts = new string[segments.Length + 1];
        parts[0] = GetProjectRoot();
        Array.Copy(segments, 0, parts, 1, segments.Length);
        return Path.Combine(parts);
    }

    private static string GetProjectRoot()
    {
        var currentDirectory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);

        while (currentDirectory is not null)
        {
            if (File.Exists(Path.Combine(currentDirectory.FullName, ProjectFileName)))
            {
                return currentDirectory.FullName;
            }

            currentDirectory = currentDirectory.Parent;
        }

        return AppDomain.CurrentDomain.BaseDirectory;
    }
}

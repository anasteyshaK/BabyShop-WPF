using System.IO;
using System.Text.Json;
using BabyShop.Models;

namespace BabyShop.Infrastructure;

public static class UserCheckoutProfileStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public static CheckoutCustomerDetails? Load(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        try
        {
            var profiles = ReadProfiles();
            if (!profiles.TryGetValue(NormalizeUsername(username), out var profile))
            {
                return null;
            }

            return new CheckoutCustomerDetails
            {
                FirstName = profile.FirstName ?? string.Empty,
                LastName = profile.LastName ?? string.Empty,
                PhoneDigits = profile.PhoneDigits ?? string.Empty,
                DeliveryAddress = profile.DeliveryAddress ?? string.Empty
            };
        }
        catch
        {
            return null;
        }
    }

    public static void Save(string username, CheckoutCustomerDetails details)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentNullException.ThrowIfNull(details);

        try
        {
            var profiles = ReadProfiles();
            profiles[NormalizeUsername(username)] = new CheckoutProfileRecord
            {
                FirstName = details.FirstName,
                LastName = details.LastName,
                PhoneDigits = details.PhoneDigits,
                DeliveryAddress = details.DeliveryAddress,
                UpdatedAt = DateTime.UtcNow
            };

            var storageDirectory = Path.GetDirectoryName(GetStoragePath());
            if (!string.IsNullOrWhiteSpace(storageDirectory))
            {
                Directory.CreateDirectory(storageDirectory);
            }

            var json = JsonSerializer.Serialize(profiles, SerializerOptions);
            File.WriteAllText(GetStoragePath(), json);
        }
        catch
        {
            // Intentionally ignore local persistence failures to avoid blocking checkout.
        }
    }

    private static Dictionary<string, CheckoutProfileRecord> ReadProfiles()
    {
        var storagePath = GetStoragePath();
        if (!File.Exists(storagePath))
        {
            return new Dictionary<string, CheckoutProfileRecord>(StringComparer.OrdinalIgnoreCase);
        }

        var json = File.ReadAllText(storagePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, CheckoutProfileRecord>(StringComparer.OrdinalIgnoreCase);
        }

        return JsonSerializer.Deserialize<Dictionary<string, CheckoutProfileRecord>>(json, SerializerOptions)
            ?? new Dictionary<string, CheckoutProfileRecord>(StringComparer.OrdinalIgnoreCase);
    }

    private static string GetStoragePath()
    {
        var appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BabyShop");

        return Path.Combine(appDataFolder, "checkout-profiles.json");
    }

    private static string NormalizeUsername(string username)
    {
        return username.Trim().ToLowerInvariant();
    }

    private sealed class CheckoutProfileRecord
    {
        public string? FirstName { get; init; }
        public string? LastName { get; init; }
        public string? PhoneDigits { get; init; }
        public string? DeliveryAddress { get; init; }
        public DateTime UpdatedAt { get; init; }
    }
}

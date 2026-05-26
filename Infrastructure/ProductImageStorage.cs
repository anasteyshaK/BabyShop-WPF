using System.IO;

namespace BabyShop.Infrastructure;

public static class ProductImageStorage
{
    private const string ProjectFileName = "BabyShop.csproj";

    public static string GetImagesDirectory()
    {
        var directory = Path.Combine(GetProjectRoot(), "Assets", "images_pr");
        Directory.CreateDirectory(directory);
        return directory;
    }

    public static string? ResolveImageAbsolutePath(string? storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
        {
            return null;
        }

        var normalized = storedPath.Trim()
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        if (Path.IsPathRooted(normalized))
        {
            return File.Exists(normalized) ? normalized : null;
        }

        var projectRelativePath = Path.Combine(GetProjectRoot(), normalized);
        if (File.Exists(projectRelativePath))
        {
            return projectRelativePath;
        }

        var fileName = Path.GetFileName(normalized);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var imageDirectoryPath = Path.Combine(GetImagesDirectory(), fileName);
        return File.Exists(imageDirectoryPath) ? imageDirectoryPath : null;
    }

    public static string ImportImage(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new InvalidOperationException("The source image path is empty.");
        }

        var fullSourcePath = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullSourcePath))
        {
            throw new FileNotFoundException("The selected image file was not found.", fullSourcePath);
        }

        var imagesDirectory = GetImagesDirectory();
        var extension = Path.GetExtension(fullSourcePath);
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fullSourcePath);
        var safeBaseName = SanitizeFileName(fileNameWithoutExtension);
        if (string.IsNullOrWhiteSpace(safeBaseName))
        {
            safeBaseName = "product-image";
        }

        var destinationFileName = $"{safeBaseName}{extension}";
        var destinationPath = Path.Combine(imagesDirectory, destinationFileName);
        if (!PathsEqual(fullSourcePath, destinationPath))
        {
            destinationPath = GetAvailableDestinationPath(imagesDirectory, safeBaseName, extension, fullSourcePath);
            File.Copy(fullSourcePath, destinationPath, overwrite: false);
        }

        return Path.GetFileName(destinationPath);
    }

    private static string GetAvailableDestinationPath(
        string directoryPath,
        string baseName,
        string extension,
        string sourcePath)
    {
        var attempt = 0;

        while (true)
        {
            var suffix = attempt == 0 ? string.Empty : $"_{attempt}";
            var candidatePath = Path.Combine(directoryPath, $"{baseName}{suffix}{extension}");

            if (!File.Exists(candidatePath) || PathsEqual(candidatePath, sourcePath))
            {
                return candidatePath;
            }

            attempt++;
        }
    }

    private static bool PathsEqual(string leftPath, string rightPath)
    {
        return string.Equals(
            Path.GetFullPath(leftPath),
            Path.GetFullPath(rightPath),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitizedChars = fileName
            .Select(character => invalidChars.Contains(character) ? '_' : character)
            .ToArray();

        return new string(sanitizedChars).Trim();
    }

    private static string GetProjectRoot()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);

        while (currentDirectory is not null)
        {
            if (File.Exists(Path.Combine(currentDirectory.FullName, ProjectFileName)))
            {
                return currentDirectory.FullName;
            }

            currentDirectory = currentDirectory.Parent;
        }

        return AppContext.BaseDirectory;
    }
}

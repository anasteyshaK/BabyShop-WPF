using System.Diagnostics;
using System.IO;
using BabyShop.Configuration;
using BabyShop.Models;
using BabyShop.Repositories;

namespace BabyShop.Services;

public sealed class BackupService
{
    public const string CreateBackupPermission = "CREATE_BACKUP";
    public const string RestoreBackupPermission = "RESTORE_BACKUP";
    public const string ViewBackupHistoryPermission = "VIEW_BACKUP_HISTORY";

    private const string BackupActionType = "CREATE_BACKUP";
    private const string RestoreActionType = "RESTORE_BACKUP";
    private const string BackupEntityName = "backup_history";
    private const int MaxProcessMessageLength = 450;

    private readonly BabyShopRepository _repository;
    private readonly AppUser _currentUser;

    private sealed record ProcessExecutionResult(int ExitCode, string StandardOutput, string StandardError);

    public BackupService(BabyShopRepository repository, AppUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public bool CanCreateBackup => _currentUser.HasPermission(CreateBackupPermission);
    public bool CanRestoreBackup => _currentUser.HasPermission(RestoreBackupPermission);
    public bool CanViewBackupHistory => _currentUser.HasPermission(ViewBackupHistoryPermission);

    public Task<BackupHistoryEntry?> GetLastBackupAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetLastBackupAsync(cancellationToken);
    }

    public Task<IReadOnlyList<BackupHistoryEntry>> GetBackupHistoryAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetBackupHistoryAsync(cancellationToken);
    }

    public async Task<BackupOperationResult> CreateBackupAsync(
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        EnsurePermission(CanCreateBackup, CreateBackupPermission);

        var fullPath = Path.GetFullPath(destinationPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? AppContext.BaseDirectory);

        try
        {
            var dumpExecutable = FindMySqlExecutable("mysqldump.exe");
            var processResult = await RunDumpAsync(dumpExecutable, fullPath, cancellationToken);
            if (processResult.ExitCode != 0)
            {
                return await RecordFailureAsync(
                    operationType: "BACKUP",
                    filePath: fullPath,
                    message: BuildProcessMessage(processResult, "mysqldump failed."),
                    cancellationToken);
            }

            var fileInfo = new FileInfo(fullPath);
            var sizeKb = fileInfo.Exists ? Math.Round(fileInfo.Length / 1024m, 2) : 0m;
            var entry = await _repository.AddBackupHistoryAsync(
                _currentUser.UserId,
                _currentUser.Username,
                operationType: "BACKUP",
                fileName: fileInfo.Name,
                filePath: fileInfo.FullName,
                fileSizeKb: sizeKb,
                databaseName: DatabaseSettings.Database,
                status: "SUCCESS",
                message: "Backup created successfully.",
                cancellationToken);

            await _repository.AddAuditLogAsync(
                _currentUser.UserId,
                _currentUser.Username,
                BackupActionType,
                BackupEntityName,
                entityId: entry?.BackupId,
                actionDescription: $"Database backup created: {fileInfo.FullName}",
                cancellationToken);

            return new BackupOperationResult
            {
                Succeeded = true,
                Message = "Backup created successfully.",
                HistoryEntry = entry,
                FilePath = fileInfo.FullName
            };
        }
        catch (Exception exception)
        {
            return await RecordFailureAsync("BACKUP", fullPath, exception.Message, cancellationToken);
        }
    }

    public async Task<BackupOperationResult> CreateSafetyBackupAsync(CancellationToken cancellationToken = default)
    {
        var backupDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "BabyShopBackups");
        Directory.CreateDirectory(backupDirectory);

        var backupPath = Path.Combine(
            backupDirectory,
            $"{DatabaseSettings.Database}_safety_{DateTime.Now:yyyyMMdd_HHmmss}.sql");

        return await CreateBackupAsync(backupPath, cancellationToken);
    }

    public string GetProjectBackupDirectory()
    {
        var projectRoot = ResolveProjectRootDirectory();
        return Path.Combine(projectRoot, "ProjectBackups");
    }

    public async Task<BackupOperationResult> CreateProjectBackupAsync(
        bool automatic,
        CancellationToken cancellationToken = default)
    {
        var backupDirectory = GetProjectBackupDirectory();
        Directory.CreateDirectory(backupDirectory);

        var backupPath = Path.Combine(
            backupDirectory,
            $"{DatabaseSettings.Database}_{(automatic ? "auto" : "project")}_{DateTime.Now:yyyyMMdd_HHmmss}.sql");

        return await CreateBackupAsync(backupPath, cancellationToken);
    }

    public async Task<BackupOperationResult> RestoreBackupAsync(
        string sourceFilePath,
        bool createSafetyBackup,
        CancellationToken cancellationToken = default)
    {
        EnsurePermission(CanRestoreBackup, RestoreBackupPermission);

        var fullPath = Path.GetFullPath(sourceFilePath);
        if (!File.Exists(fullPath))
        {
            return await RecordFailureAsync("RESTORE", fullPath, "Selected backup file was not found.", cancellationToken);
        }

        if (createSafetyBackup)
        {
            var safetyResult = await CreateSafetyBackupAsync(cancellationToken);
            if (!safetyResult.Succeeded)
            {
                return await RecordFailureAsync("RESTORE", fullPath, $"Safety backup failed. {safetyResult.Message}", cancellationToken);
            }
        }

        try
        {
            var mysqlExecutable = FindMySqlExecutable("mysql.exe");
            var processResult = await RunRestoreAsync(mysqlExecutable, fullPath, cancellationToken);
            if (processResult.ExitCode != 0)
            {
                return await RecordFailureAsync(
                    operationType: "RESTORE",
                    filePath: fullPath,
                    message: BuildProcessMessage(processResult, "mysql restore failed."),
                    cancellationToken);
            }

            var fileInfo = new FileInfo(fullPath);
            var sizeKb = fileInfo.Exists ? Math.Round(fileInfo.Length / 1024m, 2) : 0m;
            var entry = await _repository.AddBackupHistoryAsync(
                _currentUser.UserId,
                _currentUser.Username,
                operationType: "RESTORE",
                fileName: fileInfo.Name,
                filePath: fileInfo.FullName,
                fileSizeKb: sizeKb,
                databaseName: DatabaseSettings.Database,
                status: "SUCCESS",
                message: "Restore completed successfully.",
                cancellationToken);

            await _repository.AddAuditLogAsync(
                _currentUser.UserId,
                _currentUser.Username,
                RestoreActionType,
                BackupEntityName,
                entityId: entry?.BackupId,
                actionDescription: $"Database restored from: {fileInfo.FullName}",
                cancellationToken);

            return new BackupOperationResult
            {
                Succeeded = true,
                Message = "Restore completed successfully.",
                HistoryEntry = entry,
                FilePath = fileInfo.FullName
            };
        }
        catch (Exception exception)
        {
            return await RecordFailureAsync("RESTORE", fullPath, exception.Message, cancellationToken);
        }
    }

    private static void EnsurePermission(bool condition, string permissionCode)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Access denied. Missing permission: {permissionCode}.");
        }
    }

    private async Task<BackupOperationResult> RecordFailureAsync(
        string operationType,
        string filePath,
        string message,
        CancellationToken cancellationToken)
    {
        var safeMessage = Truncate(message);
        var fileName = string.IsNullOrWhiteSpace(filePath) ? string.Empty : Path.GetFileName(filePath);
        var sizeKb = File.Exists(filePath)
            ? Math.Round(new FileInfo(filePath).Length / 1024m, 2)
            : 0m;

        var entry = await _repository.AddBackupHistoryAsync(
            _currentUser.UserId,
            _currentUser.Username,
            operationType,
            fileName,
            filePath,
            sizeKb,
            DatabaseSettings.Database,
            "FAILED",
            safeMessage,
            cancellationToken);

        await _repository.AddAuditLogAsync(
            _currentUser.UserId,
            _currentUser.Username,
            operationType.Equals("BACKUP", StringComparison.OrdinalIgnoreCase) ? BackupActionType : RestoreActionType,
            BackupEntityName,
            entityId: entry?.BackupId,
            actionDescription: $"{operationType} failed. {safeMessage}",
            cancellationToken);

        return new BackupOperationResult
        {
            Succeeded = false,
            Message = safeMessage,
            HistoryEntry = entry,
            FilePath = filePath
        };
    }

    private static async Task<ProcessExecutionResult> RunDumpAsync(
        string executablePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        var startInfo = CreateBaseStartInfo(executablePath);
        AddConnectionArguments(startInfo);
        startInfo.ArgumentList.Add("--routines");
        startInfo.ArgumentList.Add("--triggers");
        startInfo.ArgumentList.Add("--events");
        startInfo.ArgumentList.Add("--single-transaction");
        startInfo.ArgumentList.Add("--databases");
        startInfo.ArgumentList.Add(DatabaseSettings.Database);
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        await using var outputStream = File.Create(destinationPath);
        var copyTask = process.StandardOutput.BaseStream.CopyToAsync(outputStream, cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await Task.WhenAll(copyTask, errorTask, process.WaitForExitAsync(cancellationToken));

        return new ProcessExecutionResult(process.ExitCode, string.Empty, await errorTask);
    }

    private static async Task<ProcessExecutionResult> RunRestoreAsync(
        string executablePath,
        string sourceFilePath,
        CancellationToken cancellationToken)
    {
        var startInfo = CreateBaseStartInfo(executablePath);
        AddConnectionArguments(startInfo);
        startInfo.ArgumentList.Add($"--database={DatabaseSettings.Database}");
        startInfo.RedirectStandardInput = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await using (var fileStream = File.OpenRead(sourceFilePath))
        {
            await fileStream.CopyToAsync(process.StandardInput.BaseStream, cancellationToken);
        }

        await process.StandardInput.FlushAsync();
        process.StandardInput.Close();

        await Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync(cancellationToken));

        return new ProcessExecutionResult(process.ExitCode, await outputTask, await errorTask);
    }

    private static ProcessStartInfo CreateBaseStartInfo(string executablePath)
    {
        return new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = AppContext.BaseDirectory
        };
    }

    private static void AddConnectionArguments(ProcessStartInfo startInfo)
    {
        startInfo.ArgumentList.Add($"--host={DatabaseSettings.Server}");
        startInfo.ArgumentList.Add($"--port={DatabaseSettings.Port}");
        startInfo.ArgumentList.Add($"--user={DatabaseSettings.Username}");
        if (!string.IsNullOrEmpty(DatabaseSettings.Password))
        {
            startInfo.ArgumentList.Add($"--password={DatabaseSettings.Password}");
        }
    }

    private static string BuildProcessMessage(ProcessExecutionResult processResult, string fallbackMessage)
    {
        var rawMessage = string.Join(
            Environment.NewLine,
            new[] { processResult.StandardError, processResult.StandardOutput }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim()));

        if (string.IsNullOrWhiteSpace(rawMessage))
        {
            rawMessage = fallbackMessage;
        }

        return Truncate(rawMessage);
    }

    private static string Truncate(string value)
    {
        return value.Length <= MaxProcessMessageLength
            ? value
            : value[..MaxProcessMessageLength];
    }

    private static string FindMySqlExecutable(string executableName)
    {
        foreach (var candidate in GetExecutableCandidates(executableName))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException($"MySQL client tool was not found: {executableName}");
    }

    private static IEnumerable<string> GetExecutableCandidates(string executableName)
    {
        var directCandidates = new List<string>
        {
            Path.Combine("C:\\xampp\\mysql\\bin", executableName),
            Path.Combine("C:\\Program Files\\MySQL\\MySQL Server 8.0\\bin", executableName),
            Path.Combine("C:\\Program Files (x86)\\MySQL\\MySQL Server 8.0\\bin", executableName)
        };

        foreach (var pathValue in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return Path.Combine(pathValue, executableName);
        }

        foreach (var candidate in directCandidates)
        {
            yield return candidate;
        }

        foreach (var root in new[] { "C:\\Program Files\\MySQL", "C:\\Program Files (x86)\\MySQL" })
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var directory in Directory.GetDirectories(root, "*", SearchOption.TopDirectoryOnly))
            {
                yield return Path.Combine(directory, "bin", executableName);
            }
        }
    }

    private static string ResolveProjectRootDirectory()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDirectory is not null)
        {
            if (File.Exists(Path.Combine(currentDirectory.FullName, "BabyShop.csproj")))
            {
                return currentDirectory.FullName;
            }

            currentDirectory = currentDirectory.Parent;
        }

        return AppContext.BaseDirectory;
    }
}

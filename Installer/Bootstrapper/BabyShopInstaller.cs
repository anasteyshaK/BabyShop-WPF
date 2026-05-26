using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BabyShopInstaller
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length > 0 && string.Equals(args[0], "/silent", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var installRoot = InstallerEngine.GetDefaultInstallRoot();
                    InstallerEngine.Install(installRoot, null);
                    Environment.Exit(0);
                    return;
                }
                catch
                {
                    Environment.Exit(1);
                    return;
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new InstallerForm());
        }
    }

    internal static class InstallerEngine
    {
        public static string GetDefaultInstallRoot()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BabyShop");
        }

        public static void Install(string installRoot, Action<string> reportProgress)
        {
            var appDir = Path.Combine(installRoot, "app");
            var appExePath = Path.Combine(appDir, "BabyShop.exe");
            var desktopShortcutPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "BabyShop.lnk");
            var startMenuDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft\\Windows\\Start Menu\\Programs\\BabyShop");
            var startMenuShortcutPath = Path.Combine(startMenuDir, "BabyShop.lnk");
            var uninstallScriptPath = Path.Combine(installRoot, "uninstall.cmd");
            var readmePath = Path.Combine(installRoot, "README.txt");

            Report(reportProgress, "Подготовка папок...");
            Directory.CreateDirectory(installRoot);

            if (Directory.Exists(appDir))
            {
                Directory.Delete(appDir, true);
            }

            Directory.CreateDirectory(appDir);

            Report(reportProgress, "Распаковка файлов приложения...");
            var tempZipPath = Path.Combine(Path.GetTempPath(), "BabyShopPackage_" + Guid.NewGuid().ToString("N") + ".zip");

            try
            {
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("BabyShop.Payload.Zip"))
                {
                    if (stream == null)
                    {
                        throw new InvalidOperationException("Не найден встроенный пакет приложения.");
                    }

                    using (var fileStream = File.Create(tempZipPath))
                    {
                        stream.CopyTo(fileStream);
                    }
                }

                ZipFile.ExtractToDirectory(tempZipPath, appDir);
            }
            finally
            {
                if (File.Exists(tempZipPath))
                {
                    File.Delete(tempZipPath);
                }
            }

            if (!File.Exists(appExePath))
            {
                throw new FileNotFoundException("После распаковки не найден BabyShop.exe.", appExePath);
            }

            Report(reportProgress, "Создание ярлыков и служебных файлов...");
            WriteReadme(readmePath, installRoot);
            WriteUninstallScript(uninstallScriptPath, installRoot, desktopShortcutPath, startMenuDir);
            CreateShortcuts(desktopShortcutPath, startMenuDir, startMenuShortcutPath, appExePath, appDir);
            RegisterUninstallEntry(installRoot, uninstallScriptPath, appExePath);
        }

        private static void WriteReadme(string readmePath, string installRoot)
        {
            var content =
                "BabyShop installed successfully." + Environment.NewLine + Environment.NewLine +
                "Install location:" + Environment.NewLine +
                installRoot + Environment.NewLine + Environment.NewLine +
                "Before launching the application, make sure the MySQL database baby_shop_restored is available in XAMPP.";

            File.WriteAllText(readmePath, content);
        }

        private static void WriteUninstallScript(
            string uninstallScriptPath,
            string installRoot,
            string desktopShortcutPath,
            string startMenuDir)
        {
            var script =
                "@echo off" + Environment.NewLine +
                "setlocal" + Environment.NewLine +
                "taskkill /IM BabyShop.exe /F >nul 2>nul" + Environment.NewLine +
                "set \"INSTALL_ROOT=" + installRoot + "\"" + Environment.NewLine +
                "set \"DESKTOP_SHORTCUT=" + desktopShortcutPath + "\"" + Environment.NewLine +
                "set \"STARTMENU_DIR=" + startMenuDir + "\"" + Environment.NewLine +
                "if exist \"%DESKTOP_SHORTCUT%\" del \"%DESKTOP_SHORTCUT%\" >nul 2>nul" + Environment.NewLine +
                "if exist \"%STARTMENU_DIR%\\BabyShop.lnk\" del \"%STARTMENU_DIR%\\BabyShop.lnk\" >nul 2>nul" + Environment.NewLine +
                "if exist \"%STARTMENU_DIR%\" rmdir \"%STARTMENU_DIR%\" >nul 2>nul" + Environment.NewLine +
                "reg delete \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\BabyShop\" /f >nul 2>nul" + Environment.NewLine +
                "if exist \"%INSTALL_ROOT%\\app\" rmdir /S /Q \"%INSTALL_ROOT%\\app\"" + Environment.NewLine +
                "if exist \"%INSTALL_ROOT%\\README.txt\" del \"%INSTALL_ROOT%\\README.txt\" >nul 2>nul" + Environment.NewLine +
                "if exist \"%INSTALL_ROOT%\\uninstall.cmd\" del \"%INSTALL_ROOT%\\uninstall.cmd\" >nul 2>nul";

            File.WriteAllText(uninstallScriptPath, script);
        }

        private static void CreateShortcuts(
            string desktopShortcutPath,
            string startMenuDir,
            string startMenuShortcutPath,
            string appExePath,
            string appDir)
        {
            CreateShortcut(desktopShortcutPath, appExePath, appDir, "BabyShop");

            if (!Directory.Exists(startMenuDir))
            {
                Directory.CreateDirectory(startMenuDir);
            }

            CreateShortcut(startMenuShortcutPath, appExePath, appDir, "BabyShop");
        }

        private static void CreateShortcut(string shortcutPath, string targetPath, string workingDirectory, string description)
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
            {
                throw new InvalidOperationException("Не удалось создать ярлык: WScript.Shell недоступен.");
            }

            var shell = Activator.CreateInstance(shellType);
            var shortcut = shellType.InvokeMember(
                "CreateShortcut",
                BindingFlags.InvokeMethod,
                null,
                shell,
                new object[] { shortcutPath });

            var shortcutType = shortcut.GetType();
            shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
            shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workingDirectory });
            shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { description });
            shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath + ",0" });
            shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }

        private static void RegisterUninstallEntry(string installRoot, string uninstallScriptPath, string appExePath)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\BabyShop"))
            {
                if (key == null)
                {
                    return;
                }

                key.SetValue("DisplayName", "BabyShop");
                key.SetValue("DisplayVersion", "1.0");
                key.SetValue("Publisher", "BabyShop");
                key.SetValue("InstallLocation", installRoot);
                key.SetValue("DisplayIcon", appExePath);
                key.SetValue("UninstallString", uninstallScriptPath);
            }
        }

        private static void Report(Action<string> reportProgress, string text)
        {
            if (reportProgress != null)
            {
                reportProgress(text);
            }
        }
    }

    internal sealed class InstallerForm : Form
    {
        private readonly string _installRoot;
        private readonly string _appDir;
        private readonly string _appExePath;
        private Label _statusLabel;
        private Button _installButton;
        private Button _cancelButton;
        private CheckBox _launchAfterInstallCheckBox;

        public InstallerForm()
        {
            _installRoot = InstallerEngine.GetDefaultInstallRoot();
            _appDir = Path.Combine(_installRoot, "app");
            _appExePath = Path.Combine(_appDir, "BabyShop.exe");

            BuildUi();
        }

        private void BuildUi()
        {
            Text = "Установка BabyShop";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(560, 320);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.White;

            var titleLabel = new Label
            {
                AutoSize = false,
                Location = new Point(24, 24),
                Size = new Size(500, 44),
                Font = new Font("Segoe UI", 20f, FontStyle.Bold),
                ForeColor = Color.FromArgb(32, 44, 83),
                Text = "BabyShop Setup"
            };

            var subtitleLabel = new Label
            {
                AutoSize = false,
                Location = new Point(24, 72),
                Size = new Size(500, 48),
                Font = new Font("Segoe UI", 10.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(120, 130, 160),
                Text = "Приложение будет установлено в локальную папку пользователя вместе со всеми рабочими файлами."
            };

            var pathTitleLabel = new Label
            {
                AutoSize = false,
                Location = new Point(24, 132),
                Size = new Size(220, 24),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(50, 60, 90),
                Text = "Папка установки"
            };

            var pathTextBox = new TextBox
            {
                Location = new Point(24, 160),
                Size = new Size(500, 36),
                ReadOnly = true,
                Font = new Font("Segoe UI", 10f, FontStyle.Regular),
                Text = _installRoot
            };

            _launchAfterInstallCheckBox = new CheckBox
            {
                AutoSize = true,
                Location = new Point(24, 214),
                Font = new Font("Segoe UI", 10f, FontStyle.Regular),
                ForeColor = Color.FromArgb(50, 60, 90),
                Text = "Запустить BabyShop после установки",
                Checked = true
            };

            _statusLabel = new Label
            {
                AutoSize = false,
                Location = new Point(24, 244),
                Size = new Size(340, 24),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(120, 130, 160),
                Text = "Готово к установке."
            };

            _installButton = new Button
            {
                Location = new Point(360, 236),
                Size = new Size(164, 48),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(237, 88, 140),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Text = "Установить"
            };
            _installButton.FlatAppearance.BorderSize = 0;
            _installButton.Click += InstallButton_Click;

            _cancelButton = new Button
            {
                Location = new Point(244, 236),
                Size = new Size(104, 48),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(255, 245, 249),
                ForeColor = Color.FromArgb(50, 60, 90),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Text = "Отмена"
            };
            _cancelButton.FlatAppearance.BorderColor = Color.FromArgb(243, 208, 223);
            _cancelButton.FlatAppearance.BorderSize = 1;
            _cancelButton.Click += delegate { Close(); };

            Controls.Add(titleLabel);
            Controls.Add(subtitleLabel);
            Controls.Add(pathTitleLabel);
            Controls.Add(pathTextBox);
            Controls.Add(_launchAfterInstallCheckBox);
            Controls.Add(_statusLabel);
            Controls.Add(_cancelButton);
            Controls.Add(_installButton);
        }

        private void InstallButton_Click(object sender, EventArgs e)
        {
            ToggleUi(false);

            try
            {
                InstallerEngine.Install(_installRoot, UpdateStatus);

                _statusLabel.Text = "Установка завершена успешно.";

                if (_launchAfterInstallCheckBox.Checked && File.Exists(_appExePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = _appExePath,
                        WorkingDirectory = _appDir
                    });
                }

                MessageBox.Show(
                    "BabyShop успешно установлен.",
                    "Готово",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                Close();
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "Установка завершилась с ошибкой.";

                MessageBox.Show(
                    "Не удалось установить BabyShop.\r\n\r\n" + ex.Message,
                    "Ошибка установки",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                ToggleUi(true);
            }
        }

        private void UpdateStatus(string text)
        {
            _statusLabel.Text = text;
            Refresh();
        }

        private void ToggleUi(bool enabled)
        {
            _installButton.Enabled = enabled;
            _cancelButton.Enabled = enabled;
            _launchAfterInstallCheckBox.Enabled = enabled;
        }
    }
}

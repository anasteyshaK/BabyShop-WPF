using System;
using System.IO;
using System.Media;
using System.Windows;
using BabyShop.Configuration;
using BabyShop.Infrastructure;
using BabyShop.Repositories;

namespace BabyShop;

public partial class LoginWindow : Window
{
    private const string OpeningSoundRelativePath = "Assets\\sounds\\mixkit-opening-software-interface-2578.wav";
    private const string ClickSoundRelativePath = "Assets\\sounds\\mixkit-interface-device-click-2577.wav";
    private const string ErrorSoundRelativePath = "Assets\\sounds\\mixkit-click-error-1110.wav";

    private readonly AuthRepository _authRepository;
    private bool _isSynchronizingPassword;

    public LoginWindow()
    {
        InitializeComponent();
        WindowAppearance.ApplyLoginIcon(this);
        Loaded += LoginWindow_Loaded;
        var dbHelper = new DbHelper(DatabaseSettings.BuildConnectionString());
        _authRepository = new AuthRepository(dbHelper);
    }

    private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
    {
        PlayWindowSound(OpeningSoundRelativePath);
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        PlayWindowSound(ClickSoundRelativePath);
        LoginStatusTextBlock.Text = string.Empty;

        var username = LoginUsernameTextBox.Text.Trim();
        var password = LoginPasswordBox.Password;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            PlayWindowSound(ErrorSoundRelativePath);
            LoginStatusTextBlock.Text = "Enter username and password.";
            return;
        }

        try
        {
            var user = await _authRepository.AuthenticateAsync(username, password);
            if (user is null)
            {
                PlayWindowSound(ErrorSoundRelativePath);
                LoginStatusTextBlock.Text = "Invalid username or password.";
                return;
            }

            Window nextWindow = user.IsAdmin
                ? new MainWindow(user)
                : new UserMainWindow(user.Username, user.UserId);

            nextWindow.Show();
            Close();
        }
        catch (Exception exception)
        {
            PlayWindowSound(ErrorSoundRelativePath);
            LoginStatusTextBlock.Text = exception.Message;
        }
    }

    private void OpenRegisterWindow_Click(object sender, RoutedEventArgs e)
    {
        PlayWindowSound(ClickSoundRelativePath);
        var registerWindow = new RegisterForm
        {
            Owner = this
        };

        var result = registerWindow.ShowDialog();
        if (result == true && !string.IsNullOrWhiteSpace(registerWindow.RegisteredUsername))
        {
            LoginUsernameTextBox.Text = registerWindow.RegisteredUsername;
            LoginStatusTextBlock.Text = "Registration completed. Sign in with your new account.";
            LoginPasswordBox.Focus();
        }
    }

    private void OpenStorefrontPreview_Click(object sender, RoutedEventArgs e)
    {
        PlayWindowSound(ClickSoundRelativePath);
        var storefrontWindow = new UserMainWindow("Guest", userId: null, isGuestAccount: true)
        {
            Owner = this
        };

        storefrontWindow.ShowDialog();
    }

    private void LoginPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_isSynchronizingPassword)
        {
            return;
        }

        _isSynchronizingPassword = true;
        LoginPasswordTextBox.Text = LoginPasswordBox.Password;
        _isSynchronizingPassword = false;

        UpdatePasswordPresentation();
    }

    private void LoginPasswordTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isSynchronizingPassword)
        {
            return;
        }

        _isSynchronizingPassword = true;
        LoginPasswordBox.Password = LoginPasswordTextBox.Text;
        _isSynchronizingPassword = false;

        UpdatePasswordPresentation();
    }

    private void ShowPasswordCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        PlayWindowSound(ClickSoundRelativePath);
        _isSynchronizingPassword = true;
        LoginPasswordTextBox.Text = LoginPasswordBox.Password;
        _isSynchronizingPassword = false;

        UpdatePasswordPresentation();
    }

    private void UpdatePasswordPresentation()
    {
        var showPassword = ShowPasswordCheckBox.IsChecked == true;
        LoginPasswordBox.Visibility = showPassword ? Visibility.Collapsed : Visibility.Visible;
        LoginPasswordTextBox.Visibility = showPassword ? Visibility.Visible : Visibility.Collapsed;

        var currentPassword = showPassword ? LoginPasswordTextBox.Text : LoginPasswordBox.Password;
        PasswordPlaceholderTextBlock.Visibility = string.IsNullOrEmpty(currentPassword)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private static void PlayWindowSound(string relativePath)
    {
        try
        {
            var fullPath = Path.Combine(AppContext.BaseDirectory, relativePath);
            if (!File.Exists(fullPath))
            {
                return;
            }

            using var player = new SoundPlayer(fullPath);
            player.Play();
        }
        catch
        {
            // Sounds are optional and should never break the login window.
        }
    }
}

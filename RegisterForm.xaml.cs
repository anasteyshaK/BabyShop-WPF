using System.Text.RegularExpressions;
using System.Windows;
using BabyShop.Configuration;
using BabyShop.Infrastructure;
using BabyShop.Models;
using BabyShop.Repositories;

namespace BabyShop;

public partial class RegisterForm : Window
{
    private static readonly Regex UsernamePattern = new("^[A-Za-z0-9_.-]+$", RegexOptions.Compiled);
    private readonly AuthRepository _authRepository;
    private bool _isSynchronizingPasswords;

    public RegisterForm()
    {
        InitializeComponent();
        WindowAppearance.ApplySharedIcon(this);
        var dbHelper = new DbHelper(DatabaseSettings.BuildConnectionString());
        _authRepository = new AuthRepository(dbHelper);
    }

    public string? RegisteredUsername { get; private set; }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadRolesAsync();
    }

    private async Task LoadRolesAsync()
    {
        RegisterButton.IsEnabled = false;
        RoleComboBox.IsEnabled = false;
        RegisterStatusTextBlock.Text = "Loading registration settings...";

        try
        {
            var roles = await _authRepository.GetRegistrationRolesAsync();
            var availableRoles = roles
                .Where(role => !role.RoleName.Equals("admin", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (availableRoles.Count == 0)
            {
                availableRoles = roles.ToList();
            }

            RoleComboBox.ItemsSource = availableRoles;
            RoleComboBox.SelectedIndex = availableRoles.FindIndex(role => role.RoleName.Equals("user", StringComparison.OrdinalIgnoreCase));
            if (RoleComboBox.SelectedIndex < 0 && availableRoles.Count > 0)
            {
                RoleComboBox.SelectedIndex = 0;
            }

            UsernameTextBox.Focus();

            RegisterStatusTextBlock.Text = availableRoles.Count == 0
                ? "No available registration roles were found in the database."
                : string.Empty;
        }
        catch (Exception exception)
        {
            RegisterStatusTextBlock.Text = exception.Message;
        }
        finally
        {
            RegisterButton.IsEnabled = RoleComboBox.Items.Count > 0;
            RoleComboBox.IsEnabled = RoleComboBox.Items.Count > 0;
        }
    }

    private async void RegisterButton_Click(object sender, RoutedEventArgs e)
    {
        RegisterStatusTextBlock.Text = string.Empty;

        var username = UsernameTextBox.Text.Trim();
        var password = PasswordBox.Password;
        var confirmPassword = ConfirmPasswordBox.Password;
        var selectedRole = RoleComboBox.SelectedItem as RegistrationRoleOption;

        var validationMessage = ValidateInput(username, password, confirmPassword, selectedRole);
        if (!string.IsNullOrWhiteSpace(validationMessage))
        {
            RegisterStatusTextBlock.Text = validationMessage;
            return;
        }

        RegisterButton.IsEnabled = false;

        try
        {
            await _authRepository.RegisterUserAsync(username, password, selectedRole!.RoleName);
            RegisteredUsername = username;

            MessageBox.Show(
                "Registration completed successfully. You can now sign in.",
                "Successful registration",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }
        catch (Exception exception)
        {
            RegisterStatusTextBlock.Text = exception.Message;
            PasswordBox.Clear();
            ConfirmPasswordBox.Clear();
            PasswordBox.Focus();
            RegisterButton.IsEnabled = true;
        }
    }

    private static string? ValidateInput(
        string username,
        string password,
        string confirmPassword,
        RegistrationRoleOption? selectedRole)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return "Username is required.";
        }

        if (username.Length < 3)
        {
            return "Username must contain at least 3 characters.";
        }

        if (!UsernamePattern.IsMatch(username))
        {
            return "Username can contain only letters, digits, dot, underscore, and dash.";
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return "Password is required.";
        }

        if (password.Length < 6)
        {
            return "Password must contain at least 6 characters.";
        }

        if (password != confirmPassword)
        {
            return "Password confirmation does not match.";
        }

        if (selectedRole is null)
        {
            return "Select a database role for the new account.";
        }

        return null;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_isSynchronizingPasswords)
        {
            return;
        }

        _isSynchronizingPasswords = true;
        PasswordTextBox.Text = PasswordBox.Password;
        _isSynchronizingPasswords = false;
    }

    private void PasswordTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isSynchronizingPasswords)
        {
            return;
        }

        _isSynchronizingPasswords = true;
        PasswordBox.Password = PasswordTextBox.Text;
        _isSynchronizingPasswords = false;
    }

    private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_isSynchronizingPasswords)
        {
            return;
        }

        _isSynchronizingPasswords = true;
        ConfirmPasswordTextBox.Text = ConfirmPasswordBox.Password;
        _isSynchronizingPasswords = false;
    }

    private void ConfirmPasswordTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isSynchronizingPasswords)
        {
            return;
        }

        _isSynchronizingPasswords = true;
        ConfirmPasswordBox.Password = ConfirmPasswordTextBox.Text;
        _isSynchronizingPasswords = false;
    }

    private void PasswordRevealToggleButton_Changed(object sender, RoutedEventArgs e)
    {
        TogglePasswordVisibility(PasswordRevealToggleButton, PasswordBox, PasswordTextBox);
    }

    private void ConfirmPasswordRevealToggleButton_Changed(object sender, RoutedEventArgs e)
    {
        TogglePasswordVisibility(ConfirmPasswordRevealToggleButton, ConfirmPasswordBox, ConfirmPasswordTextBox);
    }

    private static void TogglePasswordVisibility(System.Windows.Controls.Primitives.ToggleButton toggleButton, System.Windows.Controls.PasswordBox passwordBox, System.Windows.Controls.TextBox textBox)
    {
        var isVisible = toggleButton.IsChecked == true;
        if (isVisible)
        {
            textBox.Text = passwordBox.Password;
        }

        passwordBox.Visibility = isVisible ? Visibility.Collapsed : Visibility.Visible;
        textBox.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;

        if (isVisible)
        {
            textBox.Focus();
            textBox.CaretIndex = textBox.Text.Length;
        }
        else
        {
            passwordBox.Focus();
        }
    }
}

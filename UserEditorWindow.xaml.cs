using System.Text.RegularExpressions;
using System.Windows;
using BabyShop.Infrastructure;
using BabyShop.Localization;
using BabyShop.Models;

namespace BabyShop;

public partial class UserEditorWindow : Window
{
    private static readonly Regex UsernamePattern = new("^[A-Za-z0-9_.-]+$", RegexOptions.Compiled);
    private readonly bool _isEditMode;

    public UserEditorWindow(
        IReadOnlyList<RegistrationRoleOption> roleOptions,
        bool isEditMode,
        string? username = null,
        string? roleName = null,
        bool isActive = true)
    {
        InitializeComponent();
        WindowAppearance.ApplySharedIcon(this);
        _isEditMode = isEditMode;

        RoleComboBox.ItemsSource = roleOptions;
        RoleComboBox.SelectedValue = roleName;
        if (RoleComboBox.SelectedIndex < 0 && roleOptions.Count > 0)
        {
            RoleComboBox.SelectedIndex = 0;
        }

        UsernameTextBox.Text = username ?? string.Empty;
        IsActiveCheckBox.IsChecked = isActive;

        ApplyLocalization();
        UpdateRoleHint();
        RoleComboBox.SelectionChanged += (_, _) => UpdateRoleHint();
    }

    public string SubmittedUsername { get; private set; } = string.Empty;
    public string SubmittedPassword { get; private set; } = string.Empty;
    public string SubmittedRoleName { get; private set; } = string.Empty;
    public bool SubmittedIsActive { get; private set; }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        StatusTextBlock.Text = string.Empty;

        var username = UsernameTextBox.Text.Trim();
        var password = PasswordBox.Password;
        var confirmPassword = ConfirmPasswordBox.Password;
        var role = RoleComboBox.SelectedItem as RegistrationRoleOption;
        var validationMessage = Validate(username, password, confirmPassword, role);

        if (!string.IsNullOrWhiteSpace(validationMessage))
        {
            StatusTextBlock.Text = validationMessage;
            return;
        }

        SubmittedUsername = username;
        SubmittedPassword = password;
        SubmittedRoleName = role!.RoleName;
        SubmittedIsActive = IsActiveCheckBox.IsChecked == true;

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private string? Validate(
        string username,
        string password,
        string confirmPassword,
        RegistrationRoleOption? role)
    {
        var isRussian = LanguageManager.CurrentLanguage == AppLanguage.Russian;

        if (string.IsNullOrWhiteSpace(username))
        {
            return isRussian ? "Введите логин." : "Enter a username.";
        }

        if (username.Length < 3)
        {
            return isRussian ? "Логин должен содержать минимум 3 символа." : "Username must contain at least 3 characters.";
        }

        if (!UsernamePattern.IsMatch(username))
        {
            return isRussian
                ? "Логин может содержать только буквы, цифры, точку, подчеркивание и дефис."
                : "Username can contain only letters, digits, dot, underscore, and dash.";
        }

        if (!_isEditMode && string.IsNullOrWhiteSpace(password))
        {
            return isRussian ? "Введите пароль." : "Enter a password.";
        }

        if (!string.IsNullOrWhiteSpace(password) && password.Length < 6)
        {
            return isRussian ? "Пароль должен содержать минимум 6 символов." : "Password must contain at least 6 characters.";
        }

        if (password != confirmPassword)
        {
            return isRussian ? "Подтверждение пароля не совпадает." : "Password confirmation does not match.";
        }

        if (role is null)
        {
            return isRussian ? "Выберите роль пользователя." : "Select a user role.";
        }

        return null;
    }

    private void ApplyLocalization()
    {
        var isRussian = LanguageManager.CurrentLanguage == AppLanguage.Russian;

        Title = isRussian
            ? (_isEditMode ? "Редактирование пользователя" : "Добавление пользователя")
            : (_isEditMode ? "Edit User" : "Add User");
        TitleTextBlock.Text = Title;
        SubtitleTextBlock.Text = isRussian
            ? (_isEditMode
                ? "Обновите логин, роль, активность и пароль при необходимости."
                : "Заполните данные новой учетной записи.")
            : (_isEditMode
                ? "Update the username, role, activity and password if needed."
                : "Fill in the new account information.");
        UsernameLabelTextBlock.Text = isRussian ? "Логин" : "Username";
        RoleLabelTextBlock.Text = isRussian ? "Роль" : "Role";
        PasswordLabelTextBlock.Text = isRussian ? "Пароль" : "Password";
        ConfirmPasswordLabelTextBlock.Text = isRussian ? "Подтверждение пароля" : "Confirm password";
        PasswordHintTextBlock.Text = isRussian
            ? (_isEditMode
                ? "Оставьте поле пустым, если не хотите менять пароль. Минимум 6 символов."
                : "Минимум 6 символов.")
            : (_isEditMode
                ? "Leave this field empty if you do not want to change the password. Minimum 6 characters."
                : "Minimum 6 characters.");
        IsActiveCheckBox.Content = isRussian ? "Активный пользователь" : "Active user";
        CancelButton.Content = isRussian ? "Отмена" : "Cancel";
        SaveButton.Content = isRussian ? "Сохранить" : "Save";
        RoleComboBox.Items.Refresh();
    }

    private void UpdateRoleHint()
    {
        var role = RoleComboBox.SelectedItem as RegistrationRoleOption;
        if (role is null)
        {
            RoleHintTextBlock.Text = LanguageManager.CurrentLanguage == AppLanguage.Russian
                ? "Выберите роль для пользователя."
                : "Select a role for the user.";
            return;
        }

        RoleHintTextBlock.Text = string.IsNullOrWhiteSpace(role.PermissionSummary)
            ? role.LocalizedRoleDescription
            : $"{role.LocalizedRoleDescription} • {role.PermissionSummary}";
    }
}

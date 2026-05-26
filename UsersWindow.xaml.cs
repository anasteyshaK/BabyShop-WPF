using System.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using BabyShop.Configuration;
using BabyShop.Infrastructure;
using BabyShop.Localization;
using BabyShop.Models;
using BabyShop.Repositories;

namespace BabyShop;

public partial class UsersWindow : Window
{
    private const string ManageUsersPermission = "MANAGE_USERS";
    private readonly AppUser _currentUser;
    private readonly BabyShopRepository _repository;
    private readonly AuthRepository _authRepository;
    private IReadOnlyList<RegistrationRoleOption> _roleOptions = Array.Empty<RegistrationRoleOption>();
    private bool _isLoading;

    public UsersWindow(AppUser currentUser)
    {
        InitializeComponent();
        WindowAppearance.ApplySharedIcon(this);
        _currentUser = currentUser;

        var dbHelper = new DbHelper(DatabaseSettings.BuildConnectionString());
        _repository = new BabyShopRepository(dbHelper);
        _authRepository = new AuthRepository(dbHelper);

        ApplyLocalization();
        UpdateActionButtons();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (!CanManageUsers())
        {
            MessageBox.Show(
                LanguageManager.CurrentLanguage == AppLanguage.Russian
                    ? "У вас нет прав на просмотр и управление пользователями."
                    : "You do not have permission to view or manage users.",
                Title,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            Close();
            return;
        }

        await LoadUsersAsync();
    }

    private async void AddButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var roleOptions = await EnsureRoleOptionsLoadedAsync();
            var editor = new UserEditorWindow(roleOptions, isEditMode: false)
            {
                Owner = this
            };

            if (editor.ShowDialog() != true)
            {
                return;
            }

            await _repository.AddUserAsync(
                _currentUser,
                editor.SubmittedUsername,
                editor.SubmittedPassword,
                editor.SubmittedRoleName,
                editor.SubmittedIsActive);

            StatusTextBlock.Text = LanguageManager.CurrentLanguage == AppLanguage.Russian
                ? "Пользователь успешно добавлен."
                : "User added successfully.";
            await LoadUsersAsync();
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelectedUser(out var selectedUser))
        {
            return;
        }

        try
        {
            var roleOptions = await EnsureRoleOptionsLoadedAsync();
            var editor = new UserEditorWindow(
                roleOptions,
                isEditMode: true,
                username: selectedUser.Username,
                roleName: selectedUser.RoleName,
                isActive: selectedUser.IsActive)
            {
                Owner = this
            };

            if (editor.ShowDialog() != true)
            {
                return;
            }

            await _repository.UpdateUserAsync(
                _currentUser,
                selectedUser.UserId,
                editor.SubmittedUsername,
                editor.SubmittedPassword,
                editor.SubmittedRoleName,
                editor.SubmittedIsActive);

            StatusTextBlock.Text = LanguageManager.CurrentLanguage == AppLanguage.Russian
                ? "Данные пользователя успешно обновлены."
                : "User details updated successfully.";
            await LoadUsersAsync();
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelectedUser(out var selectedUser))
        {
            return;
        }

        var isRussian = LanguageManager.CurrentLanguage == AppLanguage.Russian;
        var confirmation = MessageBox.Show(
            isRussian
                ? $"Удалить пользователя \"{selectedUser.Username}\"?"
                : $"Delete user \"{selectedUser.Username}\"?",
            Title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _repository.DeleteUserAsync(_currentUser, selectedUser.UserId);
            StatusTextBlock.Text = isRussian
                ? "Пользователь успешно удален."
                : "User deleted successfully.";
            await LoadUsersAsync();
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadUsersAsync();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void UsersDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateActionButtons();
    }

    private async Task LoadUsersAsync()
    {
        if (_isLoading)
        {
            return;
        }

        _isLoading = true;
        UpdateActionButtons();
        StatusTextBlock.Text = LanguageManager.CurrentLanguage == AppLanguage.Russian
            ? "Загрузка пользователей..."
            : "Loading users...";

        try
        {
            var table = await _repository.GetDisplayTableAsync("USERSVIEW");
            UsersDataGrid.ItemsSource = table.DefaultView;
            RecordsCountTextBlock.Text = LanguageManager.CurrentLanguage == AppLanguage.Russian
                ? $"Записей: {table.Rows.Count}"
                : $"Records: {table.Rows.Count}";
            StatusTextBlock.Text = LanguageManager.CurrentLanguage == AppLanguage.Russian
                ? "Таблица пользователей успешно загружена."
                : "Users table loaded successfully.";
        }
        catch (Exception exception)
        {
            UsersDataGrid.ItemsSource = null;
            RecordsCountTextBlock.Text = LanguageManager.CurrentLanguage == AppLanguage.Russian
                ? "Записей: 0"
                : "Records: 0";
            StatusTextBlock.Text = LanguageManager.CurrentLanguage == AppLanguage.Russian
                ? "Не удалось загрузить пользователей."
                : "Failed to load users.";

            MessageBox.Show(exception.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isLoading = false;
            UpdateActionButtons();
        }
    }

    private void UsersDataGrid_AutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        var propertyName = e.PropertyName;
        var normalizedName = NormalizeColumnName(propertyName);

        if (propertyName.StartsWith("__src_", StringComparison.OrdinalIgnoreCase) ||
            normalizedName is not ("username" or "createdat" or "lastloginat"))
        {
            e.Cancel = true;
            return;
        }

        switch (normalizedName)
        {
            case "username":
                e.Column.Header = LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Логин" : "Username";
                e.Column.Width = new DataGridLength(1.3, DataGridLengthUnitType.Star);
                break;
            case "createdat":
                e.Column.Header = LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Дата создания" : "Created At";
                e.Column.Width = new DataGridLength(180);
                break;
            case "lastloginat":
                e.Column.Header = LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Последний вход" : "Last Login";
                e.Column.Width = new DataGridLength(180);
                break;
        }

        if (e.Column is DataGridTextColumn textColumn &&
            (normalizedName == "createdat" || normalizedName == "lastloginat") &&
            textColumn.Binding is Binding binding)
        {
            binding.StringFormat = CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern + " HH:mm";

            if (normalizedName == "lastloginat")
            {
                binding.TargetNullValue = LanguageManager.CurrentLanguage == AppLanguage.Russian
                    ? "Никогда"
                    : "Never";
            }
        }
    }

    private void ApplyLocalization()
    {
        var isRussian = LanguageManager.CurrentLanguage == AppLanguage.Russian;
        Title = isRussian ? "Пользователи" : "Users";
        WindowTitleTextBlock.Text = isRussian ? "Пользователи" : "Users";
        WindowSubtitleTextBlock.Text = isRussian
            ? "В таблице показываются только логин, дата создания и последний вход."
            : "Only the username, created date and last login are shown in the table.";
        AddButton.Content = isRussian ? "Добавить" : "Add";
        EditButton.Content = isRussian ? "Изменить" : "Edit";
        DeleteButton.Content = isRussian ? "Удалить" : "Delete";
        RefreshButton.Content = isRussian ? "Обновить" : "Refresh";
        CloseButton.Content = isRussian ? "Закрыть" : "Close";
        RecordsCountTextBlock.Text = isRussian ? "Записей: 0" : "Records: 0";
        StatusTextBlock.Text = isRussian ? "Окно пользователей готово к работе." : "Users window is ready.";
    }

    private bool CanManageUsers()
    {
        return _currentUser.IsAdmin || _currentUser.HasPermission(ManageUsersPermission);
    }

    private void UpdateActionButtons()
    {
        var canManage = CanManageUsers() && !_isLoading;
        var hasSelection = UsersDataGrid.SelectedItem is DataRowView;

        AddButton.IsEnabled = canManage;
        EditButton.IsEnabled = canManage && hasSelection;
        DeleteButton.IsEnabled = canManage && hasSelection;
        RefreshButton.IsEnabled = !_isLoading;
    }

    private async Task<IReadOnlyList<RegistrationRoleOption>> EnsureRoleOptionsLoadedAsync()
    {
        if (_roleOptions.Count > 0)
        {
            return _roleOptions;
        }

        var roles = await _authRepository.GetRegistrationRolesAsync();
        _roleOptions = roles.ToList();
        return _roleOptions;
    }

    private bool TryGetSelectedUser(out SelectedUser user)
    {
        user = default;

        if (UsersDataGrid.SelectedItem is not DataRowView rowView)
        {
            return false;
        }

        var sourceValues = BabyShopRepository.GetSourceValues(rowView);
        if (!sourceValues.TryGetValue("user_id", out var idValue) || idValue is null or DBNull)
        {
            return false;
        }

        user = new SelectedUser(
            Convert.ToInt32(idValue, CultureInfo.InvariantCulture),
            Convert.ToString(GetSourceValue(sourceValues, "username"), CultureInfo.InvariantCulture) ?? string.Empty,
            Convert.ToString(GetSourceValue(sourceValues, "role_name"), CultureInfo.InvariantCulture) ?? string.Empty,
            GetBooleanValue(GetSourceValue(sourceValues, "is_active")));
        return true;
    }

    private static object? GetSourceValue(IReadOnlyDictionary<string, object?> sourceValues, string columnName)
    {
        return sourceValues.TryGetValue(columnName, out var value) ? value : null;
    }

    private static bool GetBooleanValue(object? value)
    {
        return value switch
        {
            bool booleanValue => booleanValue,
            sbyte signedByte => signedByte != 0,
            byte unsignedByte => unsignedByte != 0,
            short shortValue => shortValue != 0,
            int intValue => intValue != 0,
            long longValue => longValue != 0,
            string textValue when bool.TryParse(textValue, out var parsed) => parsed,
            string textValue when int.TryParse(textValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInt) => parsedInt != 0,
            _ => false
        };
    }

    private static string NormalizeColumnName(string name)
    {
        return name
            .Replace("__src_", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("_", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .ToLowerInvariant();
    }

    private readonly record struct SelectedUser(int UserId, string Username, string RoleName, bool IsActive);
}

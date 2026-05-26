using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BabyShop.Infrastructure;
using BabyShop.Models;
using BabyShop.Repositories;

namespace BabyShop;

public partial class CheckoutForm : Window
{
    private static readonly Regex PhoneRegex = new(@"^\d{8}$", RegexOptions.Compiled);
    private static readonly Regex NameRegex = new(@"^[\p{L}][\p{L}\p{M}' -]{1,39}$", RegexOptions.Compiled);
    private const string PhonePrefix = "+373 ";

    private readonly BabyShopRepository _repository;
    private readonly CheckoutCustomerDetails? _suggestedCustomerDetails;
    private bool _isUpdatingPhoneText;
    private bool _isUpdatingNormalizedText;

    public CheckoutForm(
        BabyShopRepository repository,
        string? suggestedFullName = null,
        CheckoutCustomerDetails? suggestedCustomerDetails = null)
    {
        InitializeComponent();
        WindowAppearance.ApplyCheckoutIcon(this);
        _repository = repository;
        _suggestedCustomerDetails = suggestedCustomerDetails;

        PhoneTextBox.Text = FormatPhoneDisplay(null);
        PhoneTextBox.CaretIndex = PhoneTextBox.Text.Length;
        PhoneTextBox.TextChanged += PhoneTextBox_TextChanged;
        PhoneTextBox.PreviewTextInput += PhoneTextBox_PreviewTextInput;
        PhoneTextBox.PreviewKeyDown += PhoneTextBox_PreviewKeyDown;
        DataObject.AddPastingHandler(PhoneTextBox, PhoneTextBox_Pasting);
        FirstNameTextBox.TextChanged += NameTextBox_TextChanged;
        LastNameTextBox.TextChanged += NameTextBox_TextChanged;

        ApplySuggestedName(suggestedFullName);
        ApplySuggestedCustomerDetails(suggestedCustomerDetails);
    }

    public CheckoutCustomerDetails? Result { get; private set; }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadAddressOptionsAsync();
        ApplySuggestedAddress(_suggestedCustomerDetails);
        FirstNameTextBox.Focus();
    }

    private async Task LoadAddressOptionsAsync()
    {
        SubmitButton.IsEnabled = false;
        ValidationTextBlock.Text = "Загружаем адреса...";

        try
        {
            AddressComboBox.ItemsSource = await _repository.GetAddressOptionsAsync();
            ValidationTextBlock.Text = string.Empty;
        }
        catch (Exception exception)
        {
            ValidationTextBlock.Text = exception.Message;
        }
        finally
        {
            SubmitButton.IsEnabled = true;
        }
    }

    private void SubmitButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var details = BuildResult();
            Result = details;
            DialogResult = true;
            Close();
        }
        catch (Exception exception)
        {
            ValidationTextBlock.Text = exception.Message;
        }
    }

    private CheckoutCustomerDetails BuildResult()
    {
        ValidationTextBlock.Text = string.Empty;

        var firstName = FirstNameTextBox.Text.Trim();
        var lastName = LastNameTextBox.Text.Trim();
        var phoneDigits = ExtractPhoneDigits(PhoneTextBox.Text);
        var address = AddressComboBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(firstName))
        {
            throw new InvalidOperationException("Введите имя клиента.");
        }

        if (!NameRegex.IsMatch(firstName))
        {
            throw new InvalidOperationException("Имя должно содержать только буквы и быть не короче 2 символов.");
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            throw new InvalidOperationException("Введите фамилию клиента.");
        }

        if (!NameRegex.IsMatch(lastName))
        {
            throw new InvalidOperationException("Фамилия должна содержать только буквы и быть не короче 2 символов.");
        }

        if (!PhoneRegex.IsMatch(phoneDigits))
        {
            throw new InvalidOperationException("Телефон должен содержать ровно 8 цифр после +373.");
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            throw new InvalidOperationException("Введите адрес доставки.");
        }

        if (address.Length > 75)
        {
            throw new InvalidOperationException("Адрес доставки не должен превышать 75 символов.");
        }

        if (!address.Any(char.IsDigit) || !address.Any(char.IsLetter))
        {
            throw new InvalidOperationException("В адресе должны быть и буквы, и цифры.");
        }

        return new CheckoutCustomerDetails
        {
            FirstName = NormalizeDisplayText(firstName),
            LastName = NormalizeDisplayText(lastName),
            PhoneDigits = phoneDigits,
            DeliveryAddress = address
        };
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void PhoneTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !e.Text.All(char.IsDigit);
    }

    private void PhoneTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingPhoneText || sender is not TextBox textBox)
        {
            return;
        }

        UpdatePhoneTextBox(textBox, ExtractPhoneDigits(textBox.Text));
    }

    private void PhoneTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox || _isUpdatingPhoneText)
        {
            return;
        }

        if (e.Key is not Key.Back and not Key.Delete)
        {
            return;
        }

        var digits = ExtractPhoneDigits(textBox.Text);
        if (digits.Length == 0)
        {
            e.Handled = true;
            textBox.Text = FormatPhoneDisplay(null);
            textBox.CaretIndex = textBox.Text.Length;
            return;
        }

        var shortened = digits[..^1];
        UpdatePhoneTextBox(textBox, shortened);
        e.Handled = true;
    }

    private void PhoneTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.DataObject.GetDataPresent(typeof(string)))
        {
            e.CancelCommand();
            return;
        }

        if (sender is not TextBox textBox)
        {
            return;
        }

        var pastedText = e.DataObject.GetData(typeof(string)) as string;
        UpdatePhoneTextBox(textBox, ExtractPhoneDigits(pastedText));
        e.CancelCommand();
    }

    private void NameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingNormalizedText || sender is not TextBox textBox)
        {
            return;
        }

        var normalizedText = NormalizeDisplayText(textBox.Text);
        if (string.Equals(normalizedText, textBox.Text, StringComparison.Ordinal))
        {
            return;
        }

        var caretIndex = textBox.CaretIndex;
        _isUpdatingNormalizedText = true;
        textBox.Text = normalizedText;
        textBox.CaretIndex = Math.Min(caretIndex, textBox.Text.Length);
        _isUpdatingNormalizedText = false;
    }

    private void UpdatePhoneTextBox(TextBox textBox, string digits)
    {
        _isUpdatingPhoneText = true;
        textBox.Text = FormatPhoneDisplay(digits);
        textBox.CaretIndex = textBox.Text.Length;
        _isUpdatingPhoneText = false;
    }

    private void ApplySuggestedName(string? suggestedFullName)
    {
        if (string.IsNullOrWhiteSpace(suggestedFullName) ||
            string.Equals(suggestedFullName, "Guest", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var parts = suggestedFullName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length > 0 && string.IsNullOrWhiteSpace(FirstNameTextBox.Text))
        {
            FirstNameTextBox.Text = parts[0];
        }

        if (parts.Length > 1 && string.IsNullOrWhiteSpace(LastNameTextBox.Text))
        {
            LastNameTextBox.Text = string.Join(' ', parts.Skip(1));
        }
    }

    private void ApplySuggestedCustomerDetails(CheckoutCustomerDetails? details)
    {
        if (details is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(details.FirstName))
        {
            FirstNameTextBox.Text = details.FirstName;
        }

        if (!string.IsNullOrWhiteSpace(details.LastName))
        {
            LastNameTextBox.Text = details.LastName;
        }

        if (!string.IsNullOrWhiteSpace(details.PhoneDigits))
        {
            PhoneTextBox.Text = FormatPhoneDisplay(details.PhoneDigits);
            PhoneTextBox.CaretIndex = PhoneTextBox.Text.Length;
        }
    }

    private void ApplySuggestedAddress(CheckoutCustomerDetails? details)
    {
        if (details is null || string.IsNullOrWhiteSpace(details.DeliveryAddress))
        {
            return;
        }

        AddressComboBox.Text = details.DeliveryAddress;
    }

    private static string ExtractPhoneDigits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("373", StringComparison.Ordinal))
        {
            digits = digits[3..];
        }

        return digits.Length > 8 ? digits[..8] : digits;
    }

    private static string FormatPhoneDisplay(string? value)
    {
        var digits = ExtractPhoneDigits(value);
        var builder = new System.Text.StringBuilder(PhonePrefix);

        if (digits.Length > 0)
        {
            builder.Append(digits[..Math.Min(2, digits.Length)]);
        }

        if (digits.Length > 2)
        {
            builder.Append('-');
            builder.Append(digits.Substring(2, Math.Min(3, digits.Length - 2)));
        }

        if (digits.Length > 5)
        {
            builder.Append('-');
            builder.Append(digits.Substring(5, Math.Min(3, digits.Length - 5)));
        }

        return builder.ToString();
    }

    private static string NormalizeDisplayText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var lowered = value.ToLower(System.Globalization.CultureInfo.CurrentCulture);
        var characters = lowered.ToCharArray();
        var capitalizeNext = true;

        for (var i = 0; i < characters.Length; i++)
        {
            if (char.IsLetter(characters[i]))
            {
                characters[i] = capitalizeNext
                    ? char.ToUpper(characters[i], System.Globalization.CultureInfo.CurrentCulture)
                    : characters[i];
                capitalizeNext = false;
            }
            else if (char.IsWhiteSpace(characters[i]) || characters[i] is '-' or '\'')
            {
                capitalizeNext = true;
            }
        }

        return new string(characters);
    }
}

using System.Globalization;
using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BabyShop.Localization;
using BabyShop.Models;
using BabyShop.Repositories;
using Microsoft.Win32;
using BabyShop.Infrastructure;

namespace BabyShop;

public partial class AddForm : Window
{
    private static readonly Regex NameRegex = new(@"^[\p{L}][\p{L}\p{M}' -]{1,39}$", RegexOptions.Compiled);
    private static readonly Regex PhoneRegex = new(@"^\d{8}$", RegexOptions.Compiled);
    private const string PhonePrefix = "+373 ";
    private static readonly IReadOnlyList<LookupOption> OrderStatusOptions =
    [
        new LookupOption { Value = "Pending", Label = "Pending - awaiting processing" },
        new LookupOption { Value = "Shipped", Label = "Shipped - sent to customer" },
        new LookupOption { Value = "Completed", Label = "Completed - finished" }
    ];

    private readonly BabyShopRepository _repository;
    private readonly string _tableName;
    private readonly bool _isEditMode;
    private readonly IReadOnlyDictionary<string, object?> _initialValues;
    private readonly Dictionary<ColumnMetadata, FrameworkElement> _inputs = new();
    private readonly Dictionary<string, Image> _imagePreviews = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string?> _selectedImageSourcePaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly ObservableCollection<OrderProductSelection> _orderProductSelections = [];
    private readonly Dictionary<OrderProductSelection, FrameworkElement> _orderProductRowMap = new();
    private IReadOnlyList<ColumnMetadata> _columns = Array.Empty<ColumnMetadata>();
    private bool _isUpdatingPhoneText;
    private bool _isUpdatingNormalizedText;
    private TextBox? _orderProductsSearchTextBox;

    private sealed class OrderProductSelection : INotifyPropertyChanged
    {
        private bool _isSelected;
        private int _quantity = 1;

        public int ProductId { get; init; }
        public string ProductLabel { get; init; } = string.Empty;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                {
                    return;
                }

                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public int Quantity
        {
            get => _quantity;
            set
            {
                var normalized = value < 1 ? 1 : value;
                if (_quantity == normalized)
                {
                    return;
                }

                _quantity = normalized;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Quantity)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public AddForm(
        BabyShopRepository repository,
        string tableName,
        IReadOnlyDictionary<string, object?>? initialValues = null)
    {
        InitializeComponent();
        WindowAppearance.ApplySharedIcon(this);
        _repository = repository;
        _tableName = tableName;
        _isEditMode = initialValues is not null;
        _initialValues = initialValues ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        ApplyLocalization();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await BuildFormAsync();
    }

    private async Task BuildFormAsync()
    {
        try
        {
            _columns = await _repository.GetInsertableColumnsAsync(_tableName);

            if (_columns.Count == 0)
            {
                MessageBox.Show(LanguageManager.Get("NoEditableColumns"), Title, MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = false;
                Close();
                return;
            }

            foreach (var column in _columns)
            {
                if (ShouldHideColumn(column))
                {
                    continue;
                }

                FieldsPanel.Children.Add(await CreateFieldCardAsync(column));
            }

            if (ShouldShowOrderProductsPicker())
            {
                FieldsPanel.Children.Add(await CreateOrderProductsCardAsync());
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, LanguageManager.Get("FormSetupError"), MessageBoxButton.OK, MessageBoxImage.Error);
            DialogResult = false;
            Close();
        }
    }

    private async Task<FrameworkElement> CreateFieldCardAsync(ColumnMetadata column)
    {
        var card = new Border
        {
            Background = (Brush)FindResource("SectionBrush"),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(18),
            Margin = new Thickness(0, 0, 0, 16)
        };

        var stack = new StackPanel();
        card.Child = stack;

        stack.Children.Add(new TextBlock
        {
            Text = column.DisplayName,
            FontWeight = FontWeights.SemiBold,
            FontSize = 15
        });

        var hintText = BuildHint(column);
        if (!string.IsNullOrWhiteSpace(hintText))
        {
            stack.Children.Add(new TextBlock
            {
                Margin = new Thickness(0, 6, 0, 12),
                Foreground = (Brush)FindResource("MutedTextBrush"),
                Text = hintText,
                TextWrapping = TextWrapping.Wrap
            });
        }

        FrameworkElement inputControl;

        if (column.HasLookup)
        {
            var comboBox = new ComboBox
            {
                MinHeight = 42,
                IsEditable = true,
                IsTextSearchEnabled = true,
                StaysOpenOnEdit = true,
                DisplayMemberPath = nameof(LookupOption.Label),
                SelectedValuePath = nameof(LookupOption.Value)
            };

            comboBox.ItemsSource = await _repository.GetLookupOptionsAsync(column.ReferencedTableName!, column.ReferencedColumnName!);
            if (_initialValues.TryGetValue(column.ColumnName, out var comboValue) && comboValue is not null and not DBNull)
            {
                var stringValue = Convert.ToString(comboValue, CultureInfo.InvariantCulture);
                comboBox.SelectedValue = stringValue;
                comboBox.Text = stringValue ?? string.Empty;
            }

            inputControl = comboBox;
        }
        else if (column.IsAddress)
        {
            var comboBox = new ComboBox
            {
                MinHeight = 42,
                IsEditable = true,
                IsTextSearchEnabled = true,
                StaysOpenOnEdit = true,
                DisplayMemberPath = nameof(LookupOption.Label),
                SelectedValuePath = nameof(LookupOption.Value)
            };

            comboBox.ItemsSource = await _repository.GetAddressOptionsAsync();
            if (_initialValues.TryGetValue(column.ColumnName, out var addressValue) && addressValue is not null and not DBNull)
            {
                comboBox.Text = Convert.ToString(addressValue, CultureInfo.InvariantCulture) ?? string.Empty;
            }

            inputControl = comboBox;
        }
        else if (column.IsColor)
        {
            var comboBox = new ComboBox
            {
                MinHeight = 42,
                IsEditable = true,
                IsTextSearchEnabled = true,
                StaysOpenOnEdit = true,
                DisplayMemberPath = nameof(LookupOption.Label),
                SelectedValuePath = nameof(LookupOption.Value)
            };

            comboBox.ItemsSource = await _repository.GetColorOptionsAsync();
            if (_initialValues.TryGetValue(column.ColumnName, out var colorValue) && colorValue is not null and not DBNull)
            {
                comboBox.Text = Convert.ToString(colorValue, CultureInfo.InvariantCulture) ?? string.Empty;
            }

            inputControl = comboBox;
        }
        else if (column.IsImagePath)
        {
            inputControl = CreateImagePicker(column);
        }
        else if (column.IsOrderStatus)
        {
            var comboBox = new ComboBox
            {
                MinHeight = 42,
                DisplayMemberPath = nameof(LookupOption.Label),
                SelectedValuePath = nameof(LookupOption.Value),
                ItemsSource = OrderStatusOptions
            };

            if (_initialValues.TryGetValue(column.ColumnName, out var statusValue) && statusValue is not null and not DBNull)
            {
                comboBox.SelectedValue = Convert.ToString(statusValue, CultureInfo.InvariantCulture);
            }

            inputControl = comboBox;
        }
        else if (column.IsDate)
        {
            var datePicker = new DatePicker
            {
                SelectedDate = DateTime.Today
            };

            if (_initialValues.TryGetValue(column.ColumnName, out var dateValue) && dateValue is DateTime dateTime)
            {
                datePicker.SelectedDate = dateTime;
            }
            else if (_initialValues.TryGetValue(column.ColumnName, out dateValue) && dateValue is not null and not DBNull)
            {
                datePicker.SelectedDate = Convert.ToDateTime(dateValue, CultureInfo.InvariantCulture);
            }

            inputControl = datePicker;
        }
        else if (IsCustomerFullNameColumn(column))
        {
            inputControl = CreateCustomerFullNameInput(column);
        }
        else
        {
            var textBox = new TextBox
            {
                MinHeight = 42
            };

            if (_initialValues.TryGetValue(column.ColumnName, out var textValue) && textValue is not null and not DBNull)
            {
                textBox.Text = column.IsPhone
                    ? FormatPhoneDisplay(Convert.ToString(textValue, CultureInfo.InvariantCulture))
                    : Convert.ToString(textValue, CultureInfo.InvariantCulture) ?? string.Empty;
            }
            else if (column.IsPhone)
            {
                textBox.Text = FormatPhoneDisplay(null);
                textBox.CaretIndex = textBox.Text.Length;
            }

            if (column.IsPhone)
            {
                textBox.TextChanged += PhoneTextBox_TextChanged;
                textBox.PreviewTextInput += PhoneTextBox_PreviewTextInput;
                textBox.PreviewKeyDown += PhoneTextBox_PreviewKeyDown;
                DataObject.AddPastingHandler(textBox, PhoneTextBox_Pasting);
            }
            else if (ShouldNormalizeCase(column))
            {
                AttachTitleCaseNormalization(textBox);
            }

            inputControl = textBox;
        }

        if (!column.IsImagePath)
        {
            _inputs[column] = inputControl;
        }

        stack.Children.Add(inputControl);

        return card;
    }

    private static string BuildHint(ColumnMetadata column)
    {
        if (column.IsPhone)
        {
            return LanguageManager.Get("PhoneHint");
        }

        if (column.IsOrderStatus)
        {
            return LanguageManager.CurrentLanguage == AppLanguage.Russian
                ? "Выберите один из трех допустимых статусов заказа."
                : "Choose one of the three allowed order statuses.";
        }

        if (column.IsColor)
        {
            return LanguageManager.CurrentLanguage == AppLanguage.Russian
                ? "Начните вводить цвет и выберите подходящий вариант из списка."
                : "Start typing a color and choose a matching option from the list.";
        }

        if (column.IsImagePath)
        {
            return string.Empty;
        }

        if (column.HasLookup)
        {
            return LanguageManager.Get("LookupHint");
        }

        if (column.IsAddress)
        {
            return LanguageManager.CurrentLanguage == AppLanguage.Russian
                ? "Начните вводить адрес и выберите подходящий вариант из списка."
                : "Start typing an address and choose a matching option from the list.";
        }

        if (column.IsNumeric)
        {
            return LanguageManager.Get("NumericHint");
        }

        if (column.IsDate)
        {
            return LanguageManager.Get("DateHint");
        }

        if (IsCustomerFullNameColumn(column))
        {
            return LanguageManager.CurrentLanguage == AppLanguage.Russian
                ? "Введите имя и фамилию отдельно. В базе они будут сохранены как одно поле."
                : "Enter the first name and last name separately. They will be stored in the database as one field.";
        }

        return column.IsNullable ? LanguageManager.Get("OptionalField") : LanguageManager.Get("RequiredField");
    }

    private async void AddButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var values = await CollectValuesAsync();
            if (_isEditMode)
            {
                await _repository.UpdateRecordAsync(_tableName, values);
                MessageBox.Show(LanguageManager.Get("RecordUpdated"), LanguageManager.Get("EditRecord"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else if (ShouldShowOrderProductsPicker())
            {
                var selectedItems = _orderProductSelections
                    .Where(item => item.IsSelected)
                    .Select(item => (item.ProductId, item.Quantity))
                    .ToList();

                await _repository.InsertCustomerOrderWithProductsAsync(values, selectedItems);
                MessageBox.Show(LanguageManager.Get("RecordAdded"), LanguageManager.Get("AddRecord"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                await _repository.InsertRecordAsync(_tableName, values);
                MessageBox.Show(LanguageManager.Get("RecordAdded"), LanguageManager.Get("AddRecord"), MessageBoxButton.OK, MessageBoxImage.Information);
            }

            DialogResult = true;
            Close();
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, _isEditMode ? LanguageManager.Get("UpdateError") : LanguageManager.Get("InsertError"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task<Dictionary<string, object?>> CollectValuesAsync()
    {
        var values = new Dictionary<string, object?>();

        foreach (var column in _columns)
        {
            if (_isEditMode && IsIdentifierColumn(column))
            {
                values[column.ColumnName] = _initialValues[column.ColumnName];
                continue;
            }

            if (!_isEditMode && IsGeneratedIdentifier(column))
            {
                values[column.ColumnName] = await _repository.GetNextIdentifierValueAsync(_tableName, column.ColumnName);
                continue;
            }

            if (column.IsComputedOrderTotal)
            {
                values[column.ColumnName] = _isEditMode &&
                    _initialValues.TryGetValue(column.ColumnName, out var existingTotal)
                        ? existingTotal
                        : 0m;
                continue;
            }

            if (column.IsImagePath)
            {
                values[column.ColumnName] = await PrepareImageValueAsync(column);
                continue;
            }

            values[column.ColumnName] = ReadValue(column, _inputs[column]);
        }

        var addressColumn = _columns.FirstOrDefault(column => column.IsAddress);
        if (addressColumn is not null &&
            values.TryGetValue(addressColumn.ColumnName, out var addressValue) &&
            addressValue is string addressText &&
            !string.IsNullOrWhiteSpace(addressText))
        {
            await _repository.EnsureAddressExistsAsync(addressText);
        }

        var colorColumn = _columns.FirstOrDefault(column => column.IsColor);
        if (colorColumn is not null &&
            values.TryGetValue(colorColumn.ColumnName, out var colorValue) &&
            colorValue is string colorText &&
            !string.IsNullOrWhiteSpace(colorText))
        {
            await _repository.EnsureColorExistsAsync(colorText);
        }

        return values;
    }

    private FrameworkElement CreateImagePicker(ColumnMetadata column)
    {
        var wrapper = new StackPanel();

        var previewBorder = new Border
        {
            Height = 260,
            Margin = new Thickness(0, 0, 0, 12),
            CornerRadius = new CornerRadius(10),
            BorderBrush = (Brush)FindResource("LineBrush"),
            BorderThickness = new Thickness(1),
            Background = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var previewImage = new Image
        {
            Stretch = Stretch.UniformToFill
        };
        previewBorder.Child = previewImage;
        wrapper.Children.Add(previewBorder);

        var pathTextBox = new TextBox
        {
            MinHeight = 42,
            IsReadOnly = true
        };

        var storedPath = _initialValues.TryGetValue(column.ColumnName, out var initialPathValue) &&
                         initialPathValue is not null and not DBNull
            ? Convert.ToString(initialPathValue, CultureInfo.InvariantCulture)
            : string.Empty;
        pathTextBox.Text = storedPath ?? string.Empty;

        _inputs[column] = pathTextBox;
        _imagePreviews[column.ColumnName] = previewImage;

        UpdateImagePreview(column.ColumnName, ProductImageStorage.ResolveImageAbsolutePath(storedPath));
        wrapper.Children.Add(pathTextBox);

        var buttonPanel = new StackPanel
        {
            Margin = new Thickness(0, 12, 0, 0),
            Orientation = Orientation.Horizontal
        };

        var browseButton = new Button
        {
            MinWidth = 140,
            Margin = new Thickness(0, 0, 12, 0),
            Padding = new Thickness(14, 10, 14, 10),
            Content = LanguageManager.Get("ChooseImage")
        };
        browseButton.Click += (_, _) => SelectImage(column);

        var clearButton = new Button
        {
            MinWidth = 100,
            Padding = new Thickness(14, 10, 14, 10),
            Content = LanguageManager.Get("ClearImage")
        };
        clearButton.Click += (_, _) => ClearImage(column);

        buttonPanel.Children.Add(browseButton);
        buttonPanel.Children.Add(clearButton);
        wrapper.Children.Add(buttonPanel);

        return wrapper;
    }

    private void SelectImage(ColumnMetadata column)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.webp",
            Title = LanguageManager.Get("ChooseImage")
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _selectedImageSourcePaths[column.ColumnName] = dialog.FileName;
        if (_inputs[column] is TextBox textBox)
        {
            textBox.Text = Path.GetFileName(dialog.FileName);
        }

        UpdateImagePreview(column.ColumnName, dialog.FileName);
    }

    private void ClearImage(ColumnMetadata column)
    {
        _selectedImageSourcePaths.Remove(column.ColumnName);

        if (_inputs[column] is TextBox textBox)
        {
            textBox.Text = string.Empty;
        }

        UpdateImagePreview(column.ColumnName, null);
    }

    private void UpdateImagePreview(string columnName, string? filePath)
    {
        if (!_imagePreviews.TryGetValue(columnName, out var previewImage))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            previewImage.Source = null;
            return;
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        previewImage.Source = bitmap;
    }

    private async Task<object?> PrepareImageValueAsync(ColumnMetadata column)
    {
        if (_inputs[column] is not TextBox textBox)
        {
            return DBNull.Value;
        }

        if (_selectedImageSourcePaths.TryGetValue(column.ColumnName, out var sourcePath) &&
            !string.IsNullOrWhiteSpace(sourcePath))
        {
            var storedPath = await Task.Run(() => ProductImageStorage.ImportImage(sourcePath));
            textBox.Text = storedPath;
            _selectedImageSourcePaths.Remove(column.ColumnName);
            UpdateImagePreview(column.ColumnName, ProductImageStorage.ResolveImageAbsolutePath(storedPath));
            return storedPath;
        }

        var currentValue = textBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(currentValue))
        {
            return column.IsNullable ? DBNull.Value : throw new InvalidOperationException(LanguageManager.Format("FieldRequired", column.DisplayName));
        }

        return currentValue;
    }

    private static bool IsGeneratedIdentifier(ColumnMetadata column)
    {
        return column.IsIdentifier && !column.HasLookup;
    }

    private bool ShouldShowOrderProductsPicker()
    {
        return !_isEditMode && _tableName.Equals("CUSTOMER_ORDER", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<FrameworkElement> CreateOrderProductsCardAsync()
    {
        var options = await _repository.GetLookupOptionsAsync("PRODUCTT", "product_id");
        _orderProductSelections.Clear();
        _orderProductRowMap.Clear();

        foreach (var option in options)
        {
            if (!int.TryParse(option.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var productId))
            {
                continue;
            }

            _orderProductSelections.Add(new OrderProductSelection
            {
                ProductId = productId,
                ProductLabel = option.Label,
                Quantity = 1
            });
        }

        var card = new Border
        {
            Background = (Brush)FindResource("SectionBrush"),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(18),
            Margin = new Thickness(0, 0, 0, 16)
        };

        var stack = new StackPanel();
        card.Child = stack;

        stack.Children.Add(new TextBlock
        {
            Text = LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Товары в заказе" : "Products in the order",
            FontWeight = FontWeights.SemiBold,
            FontSize = 15
        });

        stack.Children.Add(new TextBlock
        {
            Margin = new Thickness(0, 6, 0, 12),
            Foreground = (Brush)FindResource("MutedTextBrush"),
            TextWrapping = TextWrapping.Wrap,
            Text = LanguageManager.CurrentLanguage == AppLanguage.Russian
                ? "Отметьте сразу несколько товаров для этого заказа и при необходимости укажите количество."
                : "Select multiple products for this order and adjust quantity if needed."
        });

        _orderProductsSearchTextBox = new TextBox
        {
            MinHeight = 42
        };
        _orderProductsSearchTextBox.TextChanged += OrderProductsSearchTextBox_TextChanged;
        stack.Children.Add(_orderProductsSearchTextBox);

        var listBorder = new Border
        {
            Margin = new Thickness(0, 12, 0, 0),
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("LineBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8)
        };

        var scrollViewer = new ScrollViewer
        {
            Height = 220,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var rowsPanel = new StackPanel();
        scrollViewer.Content = rowsPanel;
        listBorder.Child = scrollViewer;

        foreach (var selection in _orderProductSelections)
        {
            var row = CreateOrderProductRow(selection);
            _orderProductRowMap[selection] = row;
            rowsPanel.Children.Add(row);
        }

        stack.Children.Add(listBorder);

        return card;
    }

    private FrameworkElement CreateOrderProductRow(OrderProductSelection selection)
    {
        var rowBorder = new Border
        {
            Margin = new Thickness(0, 0, 0, 8),
            Padding = new Thickness(10),
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("LineBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        rowBorder.Child = grid;

        var checkBox = new CheckBox
        {
            VerticalAlignment = VerticalAlignment.Center,
            IsChecked = selection.IsSelected
        };
        checkBox.Checked += (_, _) => selection.IsSelected = true;
        checkBox.Unchecked += (_, _) => selection.IsSelected = false;
        grid.Children.Add(checkBox);

        var label = new TextBlock
        {
            Margin = new Thickness(12, 0, 16, 0),
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            FontWeight = FontWeights.SemiBold,
            Text = selection.ProductLabel
        };
        Grid.SetColumn(label, 1);
        grid.Children.Add(label);

        var quantityBox = new TextBox
        {
            Width = 58,
            MinHeight = 36,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Text = selection.Quantity.ToString(CultureInfo.InvariantCulture),
            IsEnabled = selection.IsSelected
        };
        quantityBox.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsDigit);
        quantityBox.TextChanged += (_, _) =>
        {
            if (int.TryParse(quantityBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity))
            {
                selection.Quantity = quantity;
            }
            else if (string.IsNullOrWhiteSpace(quantityBox.Text))
            {
                selection.Quantity = 1;
            }
        };
        selection.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(OrderProductSelection.IsSelected))
            {
                quantityBox.IsEnabled = selection.IsSelected;
                rowBorder.Background = selection.IsSelected
                    ? (Brush)FindResource("AccentSoftBrush")
                    : Brushes.White;
            }
        };

        Grid.SetColumn(quantityBox, 2);
        grid.Children.Add(quantityBox);

        return rowBorder;
    }

    private void OrderProductsSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = _orderProductsSearchTextBox?.Text?.Trim() ?? string.Empty;

        foreach (var pair in _orderProductRowMap)
        {
            var isVisible = string.IsNullOrWhiteSpace(query) ||
                            pair.Key.ProductLabel.Contains(query, StringComparison.CurrentCultureIgnoreCase);
            pair.Value.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private static bool IsIdentifierColumn(ColumnMetadata column)
    {
        return column.IsIdentifier && !column.HasLookup;
    }

    private Grid CreateCustomerFullNameInput(ColumnMetadata column)
    {
        var (firstName, lastName) = SplitCustomerFullName(
            _initialValues.TryGetValue(column.ColumnName, out var fullNameValue) && fullNameValue is not null and not DBNull
                ? Convert.ToString(fullNameValue, CultureInfo.InvariantCulture)
                : null);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        var firstNamePanel = new StackPanel
        {
            Margin = new Thickness(0, 0, 8, 0)
        };
        firstNamePanel.Children.Add(new TextBlock
        {
            Margin = new Thickness(0, 0, 0, 6),
            Foreground = (Brush)FindResource("MutedTextBrush"),
            Text = LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Имя" : "First name"
        });
        var firstNameTextBox = new TextBox
        {
            MinHeight = 42,
            Text = firstName
        };
        AttachTitleCaseNormalization(firstNameTextBox);
        firstNamePanel.Children.Add(firstNameTextBox);

        var lastNamePanel = new StackPanel
        {
            Margin = new Thickness(8, 0, 0, 0)
        };
        lastNamePanel.Children.Add(new TextBlock
        {
            Margin = new Thickness(0, 0, 0, 6),
            Foreground = (Brush)FindResource("MutedTextBrush"),
            Text = LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Фамилия" : "Last name"
        });
        var lastNameTextBox = new TextBox
        {
            MinHeight = 42,
            Text = lastName
        };
        AttachTitleCaseNormalization(lastNameTextBox);
        lastNamePanel.Children.Add(lastNameTextBox);

        Grid.SetColumn(firstNamePanel, 0);
        Grid.SetColumn(lastNamePanel, 1);
        grid.Children.Add(firstNamePanel);
        grid.Children.Add(lastNamePanel);

        return grid;
    }

    private bool ShouldHideColumn(ColumnMetadata column)
    {
        if (column.IsComputedOrderTotal)
        {
            return true;
        }

        return _isEditMode
            ? IsIdentifierColumn(column)
            : IsGeneratedIdentifier(column);
    }

    private object? ReadValue(ColumnMetadata column, FrameworkElement control)
    {
        if (IsCustomerFullNameColumn(column) && control is Grid fullNameGrid)
        {
            return ReadCustomerFullNameValue(column, fullNameGrid);
        }

        if (control is ComboBox comboBox)
        {
            var selectedValue = column.IsAddress || column.IsColor
                ? comboBox.Text?.Trim()
                : comboBox.SelectedValue?.ToString();

            if (string.IsNullOrWhiteSpace(selectedValue) && comboBox.IsEditable)
            {
                selectedValue = comboBox.Text?.Trim();
            }

            if (string.IsNullOrWhiteSpace(selectedValue))
            {
                if (!column.IsNullable)
                {
                    throw new InvalidOperationException(LanguageManager.Format("FieldRequired", column.DisplayName));
                }

                return DBNull.Value;
            }

            return ParseTypedValue(column, selectedValue);
        }

        if (control is DatePicker datePicker)
        {
            if (datePicker.SelectedDate is null)
            {
                if (!column.IsNullable)
                {
                    throw new InvalidOperationException(LanguageManager.Format("FieldRequired", column.DisplayName));
                }

                return DBNull.Value;
            }

            return datePicker.SelectedDate.Value;
        }

        if (control is TextBox textBox)
        {
            var text = textBox.Text.Trim();
            if (column.IsPhone)
            {
                var phoneDigits = ExtractPhoneDigits(text);
                if (!PhoneRegex.IsMatch(phoneDigits))
                {
                    throw new InvalidOperationException(LanguageManager.Format("PhoneValidation", column.DisplayName));
                }

                return phoneDigits;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                if (!column.IsNullable)
                {
                    throw new InvalidOperationException(LanguageManager.Format("FieldCannotBeEmpty", column.DisplayName));
                }

                return DBNull.Value;
            }

            return ParseTypedValue(column, text);
        }

        return DBNull.Value;
    }

    private static object ParseTypedValue(ColumnMetadata column, string rawValue)
    {
        if (!column.IsNumeric)
        {
            return NormalizeTextValue(column, rawValue);
        }

        if (column.IsWholeNumber)
        {
            if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integerValue))
            {
                return integerValue;
            }

            throw new InvalidOperationException(LanguageManager.Format("WholeNumberValidation", column.DisplayName));
        }

        if (decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue))
        {
            return decimalValue;
        }

        throw new InvalidOperationException(LanguageManager.Format("NumericValidation", column.DisplayName));
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ApplyLocalization()
    {
        var tableName = _repository.GetFriendlyTableName(_tableName);

        Title = _isEditMode ? LanguageManager.Get("EditFormTitle") : LanguageManager.Get("AddFormTitle");
        TitleTextBlock.Text = _isEditMode
            ? LanguageManager.Format("EditFormHeader", tableName)
            : LanguageManager.Format("AddFormHeader", tableName);
        SubtitleTextBlock.Text = _isEditMode
            ? LanguageManager.Get("EditFormSubtitle")
            : LanguageManager.Get("AddFormSubtitle");
        HelpTextBlock.Text = string.Empty;
        CancelButton.Content = LanguageManager.Get("Cancel");
        SubmitButton.Content = _isEditMode ? LanguageManager.Get("EditRecord") : LanguageManager.Get("AddRecord");
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

    private void UpdatePhoneTextBox(TextBox textBox, string digits)
    {
        _isUpdatingPhoneText = true;
        textBox.Text = FormatPhoneDisplay(digits);
        textBox.CaretIndex = textBox.Text.Length;
        _isUpdatingPhoneText = false;
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

    private static string NormalizeTextValue(ColumnMetadata column, string rawValue)
    {
        var trimmed = rawValue.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return trimmed;
        }

        if (!ShouldNormalizeCase(column))
        {
            return trimmed;
        }

        return ToTitleCase(trimmed);
    }

    private void AttachTitleCaseNormalization(TextBox textBox)
    {
        textBox.TextChanged += TitleCaseTextBox_TextChanged;
    }

    private void TitleCaseTextBox_TextChanged(object sender, TextChangedEventArgs e)
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

    private static bool ShouldNormalizeCase(ColumnMetadata column)
    {
        return column.ColumnName.Contains("fullname", StringComparison.OrdinalIgnoreCase) ||
               column.ColumnName.Contains("name", StringComparison.OrdinalIgnoreCase) ||
               column.ColumnName.Contains("title", StringComparison.OrdinalIgnoreCase) ||
               column.ColumnName.Contains("color", StringComparison.OrdinalIgnoreCase) ||
               column.ColumnName.Contains("fabric_type", StringComparison.OrdinalIgnoreCase);
    }

    private static string ToTitleCase(string value)
    {
        var lowered = value.ToLower(CultureInfo.CurrentCulture);
        var textInfo = CultureInfo.CurrentCulture.TextInfo;
        var words = lowered.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => string.Join('-', word
                .Split('-', StringSplitOptions.RemoveEmptyEntries)
                .Select(textInfo.ToTitleCase)));

        return string.Join(' ', words);
    }

    private static string NormalizeDisplayText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var lowered = value.ToLower(CultureInfo.CurrentCulture);
        var characters = lowered.ToCharArray();
        var capitalizeNext = true;

        for (var i = 0; i < characters.Length; i++)
        {
            if (char.IsLetter(characters[i]))
            {
                characters[i] = capitalizeNext
                    ? char.ToUpper(characters[i], CultureInfo.CurrentCulture)
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

    private static bool IsCustomerFullNameColumn(ColumnMetadata column)
    {
        return column.ColumnName.Equals("c_fullname", StringComparison.OrdinalIgnoreCase);
    }

    private object? ReadCustomerFullNameValue(ColumnMetadata column, Grid fullNameGrid)
    {
        if (fullNameGrid.Children.Count < 2 ||
            fullNameGrid.Children[0] is not StackPanel firstNamePanel ||
            fullNameGrid.Children[1] is not StackPanel lastNamePanel ||
            firstNamePanel.Children.OfType<TextBox>().FirstOrDefault() is not TextBox firstNameTextBox ||
            lastNamePanel.Children.OfType<TextBox>().FirstOrDefault() is not TextBox lastNameTextBox)
        {
            throw new InvalidOperationException("The customer name fields could not be read.");
        }

        var firstName = firstNameTextBox.Text.Trim();
        var lastName = lastNameTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName))
        {
            if (!column.IsNullable)
            {
                throw new InvalidOperationException(LanguageManager.Format("FieldCannotBeEmpty", column.DisplayName));
            }

            return DBNull.Value;
        }

        if (string.IsNullOrWhiteSpace(firstName))
        {
            throw new InvalidOperationException(LanguageManager.CurrentLanguage == AppLanguage.Russian
                ? "Введите имя клиента."
                : "Enter the customer's first name.");
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            throw new InvalidOperationException(LanguageManager.CurrentLanguage == AppLanguage.Russian
                ? "Введите фамилию клиента."
                : "Enter the customer's last name.");
        }

        if (!NameRegex.IsMatch(firstName))
        {
            throw new InvalidOperationException(LanguageManager.CurrentLanguage == AppLanguage.Russian
                ? "Имя должно содержать только буквы и быть не короче 2 символов."
                : "The first name must contain only letters and be at least 2 characters long.");
        }

        if (!NameRegex.IsMatch(lastName))
        {
            throw new InvalidOperationException(LanguageManager.CurrentLanguage == AppLanguage.Russian
                ? "Фамилия должна содержать только буквы и быть не короче 2 символов."
                : "The last name must contain only letters and be at least 2 characters long.");
        }

        var normalizedFirstName = NormalizeTextValue(column, firstName);
        var normalizedLastName = NormalizeTextValue(column, lastName);
        return $"{normalizedFirstName} {normalizedLastName}";
    }

    private static (string FirstName, string LastName) SplitCustomerFullName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return (string.Empty, string.Empty);
        }

        var parts = fullName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
        {
            return (string.Empty, string.Empty);
        }

        if (parts.Length == 1)
        {
            return (parts[0], string.Empty);
        }

        return (parts[0], string.Join(' ', parts.Skip(1)));
    }
}

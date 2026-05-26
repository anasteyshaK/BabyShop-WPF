using System.Globalization;

namespace BabyShop.Localization;

public enum AppLanguage
{
    Russian,
    English
}

public sealed record LanguageOption(AppLanguage Language, string Label);

public static class LanguageManager
{
    private static readonly IReadOnlyDictionary<string, string> AdditionalRussianTexts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["ReportAnalyticsHeroTitle"] = "Общий отчет магазина",
        ["ReportAnalyticsHeroSubtitle"] = "General Store Report",
        ["ReportPeriodLabel"] = "Период",
        ["ReportMetricHeaderIndicator"] = "Показатель",
        ["ReportMetricHeaderValue"] = "Значение",
        ["ReportMetricHeaderDescription"] = "Описание",
        ["ReportMetricTotalOrders"] = "Общее количество заказов",
        ["ReportMetricRevenue"] = "Общая сумма продаж",
        ["ReportMetricAverageOrder"] = "Средний чек",
        ["ReportMetricCustomers"] = "Количество клиентов",
        ["ReportMetricCompleted"] = "Завершённые заказы",
        ["ReportMetricShipped"] = "Отправленные заказы",
        ["ReportMetricPending"] = "Ожидающие заказы",
        ["ReportMetricPopularProduct"] = "Самый популярный товар",
        ["ReportMetricProfitableProduct"] = "Самый прибыльный товар",
        ["ReportMetricTotalOrdersNote"] = "Все заказы в системе",
        ["ReportMetricRevenueNote"] = "Сумма всех заказов за выбранный период",
        ["ReportMetricAverageOrderNote"] = "Средняя сумма одного заказа",
        ["ReportMetricCustomersNote"] = "Уникальные клиенты в выборке",
        ["ReportMetricCompletedNote"] = "Заказы со статусом Completed",
        ["ReportMetricShippedNote"] = "Заказы со статусом Shipped",
        ["ReportMetricPendingNote"] = "Заказы со статусом Pending",
        ["ReportMetricPopularProductNote"] = "Количество проданных единиц: {0}",
        ["ReportMetricProfitableProductNote"] = "Максимальная выручка по товару: {0}",
        ["ReportConclusionTitle"] = "Вывод",
        ["ReportAnalyticsConclusionText"] = "За выбранный период магазин получил {0} выручки. Завершено {1} заказов ({2}% от общего количества), а самым популярным товаром стал {3}.",
        ["ReportFooterGenerated"] = "Generated for Baby Shop",
        ["ReportFooterPage"] = "Page",
        ["ImageFieldHint"] = "\u0412\u044B\u0431\u0435\u0440\u0438\u0442\u0435 \u0444\u043E\u0442\u043E \u0442\u043E\u0432\u0430\u0440\u0430. \u0424\u0430\u0439\u043B \u0431\u0443\u0434\u0435\u0442 \u0441\u043E\u0445\u0440\u0430\u043D\u0451\u043D \u0432 Assets/images_pr, \u0430 \u0432 \u0431\u0430\u0437\u0435 \u043E\u0441\u0442\u0430\u043D\u0435\u0442\u0441\u044F \u0442\u043E\u043B\u044C\u043A\u043E \u043E\u0442\u043D\u043E\u0441\u0438\u0442\u0435\u043B\u044C\u043D\u044B\u0439 \u043F\u0443\u0442\u044C.",
        ["ChooseImage"] = "\u0412\u044B\u0431\u0440\u0430\u0442\u044C \u0444\u043E\u0442\u043E",
        ["ClearImage"] = "\u041E\u0447\u0438\u0441\u0442\u0438\u0442\u044C",
        ["OpenBackup"] = "Открыть резервное копирование",
        ["BackupWindowTitle"] = "Резервное копирование",
        ["BackupWindowSubtitle"] = "Создавайте backup, восстанавливайте базу и смотрите историю.",
        ["BackupCreate"] = "Создать backup",
        ["BackupRestore"] = "Восстановить из файла",
        ["BackupRefresh"] = "Обновить",
        ["BackupLastBackupTitle"] = "Последний backup",
        ["BackupLastBackupEmpty"] = "Резервных копий пока нет",
        ["BackupLastBackupUser"] = "Пользователь",
        ["BackupLastBackupFile"] = "Файл",
        ["BackupLastBackupPath"] = "Путь",
        ["BackupLastBackupSize"] = "Размер (КБ)",
        ["BackupLastBackupStatus"] = "Статус",
        ["BackupLastBackupMessage"] = "Сообщение",
        ["BackupLastBackupDate"] = "Дата",
        ["BackupHistoryTitle"] = "История backup / restore",
        ["BackupHistoryAccessDenied"] = "У пользователя нет права на просмотр истории резервного копирования.",
        ["BackupStatusIdle"] = "Окно резервного копирования готово к работе.",
        ["BackupStatusLoading"] = "Загрузка сведений о резервном копировании...",
        ["BackupSelectDestination"] = "Выберите путь для сохранения backup",
        ["BackupSelectSource"] = "Выберите SQL-файл для восстановления",
        ["BackupFileFilter"] = "SQL backup (*.sql)|*.sql",
        ["BackupRestoreWarningTitle"] = "Подтверждение восстановления",
        ["BackupRestoreWarning"] = "Восстановление заменит текущее состояние базы данных. Продолжить?",
        ["BackupSafetyPrompt"] = "Перед восстановлением создать защитный backup текущего состояния?",
        ["BackupSuccessTitle"] = "Операция завершена",
        ["BackupFailureTitle"] = "Ошибка backup / restore",
        ["BackupHistoryOperation"] = "Операция",
        ["BackupHistoryUser"] = "Пользователь",
        ["BackupHistoryFile"] = "Файл",
        ["BackupHistoryPath"] = "Путь",
        ["BackupHistorySize"] = "Размер",
        ["BackupHistoryDatabase"] = "База",
        ["BackupHistoryStatus"] = "Статус",
        ["BackupHistoryMessage"] = "Сообщение",
        ["BackupHistoryDate"] = "Дата",
        ["BackupUserRoleDefault"] = "Администратор"
    };

    private static readonly IReadOnlyDictionary<string, string> AdditionalEnglishTexts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["ReportAnalyticsHeroTitle"] = "General Store Report",
        ["ReportAnalyticsHeroSubtitle"] = "General Store Report",
        ["ReportPeriodLabel"] = "Period",
        ["ReportMetricHeaderIndicator"] = "Indicator",
        ["ReportMetricHeaderValue"] = "Value",
        ["ReportMetricHeaderDescription"] = "Description",
        ["ReportMetricTotalOrders"] = "Total Orders",
        ["ReportMetricRevenue"] = "Total Revenue",
        ["ReportMetricAverageOrder"] = "Average Order",
        ["ReportMetricCustomers"] = "Customers",
        ["ReportMetricCompleted"] = "Completed Orders",
        ["ReportMetricShipped"] = "Shipped Orders",
        ["ReportMetricPending"] = "Pending Orders",
        ["ReportMetricPopularProduct"] = "Most Popular Product",
        ["ReportMetricProfitableProduct"] = "Most Profitable Product",
        ["ReportMetricTotalOrdersNote"] = "All orders in the system",
        ["ReportMetricRevenueNote"] = "Revenue across the selected period",
        ["ReportMetricAverageOrderNote"] = "Average value of one order",
        ["ReportMetricCustomersNote"] = "Unique customers in the filtered range",
        ["ReportMetricCompletedNote"] = "Orders with Completed status",
        ["ReportMetricShippedNote"] = "Orders with Shipped status",
        ["ReportMetricPendingNote"] = "Orders with Pending status",
        ["ReportMetricPopularProductNote"] = "Units sold: {0}",
        ["ReportMetricProfitableProductNote"] = "Top revenue from a single product: {0}",
        ["ReportConclusionTitle"] = "Conclusion",
        ["ReportAnalyticsConclusionText"] = "During the selected period the store generated {0} in revenue. {1} orders were completed ({2}% of all orders), and the most popular product was {3}.",
        ["ReportFooterGenerated"] = "Generated for Baby Shop",
        ["ReportFooterPage"] = "Page",
        ["ImageFieldHint"] = "Choose a product image. The file will be stored in Assets/images_pr, while the database keeps only the relative path.",
        ["ChooseImage"] = "Choose Image",
        ["ClearImage"] = "Clear Image",
        ["OpenBackup"] = "Open backup tools",
        ["BackupWindowTitle"] = "Backup",
        ["BackupWindowSubtitle"] = "Create backups, restore the database, and view history.",
        ["BackupCreate"] = "Create backup",
        ["BackupRestore"] = "Restore from file",
        ["BackupRefresh"] = "Refresh",
        ["BackupLastBackupTitle"] = "Last backup",
        ["BackupLastBackupEmpty"] = "No backups have been created yet",
        ["BackupLastBackupUser"] = "User",
        ["BackupLastBackupFile"] = "File",
        ["BackupLastBackupPath"] = "Path",
        ["BackupLastBackupSize"] = "Size (KB)",
        ["BackupLastBackupStatus"] = "Status",
        ["BackupLastBackupMessage"] = "Message",
        ["BackupLastBackupDate"] = "Created at",
        ["BackupHistoryTitle"] = "Backup / restore history",
        ["BackupHistoryAccessDenied"] = "You do not have permission to view backup history.",
        ["BackupStatusIdle"] = "Backup tools are ready.",
        ["BackupStatusLoading"] = "Loading backup details...",
        ["BackupSelectDestination"] = "Choose where to save the backup",
        ["BackupSelectSource"] = "Choose an SQL file to restore",
        ["BackupFileFilter"] = "SQL backup (*.sql)|*.sql",
        ["BackupRestoreWarningTitle"] = "Confirm restore",
        ["BackupRestoreWarning"] = "Restore will overwrite the current database state. Continue?",
        ["BackupSafetyPrompt"] = "Create a safety backup before restore?",
        ["BackupSuccessTitle"] = "Operation completed",
        ["BackupFailureTitle"] = "Backup / restore failed",
        ["BackupHistoryOperation"] = "Operation",
        ["BackupHistoryUser"] = "User",
        ["BackupHistoryFile"] = "File",
        ["BackupHistoryPath"] = "Path",
        ["BackupHistorySize"] = "Size",
        ["BackupHistoryDatabase"] = "Database",
        ["BackupHistoryStatus"] = "Status",
        ["BackupHistoryMessage"] = "Message",
        ["BackupHistoryDate"] = "Date",
        ["BackupUserRoleDefault"] = "Administrator"
    };

    private static readonly IReadOnlyDictionary<string, string> RussianTexts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["AppTitle"] = "Baby Shop", ["BrandSubtitle"] = "Рабочее пространство данных", ["TablesLabel"] = "ТАБЛИЦЫ",
        ["Customers"] = "Клиенты", ["Fabrics"] = "Ткани", ["Products"] = "Товары", ["Orders"] = "Заказы", ["OrderItems"] = "Состав заказа",
        ["Dashboard"] = "Панель", ["DashboardSubtitle"] = "Анализ заказов, статусов и выручки на основе текущей базы данных.",
        ["DashboardFilters"] = "Фильтры", ["DashboardClient"] = "Клиент", ["DashboardStatus"] = "Статус", ["DashboardProduct"] = "Товар", ["DashboardFabricType"] = "Тип ткани",
        ["DashboardDateFrom"] = "Дата с", ["DashboardDateTo"] = "Дата по", ["DashboardAmountRange"] = "Диапазон суммы", ["DashboardMinAmount"] = "Минимум", ["DashboardMaxAmount"] = "Максимум",
        ["DashboardApply"] = "Применить фильтры", ["DashboardReset"] = "Сбросить", ["DashboardTotalSum"] = "Общая сумма", ["DashboardOrderCount"] = "Количество заказов",
        ["DashboardAverage"] = "Средний заказ", ["DashboardRange"] = "Мин / макс заказа", ["DashboardDataGrid"] = "Отфильтрованные данные",
        ["DashboardStatusChart"] = "Круговая диаграмма: статусы", ["DashboardTimelineChart"] = "Линейный график: динамика по датам", ["DashboardProductChart"] = "Товары по выручке",
        ["DashboardCategoryChart"] = "Столбчатая диаграмма: категории", ["DashboardMonthChart"] = "Гистограмма: сравнение по месяцам", ["DashboardLoading"] = "Загрузка панели...", ["DashboardRowsLoaded"] = "Загружено строк: {0}",
        ["DashboardDateRangeError"] = "Дата начала не может быть позже даты окончания.", ["DashboardAmountRangeError"] = "Минимальная сумма не может быть больше максимальной.",
        ["DashboardNoStatusData"] = "Нет данных для диаграммы статусов.", ["DashboardNoProductData"] = "Нет данных по товарам.", ["DashboardNoCategoryData"] = "Нет данных по категориям.",
        ["DashboardNoTimelineData"] = "Нет данных для динамики.", ["DashboardNoMonthData"] = "Нет месячных данных.",
        ["AllClients"] = "Все клиенты", ["AllStatuses"] = "Все статусы", ["AllProducts"] = "Все товары", ["AllFabricTypes"] = "Все типы тканей",
        ["StatusPending"] = "Pending - ожидает обработки", ["StatusShipped"] = "Shipped - отправлен", ["StatusCompleted"] = "Completed - завершен",
        ["DashboardOrdersShort"] = "Заказы", ["OrderStatusFilter"] = "Статус заказа", ["AddRecord"] = "Добавить запись", ["EditRecord"] = "Редактировать запись",
        ["DeleteRecord"] = "Удалить запись", ["Refresh"] = "Обновить", ["OpenDashboard"] = "Открыть панель", ["SearchPlaceholder"] = "Поиск по таблице...",
        ["Language"] = "Язык", ["ConnectedToMySql"] = "Подключено к MySQL", ["ConnectionFailed"] = "Ошибка подключения", ["Connecting"] = "Подключение...",
        ["NoDataLoaded"] = "Данные еще не загружены", ["DatabaseConnection"] = "Подключение к базе данных", ["LastRefreshed"] = "Последнее обновление: {0}",
        ["RecordsCount"] = "{0} записей", ["RowsLoaded"] = "{0}: загружено строк {1}.", ["AllTablesSynced"] = "Все отображаемые таблицы синхронизированы с текущей базой MySQL.",
        ["FooterDefault"] = "Подключите MySQL из XAMPP на localhost и настройте строку подключения в Configuration/DatabaseSettings.cs",
        ["LoadError"] = "Ошибка загрузки {0}", ["SelectRowToEdit"] = "Сначала выберите строку для редактирования.", ["SelectRowToDelete"] = "Сначала выберите строку для удаления.",
        ["DeleteConfirm"] = "Удалить выбранную запись?", ["CustomerDeleteBlocked"] = "Удаление клиентов запрещено.", ["CustomerDirectoryTitle"] = "Справочник клиентов", ["CustomerDirectorySubtitle"] = "Клиенты и контакты",
        ["FabricInventoryTitle"] = "Каталог тканей", ["FabricInventorySubtitle"] = "Все ткани с цветами и стоимостью", ["ProductCatalogueTitle"] = "Каталог товаров",
        ["ProductCatalogueSubtitle"] = "Товары, ткани и стоимость материалов", ["CustomerOrdersTitle"] = "Заказы клиентов",
        ["CustomerOrdersSubtitle"] = "Заказы с адресом доставки, датами и статусом", ["OrderItemsTitle"] = "Состав заказа", ["OrderItemsSubtitle"] = "Товары, входящие в выбранные заказы",
        ["AddFormTitle"] = "Добавление записи", ["EditFormTitle"] = "Редактирование записи", ["AddFormHeader"] = "Добавить в {0}", ["EditFormHeader"] = "Редактировать {0}",
        ["AddFormSubtitle"] = "Заполните поля ниже, чтобы добавить новую запись.", ["EditFormSubtitle"] = "Измените поля ниже, чтобы обновить выбранную запись.",
        ["RequiredFieldsHelp"] = "Обязательные поля нельзя оставлять пустыми. После +373 номер телефона должен содержать ровно 8 цифр.",
        ["Cancel"] = "Отмена", ["NoEditableColumns"] = "Для этой таблицы не найдено редактируемых полей.", ["FormSetupError"] = "Ошибка подготовки формы",
        ["PhoneHint"] = "Введите 8 цифр после +373.", ["NumericHint"] = "Введите только числовое значение.", ["DateHint"] = "Выберите корректную дату.",
        ["LookupHint"] = "Выберите существующее значение из списка.", ["OptionalField"] = "Необязательное поле.", ["RequiredField"] = "Обязательное поле.",
        ["RecordUpdated"] = "Запись успешно обновлена.", ["RecordAdded"] = "Новая запись успешно добавлена.", ["UpdateError"] = "Ошибка обновления", ["InsertError"] = "Ошибка добавления",
        ["FieldRequired"] = "Поле \"{0}\" обязательно для заполнения.", ["FieldCannotBeEmpty"] = "Поле \"{0}\" не может быть пустым.",
        ["PhoneValidation"] = "Поле \"{0}\" должно содержать ровно 8 цифр после +373.", ["WholeNumberValidation"] = "Поле \"{0}\" должно быть целым числом.",
        ["NumericValidation"] = "Поле \"{0}\" должно быть числом.", ["CouldNotLoadData"] = "Не удалось загрузить данные из {0}. {1}",
        ["CouldNotReadSchema"] = "Не удалось прочитать структуру {0}. {1}", ["CouldNotLoadLookup"] = "Не удалось загрузить значения из {0}. {1}",
        ["CouldNotGenerateIdentifier"] = "Не удалось сгенерировать следующий идентификатор для {0}. {1}",
        ["NoInsertProcedure"] = "Для {0} не настроена процедура добавления.", ["NoUpdateProcedure"] = "Для {0} не настроена процедура обновления.",
        ["NoDeleteProcedure"] = "Для {0} не настроена процедура удаления.", ["NoDeleteIdentifier"] = "Для {0} не настроен идентификатор удаления.",
        ["CouldNotLoadSelectedRecord"] = "Не удалось загрузить выбранную запись из {0}. {1}", ["MissingSourceValue"] = "Выбранная строка не содержит исходное значение \"{0}\".",
        ["OpenReports"] = "Открыть отчеты", ["ReportsMenuTitle"] = "Отчеты", ["ReportMenuAllRecords"] = "Список всех записей",
        ["ReportMenuAllRecordsNote"] = "Полный список по выбранной таблице.", ["ReportMenuFiltered"] = "Отчет с фильтром",
        ["ReportMenuFilteredNote"] = "Данные по периоду, клиенту, статусу и сумме.", ["ReportMenuAnalytics"] = "Итоговый отчет",
        ["ReportMenuAnalyticsNote"] = "Сводка и аналитика по заказам, статусам и выручке.", ["ReportFilterWindowTitle"] = "Параметры отчета",
        ["ReportFilterAllSubtitle"] = "Выберите таблицу и подготовьте полный отчет по данным.", ["ReportFilterFilteredSubtitle"] = "Уточните фильтры и соберите выборку для отчета.",
        ["ReportFilterAnalyticsSubtitle"] = "Настройте период и параметры для аналитического отчета.", ["ReportFilterHint"] = "Фильтры применяются к данным панели и отчетам одновременно.",
                ["ReportFilterFooter"] = "Сначала задайте параметры, затем сформируйте отчет и используйте кнопки печати или экспорта ниже.", ["ReportGenerate"] = "Сформировать отчет",
        ["ReportLoadingFilters"] = "Загрузка фильтров отчета...", ["ReportFiltersReady"] = "Фильтры готовы к использованию.", ["ReportViewerWindowTitle"] = "Просмотр отчета",
        ["ReportPrint"] = "Печать", ["ReportExportPdf"] = "Экспорт в PDF", ["ReportOpenBrowser"] = "Открыть в браузере",
        ["ReportStatusIdle"] = "Отчет еще не сформирован.", ["ReportStatusLoading"] = "Формирование отчета...", ["ReportStatusReady"] = "Отчет готов.",
        ["ReportStatusPreparingBrowser"] = "Подготовка HTML-версии отчета...", ["ReportBrowserOpened"] = "Отчет открыт в браузере.",
        ["ReportStatusExportingPdf"] = "Экспорт PDF...", ["ReportPdfExported"] = "PDF успешно создан.", ["ReportPrinted"] = "Отчет отправлен на печать.",
        ["ReportNotReady"] = "Отчет еще не готов.", ["ReportEdgeMissing"] = "Microsoft Edge не найден. Для экспорта в PDF нужен установленный Edge.",
        ["ReportPdfExportFailed"] = "Не удалось экспортировать отчет в PDF.", ["ReportSectionSummary"] = "Сводка",
        ["ReportSectionFilters"] = "Примененные фильтры", ["ReportSectionRecords"] = "Данные отчета", ["ReportSectionInsights"] = "Ключевые выводы",
        ["ReportSourceTable"] = "Источник данных", ["ReportRowsCount"] = "Количество строк", ["ReportGeneratedAt"] = "Сформирован",
        ["ReportGeneratedAtLine"] = "Дата формирования: {0}", ["ReportFiltersLabel"] = "Фильтры", ["ReportFilterNone"] = "Не задано",
        ["ReportNoData"] = "Нет данных для отображения.", ["ReportStatusColumn"] = "Статус", ["ReportCategoryColumn"] = "Категория",
        ["ReportQuantityColumn"] = "Количество", ["ReportDateColumn"] = "Дата", ["ReportMonthColumn"] = "Месяц",
        ["ReportInsightTopStatus"] = "Наиболее частый статус: {0} ({1}).", ["ReportInsightTopCategory"] = "Лидирующая категория по выручке: {0} ({1}).",
        ["ReportInsightTopMonth"] = "Самый сильный месяц по выручке: {0} ({1}).", ["ReportAllRecordsTitle"] = "Список всех записей",
        ["ReportAllRecordsSubtitle"] = "Полная выгрузка по таблице: {0}.", ["ReportFilteredTitle"] = "Отчет с фильтром",
        ["ReportFilteredSubtitle"] = "Отфильтрованные записи и ключевые показатели.", ["ReportAnalyticsTitle"] = "Итоговый аналитический отчет",
        ["ReportAnalyticsSubtitle"] = "Сводные показатели, тренды и распределения по заказам."
    };

    private static readonly IReadOnlyDictionary<string, string> EnglishTexts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["AppTitle"] = "Baby Shop", ["BrandSubtitle"] = "Data workspace", ["TablesLabel"] = "TABLES",
        ["Customers"] = "Customers", ["Fabrics"] = "Fabrics", ["Products"] = "Products", ["Orders"] = "Orders", ["OrderItems"] = "Order Items",
        ["Dashboard"] = "Dashboard", ["DashboardSubtitle"] = "Order, status, and revenue analytics based on your current database.",
        ["DashboardFilters"] = "Filters", ["DashboardClient"] = "Client", ["DashboardStatus"] = "Status", ["DashboardProduct"] = "Product", ["DashboardFabricType"] = "Fabric Type",
        ["DashboardDateFrom"] = "From Date", ["DashboardDateTo"] = "To Date", ["DashboardAmountRange"] = "Amount Range", ["DashboardMinAmount"] = "Minimum", ["DashboardMaxAmount"] = "Maximum",
        ["DashboardApply"] = "Apply Filters", ["DashboardReset"] = "Reset", ["DashboardTotalSum"] = "Total Sum", ["DashboardOrderCount"] = "Orders Count",
        ["DashboardAverage"] = "Average Order", ["DashboardRange"] = "Order Min / Max", ["DashboardDataGrid"] = "Filtered Records", ["DashboardStatusChart"] = "Pie Chart by Status",
        ["DashboardTimelineChart"] = "Line Chart by Dates", ["DashboardProductChart"] = "Products by Revenue", ["DashboardCategoryChart"] = "Bar Chart by Categories", ["DashboardMonthChart"] = "Monthly Histogram",
        ["DashboardLoading"] = "Loading dashboard...", ["DashboardRowsLoaded"] = "Rows loaded: {0}", ["DashboardDateRangeError"] = "The start date cannot be later than the end date.",
        ["DashboardAmountRangeError"] = "The minimum amount cannot be greater than the maximum amount.", ["DashboardNoStatusData"] = "No data available for the status chart.",
        ["DashboardNoProductData"] = "No product data is available.", ["DashboardNoCategoryData"] = "No category data is available.", ["DashboardNoTimelineData"] = "No timeline data is available.", ["DashboardNoMonthData"] = "No monthly data is available.",
        ["AllClients"] = "All Clients", ["AllStatuses"] = "All Statuses", ["AllProducts"] = "All Products", ["AllFabricTypes"] = "All Fabric Types",
        ["StatusPending"] = "Pending - awaiting processing", ["StatusShipped"] = "Shipped - sent to customer", ["StatusCompleted"] = "Completed - finished",
        ["DashboardOrdersShort"] = "Orders", ["OrderStatusFilter"] = "Order Status", ["AddRecord"] = "Add Record", ["EditRecord"] = "Edit Record", ["DeleteRecord"] = "Delete Record",
        ["Refresh"] = "Refresh", ["OpenDashboard"] = "Open Dashboard", ["SearchPlaceholder"] = "Search table...", ["Language"] = "Language",
        ["ConnectedToMySql"] = "Connected to MySQL", ["ConnectionFailed"] = "Connection failed", ["Connecting"] = "Connecting...", ["NoDataLoaded"] = "No data loaded yet",
        ["DatabaseConnection"] = "Database Connection", ["LastRefreshed"] = "Last refreshed: {0}", ["RecordsCount"] = "{0} records", ["RowsLoaded"] = "{0}: {1} rows loaded.",
        ["AllTablesSynced"] = "All visible tables are synchronized with the current MySQL database.",
        ["FooterDefault"] = "Connect XAMPP MySQL on localhost and update the connection string in Configuration/DatabaseSettings.cs",
        ["LoadError"] = "{0} Load Error", ["SelectRowToEdit"] = "Select a row to edit first.", ["SelectRowToDelete"] = "Select a row to delete first.",
        ["DeleteConfirm"] = "Delete the selected record?", ["CustomerDeleteBlocked"] = "Deleting customers is not allowed.", ["CustomerDirectoryTitle"] = "Customer Directory", ["CustomerDirectorySubtitle"] = "Clients and contacts",
        ["FabricInventoryTitle"] = "Fabric Inventory", ["FabricInventorySubtitle"] = "All fabrics with colors and pricing", ["ProductCatalogueTitle"] = "Product Catalogue",
        ["ProductCatalogueSubtitle"] = "Products, fabrics, and material pricing", ["CustomerOrdersTitle"] = "Customer Orders",
        ["CustomerOrdersSubtitle"] = "Orders with delivery address, dates, and status", ["OrderItemsTitle"] = "Order Items", ["OrderItemsSubtitle"] = "Products included in the selected orders",
        ["AddFormTitle"] = "Add Record", ["EditFormTitle"] = "Edit Record", ["AddFormHeader"] = "Add to {0}", ["EditFormHeader"] = "Edit {0}",
        ["AddFormSubtitle"] = "Fill in the fields below to insert a new row.", ["EditFormSubtitle"] = "Update the fields below to edit the selected row.",
        ["RequiredFieldsHelp"] = "Required fields cannot be empty. Phone numbers must contain exactly 8 digits after +373.",
        ["Cancel"] = "Cancel", ["NoEditableColumns"] = "No editable columns were found for this table.", ["FormSetupError"] = "Form Setup Error",
        ["PhoneHint"] = "Enter 8 digits after +373.", ["NumericHint"] = "Enter a numeric value only.", ["DateHint"] = "Choose a valid date.",
        ["LookupHint"] = "Select an existing value from the list.", ["OptionalField"] = "Optional field.", ["RequiredField"] = "Required field.",
        ["RecordUpdated"] = "The record was updated successfully.", ["RecordAdded"] = "The new record was added successfully.", ["UpdateError"] = "Update Error", ["InsertError"] = "Insert Error",
        ["FieldRequired"] = "\"{0}\" is required.", ["FieldCannotBeEmpty"] = "\"{0}\" cannot be empty.",
        ["PhoneValidation"] = "\"{0}\" must contain exactly 8 digits after +373.", ["WholeNumberValidation"] = "\"{0}\" must be a whole number.", ["NumericValidation"] = "\"{0}\" must be numeric.",
        ["CouldNotLoadData"] = "Could not load data from {0}. {1}", ["CouldNotReadSchema"] = "Could not read schema information for {0}. {1}",
        ["CouldNotLoadLookup"] = "Could not load lookup values from {0}. {1}", ["CouldNotGenerateIdentifier"] = "Could not generate the next identifier for {0}. {1}",
        ["NoInsertProcedure"] = "No insert procedure is configured for {0}.", ["NoUpdateProcedure"] = "No update procedure is configured for {0}.",
        ["NoDeleteProcedure"] = "No delete procedure is configured for {0}.", ["NoDeleteIdentifier"] = "No delete identifier is configured for {0}.",
        ["CouldNotLoadSelectedRecord"] = "Could not load the selected record from {0}. {1}", ["MissingSourceValue"] = "The selected row does not contain the source value \"{0}\".",
        ["OpenReports"] = "Open reports", ["ReportsMenuTitle"] = "Reports", ["ReportMenuAllRecords"] = "All records list",
        ["ReportMenuAllRecordsNote"] = "Full export for the selected table.", ["ReportMenuFiltered"] = "Filtered report",
        ["ReportMenuFilteredNote"] = "Records by period, client, status, and amount.", ["ReportMenuAnalytics"] = "Analytics report",
        ["ReportMenuAnalyticsNote"] = "Summary and analytics for orders, statuses, and revenue.", ["ReportFilterWindowTitle"] = "Report Filters",
        ["ReportFilterAllSubtitle"] = "Choose a table and prepare a full report for its data.", ["ReportFilterFilteredSubtitle"] = "Refine the filters and build a focused report set.",
        ["ReportFilterAnalyticsSubtitle"] = "Set the period and parameters for the analytics report.", ["ReportFilterHint"] = "These filters are shared with the dashboard reporting logic.",
                ["ReportFilterFooter"] = "Set the parameters first, then generate the report and use the print or export actions below.", ["ReportGenerate"] = "Generate report",
        ["ReportLoadingFilters"] = "Loading report filters...", ["ReportFiltersReady"] = "Filters are ready.", ["ReportViewerWindowTitle"] = "Report Viewer",
        ["ReportPrint"] = "Print", ["ReportExportPdf"] = "Export PDF", ["ReportOpenBrowser"] = "Open in Browser",
        ["ReportStatusIdle"] = "The report has not been generated yet.", ["ReportStatusLoading"] = "Generating report...", ["ReportStatusReady"] = "Report is ready.",
        ["ReportStatusPreparingBrowser"] = "Preparing browser view...", ["ReportBrowserOpened"] = "The report was opened in your browser.",
        ["ReportStatusExportingPdf"] = "Exporting PDF...", ["ReportPdfExported"] = "PDF exported successfully.", ["ReportPrinted"] = "The report was sent to print.",
        ["ReportNotReady"] = "The report is not ready yet.", ["ReportEdgeMissing"] = "Microsoft Edge was not found. PDF export requires Edge.",
        ["ReportPdfExportFailed"] = "Could not export the report to PDF.", ["ReportSectionSummary"] = "Summary",
        ["ReportSectionFilters"] = "Applied Filters", ["ReportSectionRecords"] = "Report Data", ["ReportSectionInsights"] = "Key Insights",
        ["ReportSourceTable"] = "Source Table", ["ReportRowsCount"] = "Rows Count", ["ReportGeneratedAt"] = "Generated At",
        ["ReportGeneratedAtLine"] = "Generated on: {0}", ["ReportFiltersLabel"] = "Filters", ["ReportFilterNone"] = "Not set",
        ["ReportNoData"] = "No data to display.", ["ReportStatusColumn"] = "Status", ["ReportCategoryColumn"] = "Category",
        ["ReportQuantityColumn"] = "Quantity", ["ReportDateColumn"] = "Date", ["ReportMonthColumn"] = "Month",
        ["ReportInsightTopStatus"] = "Most frequent status: {0} ({1}).", ["ReportInsightTopCategory"] = "Top category by revenue: {0} ({1}).",
        ["ReportInsightTopMonth"] = "Strongest revenue month: {0} ({1}).", ["ReportAllRecordsTitle"] = "All Records Report",
        ["ReportAllRecordsSubtitle"] = "Complete export for table: {0}.", ["ReportFilteredTitle"] = "Filtered Report",
        ["ReportFilteredSubtitle"] = "Filtered records with the key metrics.", ["ReportAnalyticsTitle"] = "Analytics Summary Report",
        ["ReportAnalyticsSubtitle"] = "Summary metrics, trends, and order distributions."
    };

    private static readonly IReadOnlyDictionary<string, string> RussianTableNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["CUSTOMER"] = "клиенты", ["FABRIC"] = "ткани", ["CUSTOMERVIEW"] = "клиенты", ["FABRICVIEW"] = "ткани",
        ["PRODUCTT"] = "товары", ["CUSTOMER_ORDER"] = "заказы", ["ORDER_PRODUCT"] = "состав заказа",
        ["PRODUCTFABRICVIEW"] = "товары", ["CUSTOMERORDERSVIEW"] = "заказы", ["ORDERPRODUCTVIEW"] = "состав заказа"
    };

    private static readonly IReadOnlyDictionary<string, string> EnglishTableNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["CUSTOMER"] = "customers", ["FABRIC"] = "fabrics", ["CUSTOMERVIEW"] = "customers", ["FABRICVIEW"] = "fabrics",
        ["PRODUCTT"] = "products", ["CUSTOMER_ORDER"] = "orders", ["ORDER_PRODUCT"] = "order items",
        ["PRODUCTFABRICVIEW"] = "products", ["CUSTOMERORDERSVIEW"] = "orders", ["ORDERPRODUCTVIEW"] = "order items"
    };

    private static readonly IReadOnlyDictionary<string, string> RussianColumnNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["NO."] = "№", ["CUSTOMER_ID"] = "ID клиента", ["C_FULLNAME"] = "ФИО клиента", ["CUSTOMER_NAME"] = "Имя клиента",
        ["NAME"] = "Название", ["PHONE"] = "Телефон", ["PHONE_NUMBER"] = "Телефон", ["C_PHONE_NUMBER"] = "Номер телефона",
        ["EMAIL"] = "Эл. почта", ["FABRIC_ID"] = "ID ткани", ["FABRIC_NAME"] = "Название ткани", ["FABRIC_TYPE"] = "Тип ткани",
        ["PRICE_PER_M"] = "Цена за метр", ["COLOR"] = "Цвет", ["PRODUCT_ID"] = "ID товара", ["PRODUCT_NAME"] = "Название товара",
        ["PRODUCT_TITLE"] = "Название товара", ["FABRIC_AMOUNT"] = "Расход ткани (м)", ["PRICE"] = "Цена", ["QUANTITY"] = "Количество",
        ["PRODUCT_COUNT"] = "Количество товара", ["STOCK"] = "Остаток", ["STOCK_QUANTITY"] = "Количество на складе",
        ["ORDER_ID"] = "ID заказа", ["ORDER_PRODUCT_ID"] = "ID позиции заказа", ["ORDER_DATE"] = "Дата заказа",
        ["START_DATE"] = "Дата начала", ["END_DATE"] = "Дата окончания", ["DELIVERY_ADDRESS"] = "Адрес доставки",
        ["ORDER_STATUS"] = "Статус заказа", ["ORDER_ITEMS"] = "Состав заказа", ["TOTAL"] = "Сумма", ["TOTAL_PRICE"] = "Итоговая сумма",
        ["TOTAL_COST"] = "Сумма заказа", ["LINE_TOTAL"] = "Сумма позиции", ["TOTAL_SUM"] = "Общая сумма",
        ["ORDER_COUNT"] = "Количество заказов", ["AVG_VALUE"] = "Среднее значение", ["MIN_VALUE"] = "Минимум", ["MAX_VALUE"] = "Максимум",
        ["TOTAL_QUANTITY"] = "Общее количество", ["MONTH_LABEL"] = "Месяц", ["UNIT_PRICE"] = "Цена за единицу", ["AMOUNT"] = "Количество"
    };

    private static readonly IReadOnlyDictionary<string, string> EnglishColumnNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["NO."] = "No.", ["CUSTOMER_ID"] = "Customer ID", ["C_FULLNAME"] = "Customer Name", ["CUSTOMER_NAME"] = "Customer Name",
        ["NAME"] = "Name", ["PHONE"] = "Phone", ["PHONE_NUMBER"] = "Phone", ["C_PHONE_NUMBER"] = "Phone Number", ["EMAIL"] = "Email",
        ["FABRIC_ID"] = "Fabric ID", ["FABRIC_NAME"] = "Fabric Name", ["FABRIC_TYPE"] = "Fabric Type", ["PRICE_PER_M"] = "Price per Meter",
        ["COLOR"] = "Color", ["PRODUCT_ID"] = "Product ID", ["PRODUCT_NAME"] = "Product Name", ["PRODUCT_TITLE"] = "Product Name",
        ["FABRIC_AMOUNT"] = "Fabric Usage (m)", ["PRICE"] = "Price", ["QUANTITY"] = "Quantity", ["PRODUCT_COUNT"] = "Product Quantity",
        ["STOCK"] = "Stock", ["STOCK_QUANTITY"] = "Stock Quantity", ["ORDER_ID"] = "Order ID", ["ORDER_PRODUCT_ID"] = "Order Item ID",
        ["ORDER_DATE"] = "Order Date", ["START_DATE"] = "Start Date", ["END_DATE"] = "End Date", ["DELIVERY_ADDRESS"] = "Delivery Address",
        ["ORDER_STATUS"] = "Order Status", ["ORDER_ITEMS"] = "Order Items", ["TOTAL"] = "Total", ["TOTAL_PRICE"] = "Total Price",
        ["TOTAL_COST"] = "Order Total", ["LINE_TOTAL"] = "Line Total", ["TOTAL_SUM"] = "Total Sum", ["ORDER_COUNT"] = "Order Count",
        ["AVG_VALUE"] = "Average Value", ["MIN_VALUE"] = "Minimum", ["MAX_VALUE"] = "Maximum", ["TOTAL_QUANTITY"] = "Total Quantity",
        ["MONTH_LABEL"] = "Month", ["UNIT_PRICE"] = "Unit Price", ["AMOUNT"] = "Amount"
    };

    public static AppLanguage CurrentLanguage { get; private set; } = AppLanguage.Russian;

    public static event EventHandler? LanguageChanged;

    public static IReadOnlyList<LanguageOption> GetOptions()
    {
        return
        [
            new LanguageOption(AppLanguage.Russian, "Русский"),
            new LanguageOption(AppLanguage.English, "English")
        ];
    }

    public static void SetLanguage(AppLanguage language)
    {
        if (CurrentLanguage == language)
        {
            return;
        }

        CurrentLanguage = language;
        var cultureName = language == AppLanguage.Russian ? "ru-RU" : "en-US";
        var culture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    public static string Get(string key)
    {
        var source = CurrentLanguage == AppLanguage.Russian ? RussianTexts : EnglishTexts;
        if (source.TryGetValue(key, out var value))
        {
            return value;
        }

        var additionalSource = CurrentLanguage == AppLanguage.Russian ? AdditionalRussianTexts : AdditionalEnglishTexts;
        return additionalSource.TryGetValue(key, out value) ? value : key;
    }

    public static string Format(string key, params object?[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, Get(key), args);
    }

    public static string? GetTableName(string key)
    {
        var source = CurrentLanguage == AppLanguage.Russian ? RussianTableNames : EnglishTableNames;
        return source.TryGetValue(key, out var value) ? value : null;
    }

    public static string? GetColumnName(string key)
    {
        if (key.Equals("image_path", StringComparison.OrdinalIgnoreCase))
        {
            return CurrentLanguage == AppLanguage.Russian ? "\u0424\u043E\u0442\u043E" : "Image";
        }

        if (key.Equals("category", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("category_id", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("category_name", StringComparison.OrdinalIgnoreCase))
        {
            return CurrentLanguage == AppLanguage.Russian ? "\u041A\u0430\u0442\u0435\u0433\u043E\u0440\u0438\u044F" : "Category";
        }

        var source = CurrentLanguage == AppLanguage.Russian ? RussianColumnNames : EnglishColumnNames;
        return source.TryGetValue(key, out var value) ? value : null;
    }
}

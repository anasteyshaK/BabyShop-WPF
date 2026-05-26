using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using BabyShop.Infrastructure;
using BabyShop.Localization;
using BabyShop.Models;

namespace BabyShop.Reporting;

public static class OrderReceiptComposer
{
    public static string BuildHtml(OrderDetailsViewModel order, bool autoPrint)
    {
        var logoPath = ProductImageStorage.ResolveImageAbsolutePath("Assets\\babyshop-logo.png");
        var logoUri = ToFileUri(logoPath);
        var generatedAt = DateTime.Now.ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture);
        var title = LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Квитанция заказа" : "Order Receipt";
        var statusLabel = LocalizeStatus(order.OrderStatus);
        var positionsLabel = LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Всего позиций" : "Positions";
        var quantityLabel = LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Общее количество" : "Total quantity";
        var totalLabel = LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Итого сумма заказа" : "Order total";
        var generatedLabel = LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Сформировано" : "Generated";
        var customerLabel = LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Клиент" : "Client";
        var addressLabel = LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Адрес доставки" : "Delivery address";
        var startDateLabel = LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Дата начала" : "Start date";
        var endDateLabel = LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Дата окончания" : "End date";
        var statusTitleLabel = LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Статус заказа" : "Order status";
        var totalCardLabel = LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Сумма заказа" : "Order amount";
        var thankYouTitle = LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Спасибо за покупку!" : "Thank you for your purchase!";
        var thankYouNote = LanguageManager.CurrentLanguage == AppLanguage.Russian
            ? "Мы ценим ваше доверие и заботимся о самых важных моментах."
            : "We appreciate your trust and care about the most important moments.";

        var builder = new StringBuilder();
        builder.Append(
            $$"""
            <!DOCTYPE html>
            <html lang="{{(LanguageManager.CurrentLanguage == AppLanguage.Russian ? "ru" : "en")}}">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>{{WebUtility.HtmlEncode(title)}} №{{order.OrderId}}</title>
                <style>
                    :root {
                        color-scheme: light;
                        --surface: rgba(255,255,255,.96);
                        --panel: #ffffff;
                        --line: #f1d8df;
                        --line-strong: #f4c8d4;
                        --text: #1f2b4d;
                        --muted: #7f88a3;
                        --accent: #f05b92;
                        --accent-soft: #fff0f5;
                        --accent-strong: #ff4c86;
                    }

                    * { box-sizing: border-box; }

                    body {
                        margin: 0;
                        font-family: "Segoe UI", "Trebuchet MS", sans-serif;
                        background:
                            radial-gradient(circle at top right, rgba(255, 211, 226, .62), transparent 30%),
                            radial-gradient(circle at bottom left, rgba(255, 231, 241, .8), transparent 28%),
                            linear-gradient(180deg, #fffafc 0%, #fff5f9 100%);
                        color: var(--text);
                    }

                    .page {
                        width: min(960px, calc(100% - 28px));
                        margin: 14px auto;
                        background: var(--surface);
                        border: 1px solid rgba(244, 200, 212, .95);
                        border-radius: 26px;
                        box-shadow: 0 18px 40px rgba(240, 91, 146, .12);
                        overflow: hidden;
                    }

                    .hero {
                        position: relative;
                        padding: 18px 22px 12px;
                        background:
                            radial-gradient(circle at 84% 18%, rgba(255, 214, 228, .85), transparent 14%),
                            linear-gradient(180deg, rgba(255,255,255,.98), rgba(255,249,251,.99));
                    }

                    .hero::after {
                        content: "♡ ☆ ☁";
                        position: absolute;
                        right: 22px;
                        top: 16px;
                        color: rgba(240, 91, 146, .42);
                        letter-spacing: 8px;
                        font-size: 20px;
                    }

                    .brand {
                        display: flex;
                        align-items: center;
                        gap: 12px;
                    }

                    .brand img {
                        width: 180px;
                        max-width: 38vw;
                        object-fit: contain;
                    }

                    .brand-note {
                        margin-top: 2px;
                        color: var(--muted);
                        font-size: 12px;
                    }

                    .receipt-title {
                        margin: 14px 0 4px;
                        text-align: center;
                        font-size: 34px;
                        font-weight: 800;
                        letter-spacing: .03em;
                    }

                    .receipt-subtitle {
                        text-align: center;
                        color: var(--muted);
                        font-size: 12px;
                    }

                    .ribbon-wrap {
                        text-align: center;
                    }

                    .ribbon {
                        display: inline-flex;
                        justify-content: center;
                        align-items: center;
                        min-width: 138px;
                        margin: 10px auto 0;
                        padding: 7px 18px;
                        color: white;
                        font-size: 24px;
                        font-weight: 800;
                        background: linear-gradient(135deg, #ff7ea9, #f05b92);
                        clip-path: polygon(8% 0, 92% 0, 100% 50%, 92% 100%, 8% 100%, 0 50%);
                        box-shadow: 0 10px 24px rgba(240, 91, 146, .18);
                    }

                    .overview {
                        display: grid;
                        grid-template-columns: repeat(7, 1fr);
                        margin-top: 16px;
                        background: white;
                        border: 1px solid var(--line);
                        border-radius: 20px;
                        overflow: hidden;
                    }

                    .overview-card {
                        padding: 12px 10px 10px;
                        border-right: 1px dashed var(--line-strong);
                    }

                    .overview-card:last-child {
                        border-right: none;
                    }

                    .overview-label {
                        color: var(--muted);
                        font-size: 10px;
                        margin-bottom: 6px;
                    }

                    .overview-value {
                        font-size: 16px;
                        line-height: 1.2;
                        font-weight: 800;
                    }

                    .status-pill {
                        display: inline-flex;
                        align-items: center;
                        justify-content: center;
                        padding: 6px 12px;
                        border-radius: 999px;
                        background: #fff3f6;
                        color: var(--accent-strong);
                        font-weight: 800;
                        font-size: 12px;
                    }

                    .section {
                        padding: 0 22px 14px;
                    }

                    .items-shell {
                        margin-top: 16px;
                        background: white;
                        border: 1px solid var(--line);
                        border-radius: 20px;
                        overflow: hidden;
                    }

                    .items-head,
                    .item-row {
                        display: grid;
                        grid-template-columns: minmax(320px, 1.55fr) 112px 132px 132px;
                    }

                    .items-head {
                        background: linear-gradient(180deg, #fff8fb, #fff2f6);
                        color: var(--muted);
                        font-size: 12px;
                        font-weight: 700;
                    }

                    .items-head div,
                    .item-row > div {
                        padding: 11px 13px;
                    }

                    .item-row {
                        align-items: center;
                        border-top: 1px dashed var(--line-strong);
                    }

                    .item-main {
                        display: flex;
                        gap: 10px;
                        align-items: center;
                    }

                    .item-photo {
                        width: 138px;
                        height: 82px;
                        border-radius: 14px;
                        border: 1px solid rgba(244, 200, 212, .8);
                        object-fit: cover;
                        background: #fff8fb;
                        flex: 0 0 auto;
                    }

                    .item-title {
                        margin: 0 0 4px;
                        font-size: 17px;
                        font-weight: 800;
                    }

                    .item-subtitle,
                    .item-meta {
                        color: #5c657d;
                        font-size: 12px;
                        line-height: 1.28;
                    }

                    .tag {
                        display: inline-flex;
                        margin-top: 6px;
                        padding: 4px 9px;
                        border-radius: 999px;
                        background: #fff1f6;
                        color: var(--accent);
                        font-size: 11px;
                        font-weight: 700;
                    }

                    .item-qty,
                    .item-unit,
                    .item-total {
                        text-align: center;
                        border-left: 1px dashed var(--line);
                    }

                    .qty-box {
                        display: inline-flex;
                        min-width: 48px;
                        justify-content: center;
                        padding: 8px 11px;
                        border-radius: 11px;
                        background: linear-gradient(180deg, #fff8fb, #fff2f7);
                        border: 1px solid rgba(244, 200, 212, .92);
                        font-size: 18px;
                        font-weight: 800;
                    }

                    .money {
                        font-size: 18px;
                        font-weight: 800;
                        line-height: 1.1;
                    }

                    .money small {
                        display: block;
                        margin-top: 4px;
                        color: var(--muted);
                        font-size: 11px;
                        font-weight: 600;
                    }

                    .money.total {
                        color: var(--accent-strong);
                    }

                    .totals {
                        display: grid;
                        grid-template-columns: 1fr 1fr 1.2fr;
                        gap: 0;
                        margin-top: 14px;
                        background: white;
                        border: 1px solid var(--line);
                        border-radius: 18px;
                        overflow: hidden;
                    }

                    .totals-card {
                        display: flex;
                        align-items: center;
                        gap: 10px;
                        padding: 12px 14px;
                        border-right: 1px dashed var(--line-strong);
                    }

                    .totals-card:last-child {
                        border-right: none;
                    }

                    .totals-icon {
                        width: 42px;
                        height: 42px;
                        border-radius: 12px;
                        display: inline-flex;
                        align-items: center;
                        justify-content: center;
                        background: linear-gradient(180deg, #fff9fb, #fff0f6);
                        color: var(--accent);
                        font-size: 20px;
                        box-shadow: inset 0 0 0 1px rgba(244, 200, 212, .8);
                    }

                    .totals-label {
                        color: var(--muted);
                        font-size: 11px;
                        margin-bottom: 4px;
                    }

                    .totals-value {
                        font-size: 17px;
                        font-weight: 800;
                    }

                    .totals-value.strong {
                        color: var(--accent-strong);
                        font-size: 25px;
                    }

                    .footer {
                        padding: 10px 22px 16px;
                        text-align: center;
                    }

                    .thanks {
                        color: var(--accent);
                        font-size: 24px;
                        font-weight: 800;
                        margin-bottom: 4px;
                    }

                    .footer-note,
                    .generated {
                        color: var(--muted);
                        font-size: 11px;
                    }

                    .generated {
                        margin-top: 8px;
                    }

                    @page {
                        size: A4 portrait;
                        margin: 5mm;
                    }

                    @media print {
                        html, body {
                            width: 210mm;
                            height: 297mm;
                            background: white;
                            -webkit-print-color-adjust: exact;
                            print-color-adjust: exact;
                        }

                        body {
                            margin: 0;
                        }

                        .page {
                            width: 200mm;
                            min-height: 287mm;
                            margin: 0 auto;
                            border: none;
                            border-radius: 0;
                            box-shadow: none;
                            zoom: 1;
                            transform-origin: top center;
                        }
                    }

                    @media (max-width: 980px) {
                        .overview {
                            grid-template-columns: repeat(2, 1fr);
                        }

                        .items-head {
                            display: none;
                        }

                        .item-row {
                            grid-template-columns: 1fr;
                        }

                        .item-qty,
                        .item-unit,
                        .item-total {
                            border-left: none;
                            border-top: 1px dashed var(--line);
                        }

                        .totals {
                            grid-template-columns: 1fr;
                        }

                        .totals-card {
                            border-right: none;
                            border-top: 1px dashed var(--line-strong);
                        }

                        .totals-card:first-child {
                            border-top: none;
                        }
                    }
                </style>
                <script>
                    function getReceiptPage() {
                        return document.getElementById('page-root');
                    }

                    function applyPrintFit() {
                        var page = getReceiptPage();
                        if (!page) {
                            return;
                        }

                        var previousWidth = page.style.width;
                        var previousZoom = page.style.zoom;
                        var previousTransform = page.style.transform;
                        var previousTransition = page.style.transition;

                        page.style.transition = 'none';
                        page.style.width = '200mm';
                        page.style.zoom = '1';
                        page.style.transform = 'none';

                        var printableHeightPx = 1084;
                        var measuredHeight = Math.ceil(page.scrollHeight || page.offsetHeight || 1);
                        var scale = Math.min(1, printableHeightPx / Math.max(measuredHeight, 1));

                        page.style.width = previousWidth;
                        page.style.zoom = scale.toFixed(4);
                        page.style.transform = previousTransform;
                        page.style.transition = previousTransition;
                    }

                    function clearPrintFit() {
                        var page = getReceiptPage();
                        if (!page) {
                            return;
                        }

                        page.style.zoom = '1';
                    }

                    function setupPrintReceipt(autoPrint) {
                        if (autoPrint) {
                            setTimeout(function () { window.print(); }, 350);
                        } else {
                            clearPrintFit();
                        }
                    }

                    if (window.addEventListener) {
                        window.addEventListener('beforeprint', applyPrintFit);
                        window.addEventListener('afterprint', clearPrintFit);
                        window.addEventListener('resize', applyPrintFit);
                    } else if (window.attachEvent) {
                        window.attachEvent('onbeforeprint', applyPrintFit);
                        window.attachEvent('onafterprint', clearPrintFit);
                        window.attachEvent('onresize', applyPrintFit);
                    }
                </script>
            </head>
            <body onload="setupPrintReceipt({{(autoPrint ? "true" : "false")}})">
                <div id="page-root" class="page">
                    <div class="hero">
                        <div class="brand">
                            {{(string.IsNullOrWhiteSpace(logoUri)
                                ? "<div style=\"font-size:34px;font-weight:900;color:#f05b92;\">Baby Shop</div>"
                                : $"<img src=\"{logoUri}\" alt=\"Baby Shop\">")}}
                            <div>
                                <div class="brand-note">{{WebUtility.HtmlEncode(LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Всё для самых важных" : "Everything for the little ones")}}</div>
                            </div>
                        </div>
                        <div class="receipt-title">{{WebUtility.HtmlEncode(title)}}</div>
                        <div class="receipt-subtitle">{{WebUtility.HtmlEncode(LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Детали и состав заказа" : "Order details and composition")}}</div>
                        <div class="ribbon-wrap"><div class="ribbon">№ {{order.OrderId}}</div></div>
                        <div class="overview">
                            {{BuildOverviewCard(LanguageManager.CurrentLanguage == AppLanguage.Russian ? "№ заказа" : "Order #", order.OrderId.ToString(CultureInfo.CurrentCulture))}}
                            {{BuildOverviewCard(customerLabel, order.ClientName)}}
                            {{BuildOverviewCard(addressLabel, order.DeliveryAddress)}}
                            {{BuildOverviewCard(startDateLabel, FormatDate(order.StartDate))}}
                            {{BuildOverviewCard(endDateLabel, FormatDate(order.EndDate))}}
                            <div class="overview-card">
                                <div class="overview-label">{{WebUtility.HtmlEncode(statusTitleLabel)}}</div>
                                <div class="status-pill">{{WebUtility.HtmlEncode(statusLabel)}}</div>
                            </div>
                            {{BuildOverviewCard(totalCardLabel, $"{FormatCurrency(order.TotalCost)}<br><span style=\"font-size:11px;color:#7f88a3;font-weight:700;\">MDL</span>", isRawHtml: true)}}
                        </div>
                    </div>
                    <div class="section">
                        <div class="items-shell">
                            <div class="items-head">
                                <div>{{WebUtility.HtmlEncode(LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Товар" : "Product")}}</div>
                                <div>{{WebUtility.HtmlEncode(LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Количество" : "Quantity")}}</div>
                                <div>{{WebUtility.HtmlEncode(LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Цена за ед." : "Unit price")}}</div>
                                <div>{{WebUtility.HtmlEncode(LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Итого" : "Total")}}</div>
                            </div>
            """);

        foreach (var item in order.Items)
        {
            builder.Append(BuildItemRow(item));
        }

        builder.Append(
            $$"""
                        </div>
                        <div class="totals">
                            <div class="totals-card">
                                <div class="totals-icon">🧺</div>
                                <div>
                                    <div class="totals-label">{{WebUtility.HtmlEncode(positionsLabel)}}</div>
                                    <div class="totals-value">{{order.TotalPositions}}</div>
                                </div>
                            </div>
                            <div class="totals-card">
                                <div class="totals-icon">📦</div>
                                <div>
                                    <div class="totals-label">{{WebUtility.HtmlEncode(quantityLabel)}}</div>
                                    <div class="totals-value">{{order.TotalQuantity}}</div>
                                </div>
                            </div>
                            <div class="totals-card">
                                <div class="totals-icon">🏷</div>
                                <div>
                                    <div class="totals-label">{{WebUtility.HtmlEncode(totalLabel)}}</div>
                                    <div class="totals-value strong">{{FormatCurrency(order.TotalCost)}}</div>
                                </div>
                            </div>
                        </div>
                    </div>
                    <div class="footer">
                        <div class="thanks">{{WebUtility.HtmlEncode(thankYouTitle)}}</div>
                        <div class="footer-note">{{WebUtility.HtmlEncode(thankYouNote)}}</div>
                        <div class="generated">{{WebUtility.HtmlEncode(generatedLabel)}}: {{WebUtility.HtmlEncode(generatedAt)}}</div>
                    </div>
                </div>
            </body>
            </html>
            """);

        return builder.ToString();
    }

    private static string BuildOverviewCard(string label, string value, bool isRawHtml = false)
    {
        var safeValue = isRawHtml ? value : WebUtility.HtmlEncode(value);
        return $$"""
            <div class="overview-card">
                <div class="overview-label">{{WebUtility.HtmlEncode(label)}}</div>
                <div class="overview-value">{{safeValue}}</div>
            </div>
            """;
    }

    private static string BuildItemRow(OrderDetailItemViewModel item)
    {
        var imageUri = ToFileUri(item.ImagePath);
        var subtitle = string.IsNullOrWhiteSpace(item.FabricType)
            ? item.CategoryName
            : $"{item.CategoryName} • {item.FabricType}";
        var colorLine = LanguageManager.CurrentLanguage == AppLanguage.Russian
            ? $"Цвет: {item.Color}"
            : $"Color: {item.Color}";

        return $$"""
            <div class="item-row">
                <div class="item-main">
                    {{(string.IsNullOrWhiteSpace(imageUri)
                        ? "<div class=\"item-photo\"></div>"
                        : $"<img class=\"item-photo\" src=\"{imageUri}\" alt=\"{WebUtility.HtmlEncode(item.ProductTitle)}\">")}}
                    <div>
                        <div class="item-title">{{WebUtility.HtmlEncode(item.ProductTitle)}}</div>
                        <div class="item-subtitle">{{WebUtility.HtmlEncode(subtitle)}}</div>
                        <div class="item-meta">{{WebUtility.HtmlEncode(colorLine)}}</div>
                        {{(string.IsNullOrWhiteSpace(item.CategoryName)
                            ? string.Empty
                            : $"<div class=\"tag\">{WebUtility.HtmlEncode(item.CategoryName)}</div>")}}
                    </div>
                </div>
                <div class="item-qty">
                    <div class="qty-box">{{item.Quantity}}</div>
                    <div class="item-meta" style="margin-top:6px;">{{WebUtility.HtmlEncode(LanguageManager.CurrentLanguage == AppLanguage.Russian ? "шт." : "pcs.")}}</div>
                </div>
                <div class="item-unit">
                    <div class="money">{{FormatCurrency(item.UnitPrice)}}<small>MDL</small></div>
                </div>
                <div class="item-total">
                    <div class="money total">{{FormatCurrency(item.LineTotal)}}<small>MDL</small></div>
                </div>
            </div>
            """;
    }

    private static string FormatCurrency(decimal value)
    {
        return value.ToString("N2", CultureInfo.CurrentCulture);
    }

    private static string FormatDate(DateTime? value)
    {
        return value?.ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture) ?? "—";
    }

    private static string LocalizeStatus(string status)
    {
        return status.Trim() switch
        {
            "Pending" => LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Ожидает" : "Pending",
            "Shipped" => LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Отправлен" : "Shipped",
            "Completed" => LanguageManager.CurrentLanguage == AppLanguage.Russian ? "Завершён" : "Completed",
            _ => status
        };
    }

    private static string ToFileUri(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return File.Exists(path)
            ? new Uri(path).AbsoluteUri
            : string.Empty;
    }
}

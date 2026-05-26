using System.Windows;

namespace BabyShop;

public partial class UserMainWindow
{
    private async void CheckoutOrderButtonMdl_Click(object sender, RoutedEventArgs e)
    {
        if (CartItems.Count == 0)
        {
            return;
        }

        if (_isGuestAccount || !_currentUserId.HasValue)
        {
            SetCheckoutStatus(
                T(
                    "\u0413\u043e\u0441\u0442\u0435\u0432\u043e\u0439 \u0440\u0435\u0436\u0438\u043c \u043d\u0435 \u0441\u043e\u0445\u0440\u0430\u043d\u044f\u0435\u0442 \u0437\u0430\u043a\u0430\u0437\u044b. \u0412\u043e\u0439\u0434\u0438\u0442\u0435 \u0432 \u0430\u043a\u043a\u0430\u0443\u043d\u0442, \u0447\u0442\u043e\u0431\u044b \u043e\u0444\u043e\u0440\u043c\u0438\u0442\u044c \u0438\u0445.",
                    "Guest mode does not save orders. Sign in to place and keep orders."),
                DefaultBadgeTextBrush);
            return;
        }

        try
        {
            ClearCheckoutStatus();
            var suggestedCustomerDetails = _lastCheckoutCustomerDetails
                ?? await _repository.GetLatestCheckoutCustomerDetailsByFullNameAsync(_currentUsername);

            var checkoutForm = new CheckoutForm(_repository, _currentUsername, suggestedCustomerDetails)
            {
                Owner = this
            };

            if (checkoutForm.ShowDialog() != true || checkoutForm.Result is null)
            {
                return;
            }

            var request = BuildCheckoutRequest(checkoutForm.Result);
            CanCheckout = false;

            var result = await _repository.CreateCheckoutOrderAsync(request);
            _lastCheckoutCustomerDetails = checkoutForm.Result;

            SetCheckoutStatus($"Order #{result.OrderId} was saved successfully.", SuccessStatusBrush);
            MessageBox.Show(
                $"Order #{result.OrderId} has been created.\nTotal: {result.TotalCost:0.##} MDL",
                "Order created",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            CartItems.Clear();
            RefreshCartState();
            IsCartOpen = false;
        }
        catch (Exception exception)
        {
            SetCheckoutStatus(exception.Message, DefaultBadgeTextBrush);
            RefreshCartState();
        }
    }
}

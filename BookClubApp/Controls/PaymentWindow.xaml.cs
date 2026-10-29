using BookClubApp.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;

namespace BookClubApp.Controls {
    public partial class PaymentWindow : Window {
        private readonly List<CartRequestLine> lines;
        private readonly decimal amount;
        private readonly Guid requestId = Guid.NewGuid();
        private bool busy;
        public int OrderId { get; private set; }
        public PaymentWindow(List<CartRequestLine> lines, decimal amount) {
            InitializeComponent(); this.lines = lines; this.amount = amount;
            txtAmount.Text = amount.ToString("N2") + " ₽";
            Closing += Window_Closing;
        }
        private async void btnPay_Click(object sender, RoutedEventArgs e) {
            if (busy) return;
            busy = true; btnPay.IsEnabled = false; btnClose.IsEnabled = false; txtMessage.Text = "";
            try {
                OrderId = await ClientOrderService.CreateCashOrderAsync(lines, requestId, amount);
                DialogResult = true;
            } catch (Exception error) {
                txtMessage.Text = error is ManagementException || error is UnauthorizedAccessException || error is ArgumentException
                    ? error.Message : "Не удалось оформить заказ. Проверьте связь и повторите попытку. Повторное нажатие не создаст копию заказа.";
            } finally { busy = false; btnPay.IsEnabled = true; btnClose.IsEnabled = true; }
        }
        private void Window_Closing(object sender, CancelEventArgs e) { if (busy && !DialogResult.HasValue) e.Cancel = true; }
        private void btnClose_Click(object sender, RoutedEventArgs e) { DialogResult = false; }
    }
}
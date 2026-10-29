using System;
using BookClubApp.Helpers;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Controls;

namespace BookClubApp.Pages {
    public partial class ConfirmationCodePage : Page {
        private readonly DispatcherTimer resendTimer = new DispatcherTimer();
        private int secondsLeft = 52;

        public ConfirmationCodePage() : this("") { }

        public ConfirmationCodePage(string phone) {
            InitializeComponent();
            txtBlockPhone.Text = string.IsNullOrWhiteSpace(phone) ? "8 (___) ___-__-__" : phone;
            resendTimer.Interval = TimeSpan.FromSeconds(1);
            resendTimer.Tick += resendTimer_Tick;
            Loaded += (sender, e) => { resendTimer.Start(); UpdateResendText(); };
            Unloaded += (sender, e) => resendTimer.Stop();
        }

        private void UpdateResendText() {
            txtBlockResend.Text = secondsLeft > 0
                ? "Отправить код повторно можно через " + TimeSpan.FromSeconds(secondsLeft).ToString(@"mm\:ss")
                : "Можно запросить новый код";
            btnResend.IsEnabled = secondsLeft == 0;
        }

        private void resendTimer_Tick(object sender, EventArgs e) {
            if (secondsLeft > 0) secondsLeft--;
            UpdateResendText();
            if (secondsLeft == 0) resendTimer.Stop();
        }

        private void txtBoxCode_PreviewTextInput(object sender, TextCompositionEventArgs e) {
            e.Handled = !NumericInput.IsDigits(e.Text);
        }

        private void txtBoxCode_TextChanged(object sender, TextChangedEventArgs e) {
            TextBox box = (TextBox)sender;
            if (!string.IsNullOrEmpty(box.Text) && (!NumericInput.IsDigits(box.Text) || box.Text.Length > 1)) {
                NumericInput.KeepDigits(box);
                return;
            }
            if (box.Text.Length == 1) box.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }

        private void txtBoxCode_PreviewKeyDown(object sender, KeyEventArgs e) {
            TextBox box = (TextBox)sender;
            if (e.Key == Key.Space) {
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Back && box.Text.Length == 0) {
                int index = int.Parse(box.Name.Substring(box.Name.Length - 1));
                if (index > 1) {
                    TextBox previous = (TextBox)FindName("txtBoxCode" + (index - 1));
                    previous.Focus();
                    previous.SelectAll();
                    e.Handled = true;
                }
            }
        }

        private void txtBoxCode_Pasting(object sender, DataObjectPastingEventArgs e) {
            string text = e.DataObject.GetData(typeof(string)) as string;
            e.CancelCommand();
            if (!NumericInput.IsDigits(text)) return;
            int start = int.Parse(((TextBox)sender).Name.Substring(10));
            if (text.Length > 7 - start) return;
            for (int i = 0; i < text.Length && start + i <= 6; i++) {
                ((TextBox)FindName("txtBoxCode" + (start + i))).Text = text[i].ToString();
            }
        }

        private void btnBack_Click(object sender, RoutedEventArgs e) {
            NavigationService.Navigate(new PasswordRecoveryPage());
        }

        private void btnContinue_Click(object sender, RoutedEventArgs e) {
            NavigationService.Navigate(new NewPasswordPage());
        }

        private void btnResend_Click(object sender, RoutedEventArgs e) {
            secondsLeft = 52;
            UpdateResendText();
            resendTimer.Start();
        }

    }
}

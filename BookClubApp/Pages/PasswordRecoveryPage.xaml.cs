using System.Windows;
using System.Windows.Controls;
using BookClubApp.Helpers;

namespace BookClubApp.Pages {
    public partial class PasswordRecoveryPage : Page {
        public PasswordRecoveryPage() {
            InitializeComponent();
            NumericInput.Attach(txtBoxPhone);
        }

        private void btnBack_Click(object sender, RoutedEventArgs e) {
            NavigationService.Navigate(new LoginPage());
        }

        private void btnContinue_Click(object sender, RoutedEventArgs e) {
            string phone = txtBoxPhone.Text.Length > 0 ? "8" + txtBoxPhone.Text : "";
            NavigationService.Navigate(new ConfirmationCodePage(phone));
        }

        private void btnLogin_Click(object sender, RoutedEventArgs e) {
            NavigationService.Navigate(new LoginPage());
        }

    }
}

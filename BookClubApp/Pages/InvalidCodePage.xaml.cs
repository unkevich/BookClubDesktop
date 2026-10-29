using System.Windows;
using System.Windows.Controls;

namespace BookClubApp.Pages {
    public partial class InvalidCodePage : Page {
        public InvalidCodePage() {
            InitializeComponent();
        }

        private void btnBack_Click(object sender, RoutedEventArgs e) {
            NavigationService.Navigate(new LoginPage());
        }

        private void btnContinue_Click(object sender, RoutedEventArgs e) {
            NavigationService.Navigate(new ConfirmationCodePage());
        }

        private void btnLogin_Click(object sender, RoutedEventArgs e) {
            NavigationService.Navigate(new ConfirmationCodePage());
        }

    }
}

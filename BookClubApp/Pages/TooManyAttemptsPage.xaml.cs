using System.Windows;
using System.Windows.Controls;

namespace BookClubApp.Pages {
    public partial class TooManyAttemptsPage : Page {
        public TooManyAttemptsPage() {
            InitializeComponent();
        }

        private void btnBack_Click(object sender, RoutedEventArgs e) {
            NavigationService.Navigate(new LoginPage());
        }

        private void btnContinue_Click(object sender, RoutedEventArgs e) {
            NavigationService.Navigate(new LoginPage());
        }

        private void btnLogin_Click(object sender, RoutedEventArgs e) {
            NavigationService.Navigate(new LoginPage());
        }

    }
}

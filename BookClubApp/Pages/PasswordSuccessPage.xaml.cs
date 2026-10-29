using System.Windows;
using System.Windows.Controls;

namespace BookClubApp.Pages {
    public partial class PasswordSuccessPage : Page {
        public PasswordSuccessPage() {
            InitializeComponent();
        }

        private void btnBack_Click(object sender, RoutedEventArgs e) {
            NavigationService.Navigate(new LoginPage());
        }

        private void btnContinue_Click(object sender, RoutedEventArgs e) {
            NavigationService.Navigate(new LoginPage());
        }

    }
}

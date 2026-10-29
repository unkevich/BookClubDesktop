using BookClubApp.Pages;
using System.Windows;
using System.Windows.Navigation;

namespace BookClubApp {
    public partial class AuthWindow : Window {
        public AuthWindow() {
            InitializeComponent();
            MainFrame.Navigated += MainFrame_Navigated;
            MainFrame.Navigate(new LoginPage());
        }

        private void MainFrame_Navigated(object sender, NavigationEventArgs e) {
            // Пароли и заполненные формы не сохраняются в истории переходов.
            while (MainFrame.CanGoBack) MainFrame.RemoveBackEntry();
        }

        public void ShowProducts() {
            MainFrame.Navigate(new ProductsTablePage());
            SizeToContent = SizeToContent.Manual;
            MinWidth = 900; MinHeight = 600;
            Width = 1100; Height = 700;
            WindowState = WindowState.Maximized;
        }
    }
}

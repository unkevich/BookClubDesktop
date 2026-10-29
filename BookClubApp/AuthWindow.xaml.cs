using BookClubApp.Pages;
using System.Windows;
using System.Windows.Navigation;
using BookClubApp.Services;

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
            MinWidth = 1100; MinHeight = 700;
            Width = 1400; Height = 900;
            WindowState = WindowState.Maximized;
        }

        public void ShowWorkspace() {
            if (AuthService.Current == null) { ShowLogin(); return; }
            if (!AuthService.Current.IsStaff) { ShowProducts(); return; }
            AuthService.RequireStaff();
            MainFrame.Navigate(new HomePage());
            SizeToContent = SizeToContent.Manual;
            MinWidth = 1100; MinHeight = 700;
            Width = 1400; Height = 900;
            WindowState = WindowState.Maximized;
        }

        public void ShowLogin() {
            AuthService.SignOut();
            WindowState = WindowState.Normal;
            MinWidth = 380; MinHeight = 600;
            Width = 400; Height = 660;
            MainFrame.Navigate(new LoginPage());
            Left = SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - Width) / 2;
            Top = SystemParameters.WorkArea.Top + (SystemParameters.WorkArea.Height - Height) / 2;
        }
    }
}

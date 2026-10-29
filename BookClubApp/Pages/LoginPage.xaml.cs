using System.Windows;
using System.Windows.Controls;
using BookClubApp.Helpers;

namespace BookClubApp.Pages {
    public partial class LoginPage : Page {
        public LoginPage() {
            InitializeComponent();
            NumericInput.Attach(txtBoxPhone);
        }

        private void btnLogin_Click(object sender, RoutedEventArgs e) {
            // Переход к существующей странице товаров. Проверка входа подключается отдельно.
            AuthWindow window = (AuthWindow)Window.GetWindow(this);
            window.ShowProducts();
        }

        private void btnLoginGuest_Click(object sender, RoutedEventArgs e) {
            AuthWindow window = (AuthWindow)Window.GetWindow(this);
            window.ShowProducts();
        }

        private void btnForgotPassword_Click(object sender, RoutedEventArgs e) {
            NavigationService.Navigate(new PasswordRecoveryPage());
        }

        private bool passwordUpdating;

        private void pwdBox_PasswordChanged(object sender, RoutedEventArgs e) {
            if (passwordUpdating) return;
            PasswordBox password = (PasswordBox)sender;
            TextBox visible = (TextBox)FindName(password.Name + "Visible");
            TextBlock hint = (TextBlock)FindName(password.Name + "Hint");
            if (visible == null || hint == null) return;
            passwordUpdating = true;
            visible.Text = password.Password;
            hint.Visibility = password.Password.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            passwordUpdating = false;
        }

        private void txtBoxPassword_TextChanged(object sender, TextChangedEventArgs e) {
            if (passwordUpdating) return;
            TextBox visible = (TextBox)sender;
            PasswordBox password = (PasswordBox)FindName(visible.Name.Replace("Visible", ""));
            if (password != null) password.Password = visible.Text;
        }

        private void btnShowPassword_Click(object sender, RoutedEventArgs e) {
            string name = (string)((Button)sender).Tag;
            PasswordBox password = (PasswordBox)FindName(name);
            TextBox visible = (TextBox)FindName(name + "Visible");
            bool show = visible.Visibility != Visibility.Visible;
            visible.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            password.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
            if (show) {
                visible.Focus();
                visible.CaretIndex = visible.Text.Length;
            } else {
                password.Focus();
            }
        }
    }
}

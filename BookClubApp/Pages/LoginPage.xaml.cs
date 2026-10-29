using System.Windows;
using System.Windows.Controls;
using BookClubApp.Helpers;
using BookClubApp.Services;
using System;

namespace BookClubApp.Pages {
    public partial class LoginPage : Page {
        public LoginPage() {
            InitializeComponent();
            NumericInput.Attach(txtBoxPhone);
            try {
                txtBoxPhone.Text = Properties.Settings.Default.RememberedPhone;
                chkBoxRemember.IsChecked = txtBoxPhone.Text.Length > 0;
            } catch (System.Configuration.ConfigurationErrorsException) { }
        }

        private async void btnLogin_Click(object sender, RoutedEventArgs e) {
            if (txtBoxPhone.Text.Length != 10 || string.IsNullOrEmpty(pwdBoxPassword.Password)) {
                txtBlockLoginError.Text = "Введите 10 цифр номера и пароль.";
                txtBlockLoginError.Visibility = Visibility.Visible;
                return;
            }
            txtBlockLoginError.Visibility = Visibility.Collapsed;
            IsEnabled = false;
            try {
                UserSession session = await AuthService.SignInAsync(txtBoxPhone.Text, pwdBoxPassword.Password);
                if (!IsLoaded) { AuthService.SignOut(); return; }
                if (session == null) {
                    txtBlockLoginError.Text = "Неверный номер телефона или пароль.";
                    txtBlockLoginError.Visibility = Visibility.Visible;
                    return;
                }
                try {
                    Properties.Settings.Default.RememberedPhone = chkBoxRemember.IsChecked == true ? txtBoxPhone.Text : "";
                    Properties.Settings.Default.Save();
                } catch (Exception error) when (error is System.Configuration.ConfigurationErrorsException || error is System.IO.IOException || error is UnauthorizedAccessException) {
                    // Ошибка сохранения номера не отменяет успешный вход.
                }
                pwdBoxPassword.Clear();
                ((AuthWindow)Window.GetWindow(this)).ShowWorkspace();
            } catch (Exception) {
                AuthService.SignOut();
                if (IsLoaded) {
                    txtBlockLoginError.Text = "Не удалось войти. Проверьте подключение к базе данных и попробуйте снова.";
                    txtBlockLoginError.Visibility = Visibility.Visible;
                }
            } finally {
                IsEnabled = true;
            }
        }
        private void btnLoginGuest_Click(object sender, RoutedEventArgs e) {
            AuthWindow window = (AuthWindow)Window.GetWindow(this);
            AuthService.SignOut();
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

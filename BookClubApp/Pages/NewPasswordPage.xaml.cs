using System.Windows;
using System.Windows.Controls;
using System;
using System.Linq;
using System.Windows.Media;

namespace BookClubApp.Pages {
    public partial class NewPasswordPage : Page {
        public NewPasswordPage() {
            InitializeComponent();
            UpdatePasswordRules();
        }

        private void btnBack_Click(object sender, RoutedEventArgs e) {
            NavigationService.Navigate(new ConfirmationCodePage());
        }

        private void btnSave_Click(object sender, RoutedEventArgs e) {
            UpdatePasswordRules();
            if (!CanSavePassword()) {
                txtBlockPasswordError.Text = "Выполните все требования и повторите пароль без ошибок.";
                txtBlockPasswordError.Visibility = Visibility.Visible;
                return;
            }
            NavigationService.Navigate(new PasswordSuccessPage());
        }

        private bool CanSavePassword() {
            string password = pwdBoxNewPassword.Password;
            return password.Length >= 8 && password.Any(char.IsUpper)
                && password.Any(character => character >= '0' && character <= '9')
                && password.Any(character => char.IsPunctuation(character) || char.IsSymbol(character))
                && string.Equals(password, pwdBoxRepeatPassword.Password, StringComparison.Ordinal);
        }

        private void SetRule(TextBlock rule, string label, bool valid) {
            rule.Text = (valid ? "✓  " : "○  ") + label;
            rule.Foreground = (Brush)FindResource(valid ? "SuccessBrush" : "TextSecondaryBrush");
        }

        private void UpdatePasswordRules() {
            if (txtRuleLength == null || pwdBoxRepeatPassword == null || btnSavePassword == null) return;
            string password = pwdBoxNewPassword.Password;
            SetRule(txtRuleLength, "Не менее 8 символов", password.Length >= 8);
            SetRule(txtRuleUppercase, "Есть заглавная буква", password.Any(char.IsUpper));
            SetRule(txtRuleDigit, "Есть цифры", password.Any(character => character >= '0' && character <= '9'));
            SetRule(txtRuleSpecial, "Есть специальный символ (например, !@#)", password.Any(character => char.IsPunctuation(character) || char.IsSymbol(character)));
            btnSavePassword.IsEnabled = CanSavePassword();
            txtBlockPasswordError.Text = "Пароли не совпадают.";
            txtBlockPasswordError.Visibility = pwdBoxRepeatPassword.Password.Length > 0
                && !string.Equals(password, pwdBoxRepeatPassword.Password, StringComparison.Ordinal) ? Visibility.Visible : Visibility.Collapsed;
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
            UpdatePasswordRules();
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

using BookClubApp.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BookClubApp.Pages {
    public partial class HomePage : Page {
        private DashboardData data;
        private bool loading;
        private bool initialized;

        public HomePage() {
            AuthService.RequireStaff();
            InitializeComponent();
            txtBlockUser.Text = AuthService.Current.NumberPhone;
            txtBlockRole.Text = AuthService.Current.RoleName;
            listMenu.SelectedIndex = 0;
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e) {
            if (initialized) return;
            initialized = true;
            await LoadDataAsync();
        }

        private async Task LoadDataAsync() {
            if (loading) return;
            loading = true;
            btnRefresh.IsEnabled = false;
            ShowStatus("Загружаем данные…", false);
            try {
                DashboardData result = await DashboardService.LoadAsync();
                if (!IsLoaded) return;
                data = result;
                txtBlockOrderCount.Text = data.OrderCount.ToString();
                txtBlockProductCount.Text = data.ProductCount.ToString();
                txtBlockClientCount.Text = data.ClientCount.ToString();
                txtBlockLowStockCount.Text = data.LowStockCount.ToString();
                statusPanel.Visibility = Visibility.Collapsed;
                ApplySearch();
            } catch (Exception) {
                if (IsLoaded) ShowStatus("Не удалось загрузить данные из базы. Проверьте подключение и нажмите «Обновить».", true);
            } finally {
                loading = false;
                btnRefresh.IsEnabled = true;
            }
        }

        private void ShowStatus(string message, bool error) {
            txtBlockStatus.Text = message;
            statusPanel.Background = (System.Windows.Media.Brush)FindResource(error ? "BackgroundErrorBrush" : "BackgroundInfoBrush");
            statusPanel.Visibility = Visibility.Visible;
        }

        private bool Matches(string value) {
            return (value ?? "").IndexOf(txtBoxSearch.Text.Trim(), StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        private void ApplySearch() {
            if (data == null) return;
            var orders = data.Orders.Where(order => Matches(order.Number) || Matches(order.Phone) || Matches(order.Status) || Matches(order.Talon)).ToList();
            var products = data.Products.Where(product => Matches(product.Name) || Matches(product.Author) || Matches(product.Category)).ToList();
            var clients = data.Clients.Where(client => Matches(client.NumberPhone) || Matches(client.Id.ToString())).ToList();
            gridRecentOrders.ItemsSource = orders.Take(5).ToList();
            gridOrders.ItemsSource = orders;
            listPopularProducts.ItemsSource = products.Take(5).ToList();
            listProducts.ItemsSource = products;
            gridClients.ItemsSource = clients;
            var lowStock = data.Products.Where(product => product.BookCount <= product.MinBookCount).OrderBy(product => product.BookCount).Take(4).ToList();
            listLowStock.ItemsSource = lowStock;
            txtBlockStockOk.Visibility = lowStock.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            txtBlockNoOrders.Visibility = orders.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            txtBlockNoProducts.Visibility = products.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            bool empty = listMenu.SelectedIndex == 1 ? orders.Count == 0 : listMenu.SelectedIndex == 2 ? products.Count == 0 : listMenu.SelectedIndex == 3 && clients.Count == 0;
            txtBlockEmpty.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        }

        private void listMenu_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (panelHome == null) return;
            panelHome.Visibility = listMenu.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            panelOrders.Visibility = listMenu.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
            panelProducts.Visibility = listMenu.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
            panelClients.Visibility = listMenu.SelectedIndex == 3 ? Visibility.Visible : Visibility.Collapsed;
            txtBlockSection.Text = ((ListBoxItem)listMenu.SelectedItem).Content.ToString();
            ApplySearch();
        }

        private void txtBoxSearch_TextChanged(object sender, TextChangedEventArgs e) { ApplySearch(); }
        private async void btnRefresh_Click(object sender, RoutedEventArgs e) { await LoadDataAsync(); }
        private void btnOrders_Click(object sender, RoutedEventArgs e) { listMenu.SelectedIndex = 1; }
        private void btnProducts_Click(object sender, RoutedEventArgs e) { listMenu.SelectedIndex = 2; }
        private void btnClients_Click(object sender, RoutedEventArgs e) { listMenu.SelectedIndex = 3; }

        private void btnLogout_Click(object sender, RoutedEventArgs e) {
            ((AuthWindow)Window.GetWindow(this)).ShowLogin();
        }

        private void btnProfile_Click(object sender, RoutedEventArgs e) {
            ContextMenu menu = new ContextMenu();
            MenuItem account = new MenuItem { Header = AuthService.Current.NumberPhone + " · " + AuthService.Current.RoleName, IsEnabled = false };
            MenuItem logout = new MenuItem { Header = "Выйти" };
            logout.Click += btnLogout_Click;
            menu.Items.Add(account); menu.Items.Add(new Separator()); menu.Items.Add(logout);
            menu.PlacementTarget = (Button)sender;
            menu.IsOpen = true;
        }

        private async void gridOrders_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
            OrderRow order = ((DataGrid)sender).SelectedItem as OrderRow;
            if (order == null) return;
            try {
                var items = await DashboardService.LoadOrderItemsAsync(order.Id);
                if (!IsLoaded || AuthService.Current == null || !AuthService.Current.IsStaff) return;
                string composition = items.Count == 0 ? "Состав не указан" : string.Join(Environment.NewLine, items.GroupBy(name => name).Select(group => group.Key + " × " + group.Count()));
                MessageBox.Show(Window.GetWindow(this), "Клиент: " + order.Phone + "\nСтатус: " + order.Status + "\nОплата: " + order.Payment
                    + "\nТалон: " + order.Talon + "\nСумма: " + order.Amount.ToString("N2") + " ₽\n\n" + composition,
                    "Заказ " + order.Number, MessageBoxButton.OK, MessageBoxImage.Information);
            } catch (Exception) {
                if (IsLoaded) ShowStatus("Не удалось загрузить состав заказа. Проверьте подключение к базе.", true);
            }
        }
    }
}

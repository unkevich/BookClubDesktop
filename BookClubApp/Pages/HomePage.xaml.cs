using BookClubApp.Services;
using System;
using BookClubApp.Helpers;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BookClubApp.Pages {
    public partial class HomePage : Page {
        private DashboardData data;
        private bool loading;
        private bool dashboardDirty;
        private int selectedSection;
        private readonly LiveDataWatcher watcher;

        public HomePage() {
            AuthService.RequireStaff();
            InitializeComponent();
            txtBlockUser.Text = AuthService.Current.DisplayPhone;
            txtBlockRole.Text = AuthService.Current.RoleName;
            listMenu.SelectedIndex = 0;
            watcher = new LiveDataWatcher(AutoRefreshAsync, () => { if (data == null) ShowStatus("Нет подключения к базе. Повторная попытка выполняется автоматически.", true); });
            Unloaded += (sender, e) => watcher.Stop();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e) { watcher.Start(); }

        private async Task<bool> LoadDataAsync(bool silent = false) {
            if (loading) return false;
            loading = true;
            if (!silent && data == null) ShowStatus("Загружаем данные…", false);
            try {
                DashboardData result = await DashboardService.LoadAsync();
                if (!IsLoaded) return false;
                data = result;
                txtBlockOrderCount.Text = data.OrderCount.ToString();
                txtBlockProductCount.Text = data.ProductCount.ToString();
                txtBlockClientCount.Text = data.ClientCount.ToString();
                txtBlockLowStockCount.Text = data.LowStockCount.ToString();
                statusPanel.Visibility = Visibility.Collapsed;
                ApplySearch();
                return true;
            } catch (Exception) {
                if (IsLoaded && (!silent || data == null)) ShowStatus("Нет подключения к базе. Повторная попытка выполняется автоматически.", true);
                return false;
            } finally {
                loading = false;
            }
        }

        private async Task<bool> AutoRefreshAsync() {
            if (!IsLoaded) return false;
            if (selectedSection == 0) {
                bool refreshed = await LoadDataAsync(true);
                if (refreshed) managementView.InvalidateData();
                return refreshed;
            }
            dashboardDirty = true;
            return await managementView.AutoReloadAsync();
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
            managementView?.SetSearch(txtBoxSearch.Text);
            if (data == null) return;
            var orders = data.Orders.Where(order => Matches(order.Number) || Matches(order.Phone) || Matches(order.Status) || Matches(order.Talon)).ToList();
            var products = data.Products.Where(product => Matches(product.Name) || Matches(product.Author) || Matches(product.Category)).ToList();
            gridRecentOrders.ItemsSource = orders.Take(5).ToList();
            listPopularProducts.ItemsSource = products.Take(5).ToList();
            var lowStock = data.Products.Where(product => product.BookCount <= product.MinBookCount).OrderBy(product => product.BookCount).Take(4).ToList();
            listLowStock.ItemsSource = lowStock;
            txtBlockStockOk.Visibility = lowStock.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            txtBlockNoOrders.Visibility = orders.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            txtBlockNoProducts.Visibility = products.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void listMenu_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (managementView == null || listMenu.SelectedIndex < 0) return;
            int section = listMenu.SelectedIndex;
            if (section != selectedSection && !(section == 0 ? managementView.TryLeave() : managementView.SelectMode(section))) {
                listMenu.SelectedIndex = selectedSection; return;
            }
            selectedSection = section;
            homeScroll.Visibility = section == 0 ? Visibility.Visible : Visibility.Collapsed;
            managementView.Visibility = section != 0 ? Visibility.Visible : Visibility.Collapsed;
            btnAddEntity.Visibility = section != 0 ? Visibility.Visible : Visibility.Collapsed;
            btnAddEntity.Content = section == 1 ? "+ Создать заказ" : section == 2 ? "+ Добавить товар" : "+ Добавить клиента";
            txtBlockSection.Text = ((ListBoxItem)listMenu.SelectedItem).Content.ToString();
            ApplySearch();
            if (section == 0 && dashboardDirty) { dashboardDirty = false; await LoadDataAsync(); }
        }

        private void txtBoxSearch_TextChanged(object sender, TextChangedEventArgs e) { ApplySearch(); }
        private void btnAddEntity_Click(object sender, RoutedEventArgs e) { managementView.BeginAdd(); }
        private void managementView_RecordsChanged(object sender, EventArgs e) { dashboardDirty = true; }
        private void btnOrders_Click(object sender, RoutedEventArgs e) { listMenu.SelectedIndex = 1; }
        private void btnProducts_Click(object sender, RoutedEventArgs e) { listMenu.SelectedIndex = 2; }
        private void btnClients_Click(object sender, RoutedEventArgs e) { listMenu.SelectedIndex = 3; }
        private void btnAddClientQuick_Click(object sender, RoutedEventArgs e) { listMenu.SelectedIndex = 3; if (listMenu.SelectedIndex == 3) managementView.BeginAdd(); }

        private void btnLogout_Click(object sender, RoutedEventArgs e) {
            if (!managementView.TryLeave()) return;
            ((AuthWindow)Window.GetWindow(this)).ShowLogin();
        }

        private void btnProfile_Click(object sender, RoutedEventArgs e) {
            ContextMenu menu = new ContextMenu();
            MenuItem account = new MenuItem { Header = AuthService.Current.DisplayPhone + " · " + AuthService.Current.RoleName, IsEnabled = false };
            MenuItem logout = new MenuItem { Header = "Выйти", Foreground = (System.Windows.Media.Brush)FindResource("MainBrush") };
            logout.Click += btnLogout_Click;
            menu.Items.Add(account); menu.Items.Add(new Separator()); menu.Items.Add(logout);
            menu.PlacementTarget = (Button)sender;
            menu.IsOpen = true;
        }

        private void gridOrders_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
            OrderRow order = ((DataGrid)sender).SelectedItem as OrderRow;
            if (order == null) return;
            listMenu.SelectedIndex = 1;
            if (listMenu.SelectedIndex == 1) managementView.OpenWhenReady(order.Id);
        }

        private void btnRecentMenu_Click(object sender, RoutedEventArgs e) {
            Button button = (Button)sender;
            OrderRow order = (OrderRow)button.DataContext;
            ContextMenu menu = new ContextMenu();
            MenuItem edit = new MenuItem { Header = "Открыть / редактировать" };
            edit.Click += (source, args) => { listMenu.SelectedIndex = 1; if (listMenu.SelectedIndex == 1) managementView.OpenWhenReady(order.Id); };
            menu.Items.Add(edit);
            MenuItem statuses = new MenuItem { Header = "Изменить статус" };
            foreach (string value in ManagementService.OrderStatuses) {
                string status = value;
                MenuItem item = new MenuItem { Header = status, IsChecked = status == order.Status,
                    Icon = new System.Windows.Shapes.Ellipse { Width = 8, Height = 8, Fill = new OrderRow { Status = status }.StatusBrush } };
                item.Click += async (source, args) => {
                    try { await ManagementService.ChangeStatusAsync(order.Id, status); await LoadDataAsync(); managementView.InvalidateData(); }
                    catch (Exception error) { ShowStatus(error is ManagementException ? error.Message : "Не удалось изменить статус заказа.", true); }
                };
                statuses.Items.Add(item);
            }
            menu.Items.Add(statuses);
            MenuItem pdf = new MenuItem { Header = "Сохранить чек (PDF)" };
            pdf.Click += async (source, args) => { await ReceiptExport.SaveAsync(Window.GetWindow(this), order.Id); };
            menu.Items.Add(pdf); menu.Items.Add(new Separator());
            MenuItem delete = new MenuItem { Header = "Удалить заказ", Foreground = (System.Windows.Media.Brush)FindResource("MainBrush") };
            delete.Click += async (source, args) => {
                try {
                    var draft = await ManagementService.LoadOrderAsync(order.Id);
                    if (MessageBox.Show(Window.GetWindow(this), "Удалить заказ #" + order.Id + "? Товары неотменённого заказа будут возвращены на склад.", "Удаление", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                    await ManagementService.DeleteOrderAsync(order.Id, draft.Version); await LoadDataAsync(); managementView.InvalidateData();
                } catch (Exception error) { ShowStatus(error is ManagementException ? error.Message : "Не удалось удалить заказ.", true); }
            };
            menu.Items.Add(delete); menu.PlacementTarget = button; menu.IsOpen = true; e.Handled = true;
        }
    }
}

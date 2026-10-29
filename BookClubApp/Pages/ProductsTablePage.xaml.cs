using BookClubApp.Controls;
using BookClubApp.Services;
using BookClubApp.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace BookClubApp.Pages {
    public sealed class BasketLine {
        public ProductCard Product { get; set; }
        public int Quantity { get; set; }
        public bool Missing { get; set; }
        public decimal Total { get { return Product.Price * Quantity; } }
    }

    public partial class ProductsTablePage : Page {
        private List<ProductCard> products = new List<ProductCard>();
        private readonly List<BasketLine> basket = new List<BasketLine>();
        private readonly LiveDataWatcher watcher;
        private readonly DispatcherTimer noticeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        private bool filling, processing;
        private bool cartWarning;
        private int view;
        private bool IsClient { get { return AuthService.Current != null && AuthService.Current.Role == 1; } }

        public ProductsTablePage() {
            InitializeComponent();
            txtBlockAccount.Text = AuthService.Current == null ? "Гостевой просмотр" : AuthService.Current.DisplayPhone + " · " + AuthService.Current.RoleName;
            btnLogout.Content = AuthService.Current == null ? "Войти" : "Выйти";
            btnMyOrders.Visibility = IsClient ? Visibility.Visible : Visibility.Collapsed;
            txtBlockGuestHint.Visibility = IsClient ? Visibility.Collapsed : Visibility.Visible;
            filling = true;
            comboSort.ItemsSource = new[] { "Популярные", "По названию", "Цена ↑", "Цена ↓" }; comboSort.SelectedIndex = 0;
            comboPickup.ItemsSource = new[] { new { Name = ClientOrderService.PickupName, Address = ClientOrderService.PickupAddress } }; comboPickup.SelectedIndex = 0;
            filling = false;
            watcher = new LiveDataWatcher(AutoRefreshAsync, () => Notice("Нет подключения к базе. Повторная попытка выполняется автоматически.", true, false));
            noticeTimer.Tick += (sender, e) => { statusPanel.Visibility = Visibility.Collapsed; noticeTimer.Stop(); };
            UpdateBasket();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e) { watcher.Start(); }
        private void Page_Unloaded(object sender, RoutedEventArgs e) { watcher.Stop(); noticeTimer.Stop(); }
        private async Task<bool> AutoRefreshAsync() {
            if (!IsLoaded || processing || comboSort.IsDropDownOpen || comboPickup.IsDropDownOpen) return false;
            return await LoadDataAsync();
        }
        private async Task<bool> LoadDataAsync() {
            var result = await DashboardService.LoadCatalogAsync();
            var myOrders = IsClient ? await ClientOrderService.LoadMyOrdersAsync() : new List<OrderRow>();
            string displayPhone = IsClient ? await ClientOrderService.LoadPhoneAsync() : null;
            if (!IsLoaded) return false;
            if (displayPhone != null) txtBlockAccount.Text = displayPhone + " · Клиент";
            products = result;
            foreach (BasketLine line in basket) {
                ProductCard fresh = products.FirstOrDefault(product => product.Id == line.Product.Id);
                line.Missing = fresh == null;
                if (fresh != null) line.Product = fresh;
            }
            string selected = (listCategories.SelectedItem as ListBoxItem)?.Uid ?? "";
            filling = true; listCategories.Items.Clear();
            listCategories.Items.Add(new ListBoxItem { Content = "Все товары", Uid = "", Tag = FindResource("HomeIcon") });
            foreach (var category in products.GroupBy(product => new { product.CategoryId, product.Category }).OrderBy(group => group.Key.Category))
                listCategories.Items.Add(new ListBoxItem { Content = category.Key.Category, Uid = category.Key.CategoryId.ToString(), Tag = FindResource("ProductIcon") });
            listCategories.SelectedItem = listCategories.Items.Cast<ListBoxItem>().FirstOrDefault(item => item.Uid == selected) ?? listCategories.Items[0];
            filling = false;
            gridMyOrders.ItemsSource = myOrders;
            if (statusPanel.Visibility == Visibility.Visible && !noticeTimer.IsEnabled) statusPanel.Visibility = Visibility.Collapsed;
            ApplySearch(); UpdateBasket();
            return true;
        }

        private void Notice(string text, bool error = false, bool transient = true) {
            txtBlockStatus.Text = text;
            statusPanel.Background = (System.Windows.Media.Brush)FindResource(error ? "BackgroundErrorBrush" : "BackgroundSuccessBrush");
            statusPanel.Visibility = Visibility.Visible; noticeTimer.Stop();
            if (transient) { noticeTimer.Interval = TimeSpan.FromSeconds(error ? 6 : 3); noticeTimer.Start(); }
        }

        private void ApplySearch() {
            if (filling || listProducts == null) return;
            string search = txtBoxSearch.Text.Trim();
            string category = (listCategories.SelectedItem as ListBoxItem)?.Uid ?? "";
            var filtered = products.Where(product => (product.Name + " " + product.Author + " " + product.Category).IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0);
            if (category != "") filtered = filtered.Where(product => product.CategoryId.ToString() == category);
            if (comboSort.SelectedIndex == 1) filtered = filtered.OrderBy(product => product.Name);
            if (comboSort.SelectedIndex == 2) filtered = filtered.OrderBy(product => product.Price);
            if (comboSort.SelectedIndex == 3) filtered = filtered.OrderByDescending(product => product.Price);
            var visible = filtered.ToList(); listProducts.ItemsSource = visible;
            txtBlockCount.Text = view == 0 ? "Найдено товаров: " + visible.Count : view == 1 ? "Проверьте состав и сумму перед оформлением" : "Талон понадобится при получении в пункте выдачи №1";
        }
        private void SetView(int value) {
            view = value;
            catalogueScroll.Visibility = value == 0 ? Visibility.Visible : Visibility.Collapsed;
            checkoutScroll.Visibility = value == 1 ? Visibility.Visible : Visibility.Collapsed;
            myOrdersPanel.Visibility = value == 2 ? Visibility.Visible : Visibility.Collapsed;
            comboSort.Visibility = value == 0 ? Visibility.Visible : Visibility.Collapsed;
            txtBlockTitle.Text = value == 0 ? (listCategories.SelectedItem as ListBoxItem)?.Content.ToString() ?? "Все товары" : value == 1 ? "Оформление заказа" : "Мои заказы";
            btnCheckout.Content = value == 1 ? "Оформить · наличные" : "Перейти к оформлению";
            ApplySearch();
        }
        private void txtBoxSearch_TextChanged(object sender, TextChangedEventArgs e) { if (!filling && view != 0) SetView(0); else ApplySearch(); }
        private void comboSort_SelectionChanged(object sender, SelectionChangedEventArgs e) { ApplySearch(); }
        private void listCategories_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (!filling) SetView(0); }
        private void btnBackToProducts_Click(object sender, RoutedEventArgs e) { SetView(0); }
        private void btnMyOrders_Click(object sender, RoutedEventArgs e) { SetView(2); }

        private void UpdateBasket() {
            listCart.ItemsSource = null; listCart.ItemsSource = basket.ToList();
            listCheckout.ItemsSource = null; listCheckout.ItemsSource = basket.ToList();
            decimal total = basket.Sum(line => line.Total);
            decimal original = basket.Sum(line => Math.Max(line.Product.OldPrice ?? line.Product.Price, line.Product.Price) * line.Quantity);
            txtBlockCartCount.Text = "Товаров: " + basket.Sum(line => line.Quantity);
            txtBlockOriginal.Text = original.ToString("N2") + " ₽";
            txtBlockDiscount.Text = "− " + (original - total).ToString("N2") + " ₽";
            txtBlockTotal.Text = total.ToString("N2") + " ₽";
            bool invalid = basket.Any(line => line.Missing || line.Quantity > line.Product.BookCount);
            btnCheckout.IsEnabled = IsClient && basket.Count > 0 && !invalid && !processing;
            if (invalid) { cartWarning = true; Notice("Наличие изменилось. Уменьшите количество или удалите недоступные товары из корзины.", true, false); }
            else if (cartWarning) { cartWarning = false; statusPanel.Visibility = Visibility.Collapsed; }
        }
        private void AddProduct(ProductCard product) {
            if (!IsClient) { Notice("Для покупок войдите в аккаунт клиента.", true); return; }
            BasketLine line = basket.FirstOrDefault(item => item.Product.Id == product.Id);
            if (product.BookCount <= (line?.Quantity ?? 0) || basket.Sum(item => item.Quantity) >= 10000) { Notice("Больше доступных товаров добавить нельзя.", true); return; }
            if (line == null) basket.Add(new BasketLine { Product = product, Quantity = 1 }); else line.Quantity++;
            UpdateBasket();
        }
        private void btnProductMenu_Click(object sender, RoutedEventArgs e) {
            Button button = (Button)sender; ProductCard product = (ProductCard)button.DataContext;
            ContextMenu menu = new ContextMenu();
            MenuItem add = new MenuItem { Header = "Добавить к заказу", IsEnabled = product.BookCount > 0 };
            add.Click += (source, args) => AddProduct(product);
            MenuItem details = new MenuItem { Header = "Подробнее" };
            details.Click += (source, args) => MessageBox.Show(Window.GetWindow(this), product.Name + "\n" + product.Author + "\n\n" + (product.Description ?? "Описание не указано."), "О товаре", MessageBoxButton.OK, MessageBoxImage.Information);
            menu.Items.Add(add); menu.Items.Add(details); menu.PlacementTarget = button; menu.IsOpen = true;
        }
        private void btnDecrease_Click(object sender, RoutedEventArgs e) { var line = (BasketLine)((Button)sender).DataContext; if (line.Quantity > 1) line.Quantity--; else basket.Remove(line); UpdateBasket(); }
        private void btnIncrease_Click(object sender, RoutedEventArgs e) { AddProduct(((BasketLine)((Button)sender).DataContext).Product); }
        private void btnRemoveCart_Click(object sender, RoutedEventArgs e) { basket.Remove((BasketLine)((Button)sender).DataContext); UpdateBasket(); }
        private void btnClearCart_Click(object sender, RoutedEventArgs e) { basket.Clear(); UpdateBasket(); }

        private async void btnCheckout_Click(object sender, RoutedEventArgs e) {
            if (view != 1) { SetView(1); return; }
            if (processing || !IsClient || basket.Count == 0) return;
            processing = true; IsEnabled = false;
            try {
                var dialog = new PaymentWindow(basket.Select(line => new CartRequestLine { ProductId = line.Product.Id, Quantity = line.Quantity }).ToList(), basket.Sum(line => line.Total)) { Owner = Window.GetWindow(this) };
                if (dialog.ShowDialog() != true) return;
                int id = dialog.OrderId;
                basket.Clear(); UpdateBasket(); SetView(2); await LoadDataAsync();
                Notice("Заказ #" + id + " оформлен. Оплата наличными при получении.");
                await ReceiptExport.SaveAsync(Window.GetWindow(this), id);
            } catch (Exception error) {
                Notice(error is ManagementException || error is ArgumentException || error is UnauthorizedAccessException ? error.Message : "Не удалось завершить оформление. Проверьте «Мои заказы» перед повторной попыткой.", true);
            } finally { processing = false; IsEnabled = true; UpdateBasket(); }
        }
        private void btnLogout_Click(object sender, RoutedEventArgs e) { ((AuthWindow)Window.GetWindow(this)).ShowLogin(); }
        private async void btnReceipt_Click(object sender, RoutedEventArgs e) {
            if (processing) return;
            OrderRow order = ((Button)sender).DataContext as OrderRow;
            if (order == null) return;
            processing = true;
            try { await ReceiptExport.SaveAsync(Window.GetWindow(this), order.Id); }
            finally { processing = false; }
        }
    }
}

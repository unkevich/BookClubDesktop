using BookClubApp.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace BookClubApp.Pages {
    public partial class ProductsTablePage : Page {
        private List<ProductCard> products;
        private bool initialized;
        private bool loading;

        public ProductsTablePage() {
            InitializeComponent();
            txtBlockAccount.Text = AuthService.Current == null ? "Гостевой просмотр" : AuthService.Current.NumberPhone + " · " + AuthService.Current.RoleName;
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e) {
            if (initialized) return;
            initialized = true;
            await LoadProductsAsync();
        }

        private async Task LoadProductsAsync() {
            if (loading) return;
            loading = true;
            btnRefresh.IsEnabled = false;
            txtBlockStatus.Text = "Загружаем товары…";
            try {
                var result = await DashboardService.LoadCatalogAsync();
                if (!IsLoaded) return;
                products = result;
                ApplySearch();
            } catch (Exception) {
                if (IsLoaded) txtBlockStatus.Text = "Не удалось загрузить товары. Проверьте подключение к базе и нажмите «Обновить».";
            } finally {
                loading = false;
                btnRefresh.IsEnabled = true;
            }
        }

        private void ApplySearch() {
            if (products == null) return;
            string search = txtBoxSearch.Text.Trim();
            var filtered = products.Where(product => (product.Name + " " + product.Author + " " + product.Category)
                .IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0).ToList();
            listProducts.ItemsSource = filtered;
            txtBlockStatus.Text = filtered.Count == 0 ? "Товары не найдены" : "Найдено товаров: " + filtered.Count;
        }

        private void txtBoxSearch_TextChanged(object sender, TextChangedEventArgs e) { ApplySearch(); }
        private async void btnRefresh_Click(object sender, RoutedEventArgs e) { await LoadProductsAsync(); }
        private void btnLogout_Click(object sender, RoutedEventArgs e) { ((AuthWindow)Window.GetWindow(this)).ShowLogin(); }
    }
}
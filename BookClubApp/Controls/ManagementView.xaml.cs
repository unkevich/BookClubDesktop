using BookClubApp.Entity;
using BookClubApp.Helpers;
using BookClubApp.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Path = System.Windows.Shapes.Path;
using System.Windows.Threading;

namespace BookClubApp.Controls {
    public partial class ManagementView : UserControl {
        private DashboardData data;
        private List<Category> categories = new List<Category>();
        private int mode;
        private int page = 1;
        private int pageCount = 1;
        private bool filling;
        private bool busy;
        private bool dirty;
        private bool tableMode;
        private int pendingOpen;
        private string globalSearch = "";
        private ProductDraft productDraft;
        private ClientDraft clientDraft;
        private OrderDraft orderDraft;
        private string originalLines;
        private decimal originalAmount;
        private byte[] imageBytes;
        private readonly DispatcherTimer toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        public event EventHandler RecordsChanged;
        public ICommand ProductMenuCommand { get; private set; }
        public ICommand OpenProductCommand { get; private set; }

        public ManagementView() {
            AuthService.RequireStaff();
            ProductMenuCommand = new UiCommand(value => OpenRecordMenu((Button)value));
            OpenProductCommand = new UiCommand(value => BeginEdit(((ProductCard)value).Id));
            InitializeComponent();
            comboPageSize.ItemsSource = new[] { 10, 20, 50 }; comboPageSize.SelectedIndex = 0;
            NumericInput.Attach(txtBoxClientPhone); NumericInput.Attach(txtBoxStock);
            NumericInput.Attach(txtBoxMinStock); NumericInput.Attach(txtBoxQuantity);
            AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler(Editor_Changed));
            AddHandler(System.Windows.Controls.Primitives.Selector.SelectionChangedEvent, new SelectionChangedEventHandler(Editor_Changed));
            AddHandler(PasswordBox.PasswordChangedEvent, new RoutedEventHandler(Editor_Changed));
            AddHandler(DatePicker.SelectedDateChangedEvent, new EventHandler<SelectionChangedEventArgs>(Editor_Changed));
            toastTimer.Tick += (sender, e) => { messagePanel.Visibility = Visibility.Collapsed; toastTimer.Stop(); };
            Unloaded += (sender, e) => toastTimer.Stop();
        }

        private void Editor_Changed(object sender, RoutedEventArgs e) {
            DependencyObject source = e.OriginalSource as DependencyObject;
            if (!filling && editorPanel.Visibility == Visibility.Visible && source != null && editorPanel.IsAncestorOf(source)) dirty = true;
        }

        private bool ConfirmClose() {
            return !dirty || MessageBox.Show(Window.GetWindow(this), "Закрыть панель без сохранения изменений?", "Несохранённые изменения", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        }

        public bool TryLeave() {
            if (busy || !ConfirmClose()) return false;
            CloseEditor(); return true;
        }

        public void OpenWhenReady(int id) {
            if (busy) pendingOpen = id;
            else BeginEdit(id);
        }

        public bool SelectMode(int value) {
            if (mode == value) {
                if (busy) return false;
                if (data == null && IsLoaded) Run(() => ReloadAsync());
                return true;
            }
            if (busy || !ConfirmClose()) return false;
            CloseEditor(); mode = value; page = 1;
            ConfigureFilters();
            if (data != null) ApplyFilters();
            else if (IsLoaded) Run(() => ReloadAsync());
            return true;
        }

        private void ConfigureFilters() {
            filling = true;
            txtBoxLocalSearch.Clear();
            bool products = mode == 2, orders = mode == 1;
            searchContainer.Width = products ? 190 : 215; comboSort.Width = products ? 130 : 160;
            productScroll.Visibility = products && !tableMode ? Visibility.Visible : Visibility.Collapsed;
            gridProducts.Visibility = products && tableMode ? Visibility.Visible : Visibility.Collapsed;
            productViewButtons.Visibility = products ? Visibility.Visible : Visibility.Collapsed;
            gridOrders.Visibility = orders ? Visibility.Visible : Visibility.Collapsed;
            gridClients.Visibility = mode == 3 ? Visibility.Visible : Visibility.Collapsed;
            comboCategoryFilter.Visibility = comboStockFilter.Visibility = comboPriceFilter.Visibility = products ? Visibility.Visible : Visibility.Collapsed;
            comboStatusFilter.Visibility = dateFilterFrom.Visibility = dateFilterTo.Visibility = orders ? Visibility.Visible : Visibility.Collapsed;
            comboStockFilter.ItemsSource = new[] { "Все остатки", "В наличии", "Мало", "Нет в наличии" };
            comboPriceFilter.ItemsSource = new[] { "Любая цена", "До 500 ₽", "500–1000 ₽", "От 1000 ₽" }; comboPriceFilter.SelectedIndex = 0;
            comboStatusFilter.ItemsSource = new[] { "Все статусы" }.Concat(ManagementService.OrderStatuses).Concat(data == null ? new string[0] : data.Orders.Select(order => order.Status)).Distinct().ToList();
            comboStatusFilter.SelectedIndex = 0; comboStockFilter.SelectedIndex = 0;
            comboSort.ItemsSource = products ? new[] { "По названию", "Цена ↑", "Цена ↓", "По остатку" } : new[] { "Сначала новые", "Сначала старые" };
            comboSort.SelectedIndex = 0;
            dateFilterFrom.SelectedDate = dateFilterTo.SelectedDate = null;
            BuildTabs(); filling = false;
        }

        private void BuildTabs() {
            bool previous = filling; filling = true;
            listTabs.Items.Clear();
            int count = data == null ? 0 : mode == 1 ? data.Orders.Count : mode == 2 ? data.Products.Count : data.Clients.Count;
            listTabs.Items.Add(new ListBoxItem { Content = (mode == 2 ? "Все товары" : mode == 3 ? "Все клиенты" : "Все") + "  " + count, Tag = "" });
            if (mode == 1) foreach (string status in ManagementService.OrderStatuses) listTabs.Items.Add(new ListBoxItem {
                Content = status + "  " + (data == null ? 0 : data.Orders.Count(order => order.Status == status)), Tag = status });
            if (mode == 2) foreach (Category category in categories) listTabs.Items.Add(new ListBoxItem {
                Content = category.Name + "  " + (data == null ? 0 : data.Products.Count(product => product.CategoryId == category.Id)), Tag = category.Id.ToString() });
            listTabs.SelectedIndex = 0; filling = previous;
        }

        public void SetSearch(string value) {
            value = value ?? ""; if (value == globalSearch) return;
            globalSearch = value; page = 1; ApplyFilters();
        }
        public void InvalidateData() { data = null; }

        private bool Matches(params string[] values) {
            string text = string.Join(" ", values);
            return text.IndexOf(globalSearch.Trim(), StringComparison.CurrentCultureIgnoreCase) >= 0
                && text.IndexOf(txtBoxLocalSearch.Text.Trim(), StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        private void ApplyFilters() {
            if (data == null || filling || comboPageSize.SelectedItem == null) return;
            int? selectedOrder = (gridOrders.SelectedItem as OrderRow)?.Id;
            int? selectedClient = (gridClients.SelectedItem as ClientRow)?.Id;
            int? selectedProduct = (gridProducts.SelectedItem as ProductCard)?.Id;
            int size = (int)comboPageSize.SelectedItem;
            string tab = listTabs.SelectedItem == null ? "" : (string)((ListBoxItem)listTabs.SelectedItem).Tag;
            int count;
            if (mode == 2) {
                var products = data.Products.Where(product => Matches(product.Name, product.Author, product.Category));
                Category selected = comboCategoryFilter.SelectedItem as Category;
                if (selected != null && selected.Id != 0) products = products.Where(product => product.CategoryId == selected.Id);
                if (!string.IsNullOrEmpty(tab)) products = products.Where(product => product.CategoryId.ToString() == tab);
                if (comboStockFilter.SelectedIndex == 1) products = products.Where(product => product.BookCount > 0);
                if (comboStockFilter.SelectedIndex == 2) products = products.Where(product => product.BookCount <= product.MinBookCount);
                if (comboStockFilter.SelectedIndex == 3) products = products.Where(product => product.BookCount == 0);
                if (comboPriceFilter.SelectedIndex == 1) products = products.Where(product => product.Price < 500);
                if (comboPriceFilter.SelectedIndex == 2) products = products.Where(product => product.Price >= 500 && product.Price < 1000);
                if (comboPriceFilter.SelectedIndex == 3) products = products.Where(product => product.Price >= 1000);
                products = comboSort.SelectedIndex == 1 ? products.OrderBy(product => product.Price) : comboSort.SelectedIndex == 2 ? products.OrderByDescending(product => product.Price)
                    : comboSort.SelectedIndex == 3 ? products.OrderBy(product => product.BookCount) : products.OrderBy(product => product.Name);
                count = products.Count(); ClampPage(count, size); var visible = products.Skip((page - 1) * size).Take(size).ToList();
                listProducts.ItemsSource = visible; gridProducts.ItemsSource = visible;
            } else if (mode == 1) {
                var orders = data.Orders.Where(order => Matches(order.Number, order.Phone, order.Status, order.Talon));
                if (!string.IsNullOrEmpty(tab)) orders = orders.Where(order => order.Status == tab);
                if (comboStatusFilter.SelectedIndex > 0) orders = orders.Where(order => order.Status == (string)comboStatusFilter.SelectedItem);
                if (dateFilterFrom.SelectedDate.HasValue) orders = orders.Where(order => order.Date.Date >= dateFilterFrom.SelectedDate.Value.Date);
                if (dateFilterTo.SelectedDate.HasValue) orders = orders.Where(order => order.Date.Date <= dateFilterTo.SelectedDate.Value.Date);
                orders = comboSort.SelectedIndex == 1 ? orders.OrderBy(order => order.Date).ThenBy(order => order.Id) : orders.OrderByDescending(order => order.Date).ThenByDescending(order => order.Id);
                count = orders.Count(); ClampPage(count, size); gridOrders.ItemsSource = orders.Skip((page - 1) * size).Take(size).ToList();
            } else {
                var clients = data.Clients.Where(client => Matches(client.NumberPhone, client.Id.ToString()));
                clients = comboSort.SelectedIndex == 1 ? clients.OrderBy(client => client.Created_at) : clients.OrderByDescending(client => client.Created_at);
                count = clients.Count(); ClampPage(count, size); gridClients.ItemsSource = clients.Skip((page - 1) * size).Take(size).ToList();
            }
            txtBlockEmpty.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
            txtBlockPage.Text = page + " / " + pageCount + " · " + count;
            if (selectedOrder.HasValue) gridOrders.SelectedItem = gridOrders.Items.Cast<OrderRow>().FirstOrDefault(order => order.Id == selectedOrder.Value);
            if (selectedClient.HasValue) gridClients.SelectedItem = gridClients.Items.Cast<ClientRow>().FirstOrDefault(client => client.Id == selectedClient.Value);
            if (selectedProduct.HasValue) gridProducts.SelectedItem = gridProducts.Items.Cast<ProductCard>().FirstOrDefault(product => product.Id == selectedProduct.Value);
        }

        private void ClampPage(int count, int size) { pageCount = Math.Max(1, (count + size - 1) / size); page = Math.Min(pageCount, Math.Max(1, page)); }

        private async Task ReloadAsync(bool preserveState = false) {
            DashboardData result = await DashboardService.LoadAsync();
            var newCategories = await ManagementService.LoadCategoriesAsync();
            if (!IsLoaded) return;
            int categoryFilter = (comboCategoryFilter.SelectedItem as Category)?.Id ?? 0;
            string tab = (listTabs.SelectedItem as ListBoxItem)?.Tag as string;
            object selectedCategory = comboProductCategory.SelectedValue, selectedClient = comboOrderClient.SelectedValue, selectedProduct = comboOrderProduct.SelectedValue;
            data = result; categories = newCategories;
            filling = true;
            comboCategoryFilter.ItemsSource = new[] { new Category { Id = 0, Name = "Все категории" } }.Concat(categories).ToList(); comboCategoryFilter.SelectedIndex = 0;
            if (preserveState) comboCategoryFilter.SelectedItem = ((IEnumerable<Category>)comboCategoryFilter.ItemsSource).FirstOrDefault(category => category.Id == categoryFilter) ?? ((IEnumerable<Category>)comboCategoryFilter.ItemsSource).First();
            comboProductCategory.ItemsSource = categories;
            comboOrderClient.ItemsSource = data.Clients;
            comboOrderProduct.ItemsSource = data.Products.OrderBy(product => product.Name).ToList();
            if (productDraft != null) comboProductCategory.SelectedValue = preserveState ? selectedCategory : productDraft.CategoryId;
            if (orderDraft != null) comboOrderClient.SelectedValue = preserveState ? selectedClient : orderDraft.ClientId;
            comboOrderProduct.SelectedValue = selectedProduct;
            BuildTabs();
            if (preserveState) listTabs.SelectedItem = listTabs.Items.Cast<ListBoxItem>().FirstOrDefault(item => (string)item.Tag == tab) ?? listTabs.Items[0];
            filling = false; ApplyFilters();
        }

        public async Task<bool> AutoReloadAsync() {
            if (busy || !IsLoaded) return false;
            if (new[] { comboCategoryFilter, comboStockFilter, comboPriceFilter, comboStatusFilter, comboSort, comboPageSize, comboProductCategory, comboOrderClient, comboOrderProduct, comboOrderStatus, comboPayment }.Any(combo => combo.IsDropDownOpen)) return false;
            if (dateFilterFrom.IsDropDownOpen || dateFilterTo.IsDropDownOpen || dateClientCreated.IsDropDownOpen || dateOrder.IsDropDownOpen) return false;
            busy = true;
            try {
                await ReloadAsync(true);
                if (orderDraft != null) {
                    foreach (OrderLineDraft line in orderDraft.Lines) {
                        ProductCard product = data.Products.FirstOrDefault(item => item.Id == line.ProductId);
                        if (product != null) { line.Price = product.Price; line.Name = product.Name; }
                    }
                    UpdateLines();
                }
                string version = null;
                ProductDraft freshProduct = null; ClientDraft freshClient = null; OrderDraft freshOrder = null;
                if (productDraft != null && productDraft.Id != 0) { freshProduct = await ManagementService.LoadProductAsync(productDraft.Id); version = freshProduct.Version; }
                if (clientDraft != null && clientDraft.Id != 0) { freshClient = await ManagementService.LoadClientAsync(clientDraft.Id); version = freshClient.Version; }
                if (orderDraft != null && orderDraft.Id != 0) { freshOrder = await ManagementService.LoadOrderAsync(orderDraft.Id); version = freshOrder.Version; }
                string original = productDraft?.Version ?? clientDraft?.Version ?? orderDraft?.Version;
                if (version != null && version != original) {
                    if (dirty) { txtBlockEditorError.Text = "Запись изменена в другом окне. Ваш ввод сохранён; перед записью откройте свежую версию."; txtBlockEditorError.Visibility = Visibility.Visible; }
                    else if (freshProduct != null) ShowProduct(freshProduct);
                    else if (freshClient != null) ShowClient(freshClient);
                    else if (freshOrder != null) ShowOrder(freshOrder);
                }
                return true;
            } catch (ManagementException error) { txtBlockEditorError.Text = error.Message; txtBlockEditorError.Visibility = Visibility.Visible; return true; }
            finally { busy = false; }
        }

        private async Task ReloadAfterChangeAsync(string message) {
            CloseEditor(); RecordsChanged?.Invoke(this, EventArgs.Empty);
            bool refreshed = true;
            try { await ReloadAsync(); }
            catch (Exception) { refreshed = false; message += " Список обновится автоматически после восстановления связи."; }
            txtBlockMessage.Text = message;
            messagePanel.Background = (Brush)FindResource(refreshed ? "BackgroundSuccessBrush" : "BackgroundWarnBrush");
            messagePanel.Visibility = Visibility.Visible;
            toastTimer.Stop(); toastTimer.Start();
        }

        private async void Run(Func<Task> action) {
            if (busy) return;
            busy = true; IsEnabled = false;
            toastTimer.Stop();
            messagePanel.Visibility = Visibility.Collapsed; txtBlockEditorError.Visibility = Visibility.Collapsed;
            try { AuthService.RequireStaff(); await action(); }
            catch (Exception error) {
                string message = error is ArgumentException || error is ManagementException || error is UnauthorizedAccessException ? error.Message : "Не удалось выполнить операцию. Проверьте подключение к базе и повторите попытку.";
                if (editorPanel.Visibility == Visibility.Visible) { txtBlockEditorError.Text = message; txtBlockEditorError.Visibility = Visibility.Visible; }
                else { txtBlockMessage.Text = message; messagePanel.Background = (Brush)FindResource("BackgroundErrorBrush"); messagePanel.Visibility = Visibility.Visible; }
            } finally {
                busy = false; IsEnabled = true;
                if (pendingOpen != 0) { int id = pendingOpen; pendingOpen = 0; BeginEdit(id); }
            }
        }

        public void Refresh() { if (!ConfirmClose()) return; CloseEditor(); Run(() => ReloadAsync()); }
        private void View_Loaded(object sender, RoutedEventArgs e) { if (mode != 0 && data == null) Run(() => ReloadAsync()); }
        private void Filters_Changed(object sender, RoutedEventArgs e) { if (filling) return; page = 1; ApplyFilters(); }
        private void listTabs_SelectionChanged(object sender, SelectionChangedEventArgs e) { Filters_Changed(sender, e); }
        private void PageSize_Changed(object sender, SelectionChangedEventArgs e) { if (comboPageSize == null) return; page = 1; ApplyFilters(); }
        private void btnPrevious_Click(object sender, RoutedEventArgs e) { page--; ApplyFilters(); }
        private void btnNext_Click(object sender, RoutedEventArgs e) { page++; ApplyFilters(); }
        private void btnCards_Click(object sender, RoutedEventArgs e) { tableMode = false; productScroll.Visibility = Visibility.Visible; gridProducts.Visibility = Visibility.Collapsed; }
        private void btnTable_Click(object sender, RoutedEventArgs e) { tableMode = true; productScroll.Visibility = Visibility.Collapsed; gridProducts.Visibility = Visibility.Visible; }
        private void btnReset_Click(object sender, RoutedEventArgs e) {
            filling = true; txtBoxLocalSearch.Clear(); comboCategoryFilter.SelectedIndex = 0; comboStockFilter.SelectedIndex = 0;
            comboStatusFilter.SelectedIndex = 0; comboPriceFilter.SelectedIndex = 0; comboSort.SelectedIndex = 0; listTabs.SelectedIndex = 0;
            dateFilterFrom.SelectedDate = dateFilterTo.SelectedDate = null; filling = false; page = 1; ApplyFilters();
        }

        private void ShowEditor(string title) {
            filling = true; txtBlockEditorTitle.Text = title; txtBlockEditorError.Visibility = Visibility.Collapsed;
            productEditor.Visibility = mode == 2 ? Visibility.Visible : Visibility.Collapsed;
            orderEditor.Visibility = mode == 1 ? Visibility.Visible : Visibility.Collapsed;
            clientEditor.Visibility = mode == 3 ? Visibility.Visible : Visibility.Collapsed;
            editorColumn.Width = new GridLength(350); editorPanel.Visibility = Visibility.Visible; dirty = false;
        }

        private void CloseEditor() {
            editorColumn.Width = new GridLength(0); editorPanel.Visibility = Visibility.Collapsed;
            productDraft = null; clientDraft = null; orderDraft = null; imageBytes = null;
            pwdBoxClientPassword.Clear(); dirty = false;
        }

        public void BeginAdd() {
            if (!ConfirmClose()) return;
            Run(async () => {
                if (data == null) await ReloadAsync();
                CloseEditor();
                if (mode == 2) ShowProduct(new ProductDraft { Name = "", Author = "", BookCount = 0, MinBookCount = 0 });
                else if (mode == 3) ShowClient(new ClientDraft { CreatedAt = DateTime.Now, Phone = "" });
                else ShowOrder(new OrderDraft { Date = DateTime.Now, Status = "Новый", Payment = "Наличные", Talon = "Т-" + DateTime.Now.ToString("yyMMddHHmmss"), Lines = new List<OrderLineDraft>() });
            });
        }

        public void BeginEdit(int id) {
            if (!ConfirmClose()) return;
            Run(async () => {
                if (data == null) await ReloadAsync();
                if (mode == 2) ShowProduct(await ManagementService.LoadProductAsync(id));
                else if (mode == 3) ShowClient(await ManagementService.LoadClientAsync(id));
                else ShowOrder(await ManagementService.LoadOrderAsync(id));
            });
        }

        private void ShowProduct(ProductDraft draft) {
            CloseEditor(); productDraft = draft; ShowEditor(draft.Id == 0 ? "Добавить товар" : draft.Name);
            txtBoxProductName.Text = draft.Name; txtBoxAuthor.Text = draft.Author; txtBoxDescription.Text = draft.Description;
            comboProductCategory.SelectedValue = draft.CategoryId; txtBoxNewCategory.Clear();
            txtBoxPrice.Text = draft.Price.ToString("0.####"); txtBoxOldPrice.Text = draft.OldPrice.HasValue ? draft.OldPrice.Value.ToString("0.####") : "";
            txtBoxStock.Text = draft.BookCount.ToString(); txtBoxMinStock.Text = draft.MinBookCount.ToString();
            imageBytes = draft.ImagePreview; imagePreview.Source = new ProductCard { ImagePreview = imageBytes }.Cover;
            filling = false; dirty = false;
        }

        private void ShowClient(ClientDraft draft) {
            CloseEditor(); clientDraft = draft; ShowEditor(draft.Id == 0 ? "Добавить клиента" : "Клиент #" + draft.Id);
            txtBoxClientPhone.Text = draft.Phone; pwdBoxClientPassword.Clear(); dateClientCreated.SelectedDate = draft.CreatedAt;
            txtBlockPasswordLabel.Text = draft.Id == 0 ? "Пароль" : "Новый пароль (необязательно)";
            filling = false; dirty = false;
        }

        private void ShowOrder(OrderDraft draft) {
            CloseEditor(); orderDraft = draft; ShowEditor(draft.Id == 0 ? "Создать заказ" : "Заказ #" + draft.Id);
            comboOrderClient.SelectedValue = draft.ClientId; dateOrder.SelectedDate = draft.Date; txtBoxOrderTime.Text = draft.Date.ToString("HH:mm");
            comboOrderStatus.ItemsSource = ManagementService.OrderStatuses.Concat(new[] { draft.Status }).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().ToList(); comboOrderStatus.SelectedItem = draft.Status;
            comboPayment.ItemsSource = ManagementService.Payments.Concat(new[] { draft.Payment }).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().ToList(); comboPayment.SelectedItem = draft.Payment;
            txtBoxTalon.Text = draft.Talon; txtBoxQuantity.Text = "1"; comboOrderProduct.SelectedIndex = -1;
            originalLines = LineSignature(draft.Lines); originalAmount = draft.Amount;
            UpdateLines(); filling = false; dirty = false;
        }

        private static string LineSignature(List<OrderLineDraft> lines) { return string.Join(";", lines.OrderBy(line => line.ProductId).Select(line => line.ProductId + ":" + line.Quantity)); }
        private void UpdateLines() {
            listOrderLines.ItemsSource = null; listOrderLines.ItemsSource = orderDraft.Lines;
            bool changed = orderDraft.Id == 0 || LineSignature(orderDraft.Lines) != originalLines;
            decimal total = changed ? orderDraft.Lines.Sum(line => line.Total) : originalAmount;
            txtBlockOrderTotal.Text = "Итого: " + total.ToString("N2") + " ₽";
            txtBlockAmountHint.Text = changed ? "Сумма будет рассчитана по текущим ценам товаров." : "Сумма сохранённого заказа. Изменение состава пересчитает её по текущим ценам.";
        }

        private static decimal ParseMoney(string text, string label) {
            decimal value;
            if (!decimal.TryParse(text.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value)) throw new ArgumentException("Проверьте поле «" + label + "».");
            return value;
        }
        private static int ParseCount(string text, string label) {
            int value; if (!int.TryParse(text, out value) || value < 0) throw new ArgumentException("Проверьте поле «" + label + "»."); return value;
        }

        private void btnSave_Click(object sender, RoutedEventArgs e) {
            Run(async () => {
                if (mode == 2) {
                    productDraft.Name = txtBoxProductName.Text; productDraft.Author = txtBoxAuthor.Text; productDraft.Description = txtBoxDescription.Text;
                    productDraft.CategoryId = comboProductCategory.SelectedValue is int ? (int)comboProductCategory.SelectedValue : 0; productDraft.NewCategory = txtBoxNewCategory.Text;
                    productDraft.Price = ParseMoney(txtBoxPrice.Text, "Цена"); productDraft.OldPrice = string.IsNullOrWhiteSpace(txtBoxOldPrice.Text) ? (decimal?)null : ParseMoney(txtBoxOldPrice.Text, "Старая цена");
                    productDraft.BookCount = ParseCount(txtBoxStock.Text, "На складе"); productDraft.MinBookCount = ParseCount(txtBoxMinStock.Text, "Минимальный остаток"); productDraft.ImagePreview = imageBytes;
                    await ManagementService.SaveProductAsync(productDraft);
                } else if (mode == 3) {
                    if (!dateClientCreated.SelectedDate.HasValue) throw new ArgumentException("Укажите дату регистрации.");
                    clientDraft.Phone = txtBoxClientPhone.Text; clientDraft.Password = pwdBoxClientPassword.Password;
                    clientDraft.CreatedAt = dateClientCreated.SelectedDate.Value.Date + clientDraft.CreatedAt.TimeOfDay;
                    await ManagementService.SaveClientAsync(clientDraft);
                } else {
                    if (!dateOrder.SelectedDate.HasValue) throw new ArgumentException("Укажите дату заказа.");
                    DateTime time; if (!DateTime.TryParseExact(txtBoxOrderTime.Text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time)) throw new ArgumentException("Время указывается в формате ЧЧ:ММ.");
                    orderDraft.ClientId = comboOrderClient.SelectedValue is int ? (int)comboOrderClient.SelectedValue : 0;
                    DateTime enteredDate = dateOrder.SelectedDate.Value.Date + time.TimeOfDay;
                    if (orderDraft.Date.Date != enteredDate.Date || orderDraft.Date.ToString("HH:mm") != txtBoxOrderTime.Text) orderDraft.Date = enteredDate;
                    orderDraft.Status = comboOrderStatus.SelectedItem as string; orderDraft.Payment = comboPayment.SelectedItem as string; orderDraft.Talon = txtBoxTalon.Text;
                    await ManagementService.SaveOrderAsync(orderDraft);
                }
                await ReloadAfterChangeAsync("Изменения сохранены.");
            });
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e) { if (ConfirmClose()) CloseEditor(); }
        private void btnAddLine_Click(object sender, RoutedEventArgs e) {
            try {
                ProductCard product = comboOrderProduct.SelectedItem as ProductCard;
                if (product == null) throw new ArgumentException("Выберите товар.");
                int quantity = ParseCount(txtBoxQuantity.Text, "Количество");
                if (quantity < 1 || quantity > 9999) throw new ArgumentException("Количество: от 1 до 9999.");
                OrderLineDraft existing = orderDraft.Lines.FirstOrDefault(line => line.ProductId == product.Id);
                if (existing != null && existing.Quantity + quantity > 9999) throw new ArgumentException("В позиции может быть не более 9999 единиц.");
                if (existing == null) orderDraft.Lines.Add(new OrderLineDraft { ProductId = product.Id, Name = product.Name, Price = product.Price, Quantity = quantity });
                else existing.Quantity += quantity;
                dirty = true; UpdateLines(); txtBlockEditorError.Visibility = Visibility.Collapsed;
            } catch (ArgumentException error) { txtBlockEditorError.Text = error.Message; txtBlockEditorError.Visibility = Visibility.Visible; }
        }
        private void btnRemoveLine_Click(object sender, RoutedEventArgs e) { orderDraft.Lines.Remove((OrderLineDraft)((Button)sender).DataContext); dirty = true; UpdateLines(); }

        private void btnImage_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog dialog = new OpenFileDialog { Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp;*.gif" };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
            try {
                if (new FileInfo(dialog.FileName).Length > 5 * 1024 * 1024) throw new ArgumentException("Размер обложки не должен превышать 5 МБ.");
                byte[] bytes = File.ReadAllBytes(dialog.FileName); var cover = new ProductCard { ImagePreview = bytes }.Cover;
                if (cover == null) throw new ArgumentException("Не удалось прочитать изображение.");
                imageBytes = bytes; imagePreview.Source = cover; dirty = true;
            } catch (Exception error) { txtBlockEditorError.Text = error is ArgumentException ? error.Message : "Не удалось открыть изображение."; txtBlockEditorError.Visibility = Visibility.Visible; }
        }
        private void btnRemoveImage_Click(object sender, RoutedEventArgs e) { imageBytes = null; imagePreview.Source = null; dirty = true; }

        private MenuItem MenuAction(string header, string geometry, Action action, bool danger = false) {
            Brush brush = (Brush)FindResource(danger ? "MainBrush" : "TextMainBrush");
            MenuItem item = new MenuItem { Header = header, Foreground = brush, Icon = new Path { Data = Geometry.Parse(geometry), Stroke = brush, StrokeThickness = 1.3, Stretch = Stretch.Uniform } };
            item.Click += (sender, e) => action(); return item;
        }

        private ContextMenu BuildRecordMenu(object record) {
            int id = record is ProductCard ? ((ProductCard)record).Id : record is OrderRow ? ((OrderRow)record).Id : ((ClientRow)record).Id;
            ContextMenu menu = new ContextMenu();
            menu.Items.Add(MenuAction("Редактировать", "M 1,15 L 3,9 13,0 17,4 7,14 Z M 3,9 L 7,14", () => BeginEdit(id)));
            if (record is OrderRow) {
                OrderRow order = (OrderRow)record;
                MenuItem statuses = MenuAction("Изменить статус", "M 1,4 L 5,8 15,0 M 1,13 L 5,17 15,9", () => { });
                foreach (string status in ManagementService.OrderStatuses) {
                    string selectedStatus = status;
                    MenuItem statusItem = new MenuItem { Header = status, IsChecked = status == order.Status, Icon = new Ellipse { Width = 8, Height = 8, Fill = new OrderRow { Status = status }.StatusBrush } };
                    statusItem.Click += (sender, e) => Run(async () => {
                        if (!ConfirmClose()) return;
                        await ManagementService.ChangeStatusAsync(id, selectedStatus); await ReloadAfterChangeAsync("Статус изменён.");
                    });
                    statuses.Items.Add(statusItem);
                }
                menu.Items.Add(statuses);
                menu.Items.Add(MenuAction("Сохранить чек (PDF)", "M 3,1 L 12,1 17,6 17,20 3,20 Z M 12,1 L 12,6 17,6 M 6,11 L 14,11 M 6,15 L 14,15", () => Run(async () => {
                    await ReceiptExport.SaveAsync(Window.GetWindow(this), id);
                })));
            }
            menu.Items.Add(new Separator { Style = (Style)FindResource("MenuSeparatorStyle") });
            menu.Items.Add(MenuAction("Удалить", "M 1,4 L 17,4 M 5,4 L 5,1 13,1 13,4 M 3,4 L 4,19 14,19 15,4 M 7,7 L 7,16 M 11,7 L 11,16", () => DeleteRecord(id), true));
            return menu;
        }

        private void OpenRecordMenu(Button button) {
            ContextMenu menu = BuildRecordMenu(button.DataContext);
            menu.PlacementTarget = button; menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom; menu.IsOpen = true;
        }

        private void DeleteRecord(int id) {
            if (!ConfirmClose()) return;
            Run(async () => {
                string version, name;
                if (mode == 2) { var draft = await ManagementService.LoadProductAsync(id); version = draft.Version; name = "товар «" + draft.Name + "»"; }
                else if (mode == 3) { var draft = await ManagementService.LoadClientAsync(id); version = draft.Version; name = "клиента " + PhoneNumber.Format(draft.Phone); }
                else { var draft = await ManagementService.LoadOrderAsync(id); version = draft.Version; name = "заказ #" + id; }
                string text = "Удалить " + name + "?" + (mode == 1 ? " Товары неотменённого заказа будут возвращены на склад." : "");
                if (MessageBox.Show(Window.GetWindow(this), text, "Удаление", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                if (mode == 2) await ManagementService.DeleteProductAsync(id, version);
                else if (mode == 3) await ManagementService.DeleteClientAsync(id, version);
                else await ManagementService.DeleteOrderAsync(id, version);
                await ReloadAfterChangeAsync("Запись удалена.");
            });
        }

        private void btnRowMenu_Click(object sender, RoutedEventArgs e) { OpenRecordMenu((Button)sender); e.Handled = true; }
        private void gridRow_Click(object sender, MouseButtonEventArgs e) {
            DependencyObject source = e.OriginalSource as DependencyObject;
            while (source != null && !(source is DataGridRow)) {
                if (source is Button) return;
                source = source is Visual || source is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
            }
            DataGridRow row = source as DataGridRow;
            if (row == null) return;
            if (row.Item is ProductCard) {
                int id = ((ProductCard)row.Item).Id;
                if (productDraft == null || productDraft.Id != id) BeginEdit(id);
            } else if (row.Item is ClientRow) {
                int id = ((ClientRow)row.Item).Id;
                if (clientDraft == null || clientDraft.Id != id) BeginEdit(id);
            } else if (row.Item is OrderRow) {
                int id = ((OrderRow)row.Item).Id;
                if (orderDraft == null || orderDraft.Id != id) BeginEdit(id);
            }
        }
    }
}

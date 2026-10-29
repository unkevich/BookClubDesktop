using BookClubApp.Entity;
using BookClubApp.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BookClubApp.Services {
    public sealed class ManagementException : InvalidOperationException {
        public ManagementException(string message) : base(message) { }
    }
    public sealed class ProductDraft {
        public int Id { get; set; }
        public string Version { get; set; }
        public string Name { get; set; }
        public string Author { get; set; }
        public string Description { get; set; }
        public int CategoryId { get; set; }
        public string NewCategory { get; set; }
        public decimal Price { get; set; }
        public decimal? OldPrice { get; set; }
        public int BookCount { get; set; }
        public int MinBookCount { get; set; }
        public byte[] ImagePreview { get; set; }
    }

    public sealed class ClientDraft {
        public int Id { get; set; }
        public string Version { get; set; }
        public string Phone { get; set; }
        public string Password { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public sealed class OrderLineDraft {
        public int ProductId { get; set; }
        public string Name { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Total { get { return Price * Quantity; } }
    }

    public sealed class OrderDraft {
        public int Id { get; set; }
        public string Version { get; set; }
        public int ClientId { get; set; }
        public DateTime Date { get; set; }
        public string Status { get; set; }
        public string Payment { get; set; }
        public string Talon { get; set; }
        public decimal Amount { get; set; }
        public List<OrderLineDraft> Lines { get; set; }
    }

    public static class ManagementService {
        public static readonly string[] OrderStatuses = { "Новый", "В обработке", "Готов к выдаче", "Выдан", "Отменён" };
        public static readonly string[] Payments = { "Наличные" };

        private static UserSession Session() { AuthService.RequireStaff(); return AuthService.Current; }
        private static void CheckSession(UserSession session) {
            AuthService.RequireStaff();
            if (!ReferenceEquals(session, AuthService.Current)) throw new UnauthorizedAccessException("Сеанс завершён. Войдите снова.");
        }
        private static BookBaseEntities Context() {
            BookBaseEntities context = new BookBaseEntities();
            context.Database.CommandTimeout = 15;
            return context;
        }
        private static string Required(string value, string label, int length) {
            value = (value ?? "").Trim();
            if (value.Length == 0 || value.Length > length) throw new ArgumentException(label + ": от 1 до " + length + " символов.");
            return value;
        }
        private static void Money(decimal value) {
            if (value < 0 || value > 922337203685477.5807m || decimal.Round(value, 4) != value)
                throw new ArgumentException("Укажите неотрицательную сумму с точностью не более 4 знаков после запятой.");
        }
        private static void Date(DateTime value) {
            if (value < new DateTime(1753, 1, 1)) throw new ArgumentException("Дата не может быть раньше 01.01.1753.");
        }
        private static string Fingerprint(params object[] fields) {
            StringBuilder text = new StringBuilder();
            foreach (object field in fields) {
                string value = field is byte[] ? Convert.ToBase64String((byte[])field) : Convert.ToString(field, CultureInfo.InvariantCulture) ?? "";
                text.Append(value.Length).Append(':').Append(value);
            }
            using (SHA256 hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));
        }
        private static string Version(Product product) {
            return Fingerprint(product.Id, product.Name, product.Author, product.Description, product.Category, product.Price,
                product.OldPrice, product.BookCount, product.MinBookCount, product.ImagePreview);
        }
        private static string Version(User client) { return Fingerprint(client.Id, client.NumberPhone, client.Password, client.Role, client.Created_at.Ticks); }
        private static string Version(Order order) {
            return Fingerprint(order.Id, order.UserId, order.Date.Ticks, order.Amount, order.Status, order.Payment, order.Talon,
                string.Join(",", order.OrderItems.OrderBy(item => item.Id).Select(item => item.Id + ":" + item.ProductId)));
        }
        private static void Unchanged(string expected, string actual) {
            if (string.IsNullOrEmpty(expected) || expected != actual) throw new ManagementException("Запись уже изменена. Закройте панель и откройте её снова.");
        }

        public static async Task<List<Category>> LoadCategoriesAsync() {
            UserSession session = Session();
            using (BookBaseEntities context = Context()) {
                var categories = await context.Categories.AsNoTracking().OrderBy(category => category.Name).ToListAsync();
                CheckSession(session); return categories;
            }
        }

        public static async Task<ProductDraft> LoadProductAsync(int id) {
            UserSession session = Session();
            using (BookBaseEntities context = Context()) {
                Product product = await context.Products.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id);
                if (product == null) throw new ManagementException("Товар уже удалён.");
                CheckSession(session);
                return new ProductDraft { Id = product.Id, Version = Version(product), Name = product.Name, Author = product.Author,
                    Description = product.Description, CategoryId = product.Category, Price = product.Price, OldPrice = product.OldPrice,
                    BookCount = product.BookCount, MinBookCount = product.MinBookCount, ImagePreview = product.ImagePreview };
            }
        }

        public static async Task<int> SaveProductAsync(ProductDraft draft) {
            UserSession session = Session();
            string name = Required(draft.Name, "Название", 128);
            string author = (draft.Author ?? "").Trim();
            if (author.Length > 128) throw new ArgumentException("Автор: максимум 128 символов.");
            Money(draft.Price); if (draft.OldPrice.HasValue) Money(draft.OldPrice.Value);
            if (draft.BookCount < 0 || draft.MinBookCount < 0) throw new ArgumentException("Остатки не могут быть отрицательными.");
            if (draft.ImagePreview != null && (draft.ImagePreview.Length > 5 * 1024 * 1024 || new ProductCard { ImagePreview = draft.ImagePreview }.Cover == null))
                throw new ArgumentException("Загрузите корректное изображение размером до 5 МБ.");
            using (BookBaseEntities context = Context())
            using (var transaction = context.Database.BeginTransaction(IsolationLevel.Serializable)) {
                Product product = draft.Id == 0 ? new Product() : await context.Products.SingleOrDefaultAsync(item => item.Id == draft.Id);
                if (product == null) throw new ManagementException("Товар уже удалён.");
                if (draft.Id != 0) Unchanged(draft.Version, Version(product));
                Category category;
                if (!string.IsNullOrWhiteSpace(draft.NewCategory)) {
                    string categoryName = Required(draft.NewCategory, "Категория", 128);
                    category = await context.Categories.FirstOrDefaultAsync(item => item.Name == categoryName);
                    if (category == null) { category = new Category { Name = categoryName }; context.Categories.Add(category); }
                } else {
                    category = await context.Categories.SingleOrDefaultAsync(item => item.Id == draft.CategoryId);
                    if (category == null) throw new ArgumentException("Выберите категорию или укажите новую.");
                }
                if (draft.Id == 0) context.Products.Add(product);
                product.Name = name; product.Author = author; product.Description = draft.Description;
                product.Category1 = category; product.Price = draft.Price; product.OldPrice = draft.OldPrice;
                product.BookCount = draft.BookCount; product.MinBookCount = draft.MinBookCount; product.ImagePreview = draft.ImagePreview;
                CheckSession(session); await context.SaveChangesAsync(); CheckSession(session); transaction.Commit(); DataChanges.Notify(); return product.Id;
            }
        }

        public static async Task DeleteProductAsync(int id, string version) {
            UserSession session = Session();
            using (BookBaseEntities context = Context())
            using (var transaction = context.Database.BeginTransaction(IsolationLevel.Serializable)) {
                Product product = await context.Products.SingleOrDefaultAsync(item => item.Id == id);
                if (product == null) throw new ManagementException("Товар уже удалён.");
                Unchanged(version, Version(product));
                if (await context.OrderItems.AnyAsync(item => item.ProductId == id)) throw new ManagementException("Товар используется в заказах. Его нельзя удалить; можно установить остаток 0.");
                context.Products.Remove(product);
                CheckSession(session); await context.SaveChangesAsync(); CheckSession(session); transaction.Commit(); DataChanges.Notify();
            }
        }

        public static string PhoneDigits(string phone) {
            string digits = new string((phone ?? "").Where(character => character >= '0' && character <= '9').ToArray());
            if (digits.Length == 11 && (digits[0] == '7' || digits[0] == '8')) digits = digits.Substring(1);
            return digits;
        }

        public static async Task<ClientDraft> LoadClientAsync(int id) {
            UserSession session = Session();
            using (BookBaseEntities context = Context()) {
                User client = await context.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id && item.Role == 1);
                if (client == null) throw new ManagementException("Клиент не найден.");
                CheckSession(session);
                return new ClientDraft { Id = id, Version = Version(client), Phone = PhoneDigits(client.NumberPhone), CreatedAt = client.Created_at };
            }
        }

        public static async Task<int> SaveClientAsync(ClientDraft draft) {
            UserSession session = Session();
            if (draft.Phone == null || draft.Phone.Length != 10 || !NumericInput.IsDigits(draft.Phone)) throw new ArgumentException("Введите 10 цифр номера после +7.");
            if (draft.Id == 0 && string.IsNullOrEmpty(draft.Password)) throw new ArgumentException("Укажите пароль нового клиента.");
            Date(draft.CreatedAt);
            string password = string.IsNullOrEmpty(draft.Password) ? null : await Task.Run(() => PasswordHasher.Hash(draft.Password));
            using (BookBaseEntities context = Context())
            using (var transaction = context.Database.BeginTransaction(IsolationLevel.Serializable)) {
                if (!await context.Roles.AnyAsync(role => role.Code == 1)) throw new ManagementException("В базе отсутствует роль «Клиент» с кодом 1. Добавьте её в таблицу Role.");
                User client = draft.Id == 0 ? new User { Role = 1 } : await context.Users.SingleOrDefaultAsync(item => item.Id == draft.Id && item.Role == 1);
                if (client == null) throw new ManagementException("Клиент не найден.");
                if (draft.Id != 0) Unchanged(draft.Version, Version(client));
                string digits = draft.Phone;
                bool duplicate = await context.Users.AnyAsync(user => user.Id != draft.Id && (
                    user.NumberPhone.Replace(" ", "").Replace("+", "").Replace("-", "").Replace("(", "").Replace(")", "") == "8" + digits ||
                    user.NumberPhone.Replace(" ", "").Replace("+", "").Replace("-", "").Replace("(", "").Replace(")", "") == "7" + digits ||
                    user.NumberPhone.Replace(" ", "").Replace("+", "").Replace("-", "").Replace("(", "").Replace(")", "") == digits));
                if (duplicate) throw new ArgumentException("Этот номер телефона уже используется.");
                if (draft.Id == 0) context.Users.Add(client);
                client.NumberPhone = "8" + digits; client.Created_at = draft.CreatedAt;
                if (password != null) client.Password = password;
                CheckSession(session); await context.SaveChangesAsync(); CheckSession(session); transaction.Commit(); DataChanges.Notify(); return client.Id;
            }
        }

        public static async Task DeleteClientAsync(int id, string version) {
            UserSession session = Session();
            using (BookBaseEntities context = Context())
            using (var transaction = context.Database.BeginTransaction(IsolationLevel.Serializable)) {
                User client = await context.Users.SingleOrDefaultAsync(item => item.Id == id && item.Role == 1);
                if (client == null) throw new ManagementException("Клиент не найден.");
                Unchanged(version, Version(client));
                if (await context.Orders.AnyAsync(order => order.UserId == id)) throw new ManagementException("У клиента есть заказы. Сначала удалите связанные заказы.");
                context.Users.Remove(client);
                CheckSession(session); await context.SaveChangesAsync(); CheckSession(session); transaction.Commit(); DataChanges.Notify();
            }
        }

        public static bool IsCancelled(string status) {
            return string.Equals((status ?? "").Trim().Replace('ё', 'е'), "Отменен", StringComparison.OrdinalIgnoreCase);
        }

        public static async Task<OrderDraft> LoadOrderAsync(int id) {
            UserSession session = Session();
            using (BookBaseEntities context = Context()) {
                Order order = await context.Orders.AsNoTracking().Include(item => item.OrderItems.Select(line => line.Product)).SingleOrDefaultAsync(item => item.Id == id);
                if (order == null) throw new ManagementException("Заказ уже удалён.");
                CheckSession(session);
                return new OrderDraft { Id = id, Version = Version(order), ClientId = order.UserId, Date = order.Date,
                    Status = order.Status, Payment = order.Payment, Talon = order.Talon, Amount = order.Amount,
                    Lines = order.OrderItems.GroupBy(item => item.ProductId).Select(group => new OrderLineDraft {
                        ProductId = group.Key, Name = group.First().Product.Name, Price = group.First().Product.Price, Quantity = group.Count() }).ToList() };
            }
        }

        public static void ValidateLines(List<OrderLineDraft> lines) {
            if (lines == null || lines.Count == 0) throw new ArgumentException("Добавьте хотя бы один товар в заказ.");
            if (lines.Any(line => line.Quantity < 1 || line.Quantity > 9999) || lines.Sum(line => (long)line.Quantity) > 10000)
                throw new ArgumentException("Количество: от 1 до 9999 на позицию, не более 10000 единиц в заказе.");
            if (lines.GroupBy(line => line.ProductId).Any(group => group.Count() != 1)) throw new ArgumentException("Товар повторяется. Увеличьте количество существующей позиции.");
        }

        public static async Task<int> SaveOrderAsync(OrderDraft draft) {
            UserSession session = Session();
            ValidateLines(draft.Lines); Date(draft.Date);
            string status = Required(draft.Status, "Статус", 32), payment = Required(draft.Payment, "Способ оплаты", 32);
            string talon = Required(draft.Talon, "Талон", 24);
            using (BookBaseEntities context = Context())
            using (var transaction = context.Database.BeginTransaction(IsolationLevel.Serializable)) {
                if (!await context.Users.AnyAsync(user => user.Id == draft.ClientId && user.Role == 1)) throw new ArgumentException("Выберите существующего клиента.");
                Order order = draft.Id == 0 ? new Order() : await context.Orders.Include(item => item.OrderItems).SingleOrDefaultAsync(item => item.Id == draft.Id);
                if (order == null) throw new ManagementException("Заказ уже удалён.");
                if (draft.Id != 0) Unchanged(draft.Version, Version(order));
                if (payment != "Наличные" && (draft.Id == 0 || payment != order.Payment))
                    throw new ArgumentException("Сейчас доступна только оплата наличными.");
                var oldCounts = order.OrderItems.GroupBy(line => line.ProductId).ToDictionary(group => group.Key, group => group.Count());
                var newCounts = draft.Lines.ToDictionary(line => line.ProductId, line => line.Quantity);
                var ids = oldCounts.Keys.Union(newCounts.Keys).ToList();
                var products = await context.Products.Where(product => ids.Contains(product.Id)).OrderBy(product => product.Id).ToListAsync();
                if (products.Count != ids.Count) throw new ManagementException("Один из товаров уже удалён. Обновите состав заказа.");
                bool oldCancelled = draft.Id == 0 || IsCancelled(order.Status), newCancelled = IsCancelled(status);
                foreach (Product product in products) {
                    int oldQuantity = oldCounts.ContainsKey(product.Id) ? oldCounts[product.Id] : 0;
                    int newQuantity = newCounts.ContainsKey(product.Id) ? newCounts[product.Id] : 0;
                    long stock = (long)product.BookCount + (oldCancelled ? 0 : oldQuantity) - (newCancelled ? 0 : newQuantity);
                    if (stock < 0) throw new ManagementException("Недостаточно товара «" + product.Name + "» на складе.");
                    if (stock > int.MaxValue) throw new ManagementException("Остаток товара превышает допустимое значение.");
                    product.BookCount = (int)stock;
                }
                bool changed = oldCounts.Count != newCounts.Count || oldCounts.Any(pair => !newCounts.ContainsKey(pair.Key) || newCounts[pair.Key] != pair.Value);
                if (draft.Id == 0 || changed) {
                    order.Amount = products.Where(product => newCounts.ContainsKey(product.Id)).Sum(product => product.Price * newCounts[product.Id]);
                    Money(order.Amount);
                }
                if (draft.Id == 0) context.Orders.Add(order);
                order.UserId = draft.ClientId; order.Date = draft.Date; order.Status = status; order.Payment = payment; order.Talon = talon;
                if (changed || draft.Id == 0) {
                    context.OrderItems.RemoveRange(order.OrderItems.ToList());
                    foreach (var line in draft.Lines) for (int i = 0; i < line.Quantity; i++) order.OrderItems.Add(new OrderItem { ProductId = line.ProductId });
                }
                CheckSession(session); await context.SaveChangesAsync(); CheckSession(session); transaction.Commit(); DataChanges.Notify(); return order.Id;
            }
        }

        public static async Task ChangeStatusAsync(int id, string status) {
            OrderDraft draft = await LoadOrderAsync(id);
            draft.Status = status;
            await SaveOrderAsync(draft);
        }

        public static async Task DeleteOrderAsync(int id, string version) {
            UserSession session = Session();
            using (BookBaseEntities context = Context())
            using (var transaction = context.Database.BeginTransaction(IsolationLevel.Serializable)) {
                Order order = await context.Orders.Include(item => item.OrderItems).SingleOrDefaultAsync(item => item.Id == id);
                if (order == null) throw new ManagementException("Заказ уже удалён.");
                Unchanged(version, Version(order));
                if (!IsCancelled(order.Status)) {
                    var counts = order.OrderItems.GroupBy(line => line.ProductId).ToDictionary(group => group.Key, group => group.Count());
                    var ids = counts.Keys.ToList();
                    var products = await context.Products.Where(product => ids.Contains(product.Id)).OrderBy(product => product.Id).ToListAsync();
                    foreach (Product product in products) product.BookCount = checked(product.BookCount + counts[product.Id]);
                }
                context.OrderItems.RemoveRange(order.OrderItems); context.Orders.Remove(order);
                CheckSession(session); await context.SaveChangesAsync(); CheckSession(session); transaction.Commit(); DataChanges.Notify();
            }
        }
    }
}

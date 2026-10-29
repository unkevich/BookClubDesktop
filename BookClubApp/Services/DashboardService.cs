using BookClubApp.Entity;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BookClubApp.Services {
    public sealed class OrderRow {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string Phone { get; set; }
        public int ItemCount { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; }
        public string Payment { get; set; }
        public string Talon { get; set; }
        public string Number { get { return "#" + Id; } }
        public Brush StatusBrush {
            get {
                string status = (Status ?? "").ToLowerInvariant();
                if (status.Contains("отмен")) return Brushes.Crimson;
                if (status.Contains("выдан")) return Brushes.ForestGreen;
                if (status.Contains("готов")) return Brushes.SeaGreen;
                if (status.Contains("обработ")) return Brushes.Goldenrod;
                if (status.Contains("нов")) return Brushes.DodgerBlue;
                return Brushes.SlateGray;
            }
        }
    }

    public sealed class ClientRow {
        public int Id { get; set; }
        public string NumberPhone { get; set; }
        public DateTime Created_at { get; set; }
        public int OrderCount { get; set; }
    }

    public sealed class ProductCard {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Author { get; set; }
        public string Category { get; set; }
        public decimal Price { get; set; }
        public int BookCount { get; set; }
        public int MinBookCount { get; set; }
        public byte[] ImagePreview { get; set; }
        public ImageSource Cover {
            get {
                if (ImagePreview == null || ImagePreview.Length == 0) return null;
                try {
                    using (MemoryStream stream = new MemoryStream(ImagePreview)) {
                        BitmapImage image = new BitmapImage();
                        image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
                        image.DecodePixelWidth = 180; image.StreamSource = stream; image.EndInit(); image.Freeze();
                        return image;
                    }
                } catch (Exception e) when (e is NotSupportedException || e is ArgumentException || e is IOException || e is FileFormatException) {
                    return null;
                }
            }
        }
        public string StockText { get { return BookCount <= 0 ? "Нет в наличии" : BookCount <= MinBookCount ? "Мало: " + BookCount : "В наличии: " + BookCount; } }
        public Brush StockBrush { get { return BookCount <= 0 ? Brushes.SlateGray : BookCount <= MinBookCount ? Brushes.DarkGoldenrod : Brushes.SeaGreen; } }
    }

    public sealed class DashboardData {
        public int OrderCount { get; set; }
        public int ProductCount { get; set; }
        public int ClientCount { get; set; }
        public int LowStockCount { get; set; }
        public List<OrderRow> Orders { get; set; }
        public List<ProductCard> Products { get; set; }
        public List<ClientRow> Clients { get; set; }
    }

    public static class DashboardService {
        public static async Task<DashboardData> LoadAsync() {
            AuthService.RequireStaff();
            UserSession session = AuthService.Current;
            using (BookBaseEntities context = new BookBaseEntities()) {
                context.Database.CommandTimeout = 10;
                DashboardData data = new DashboardData();
                data.OrderCount = await context.Orders.CountAsync();
                data.ProductCount = await context.Products.CountAsync();
                data.ClientCount = await context.Users.CountAsync(user => user.Role == 1);
                data.LowStockCount = await context.Products.CountAsync(product => product.BookCount <= product.MinBookCount);
                data.Orders = await context.Orders.AsNoTracking().OrderByDescending(order => order.Date).ThenByDescending(order => order.Id)
                    .Select(order => new OrderRow { Id = order.Id, Date = order.Date, Phone = order.User.NumberPhone,
                        ItemCount = order.OrderItems.Count(), Amount = order.Amount, Status = order.Status, Payment = order.Payment, Talon = order.Talon }).ToListAsync();
                data.Clients = await context.Users.AsNoTracking().Where(user => user.Role == 1).OrderByDescending(user => user.Created_at)
                    .Select(user => new ClientRow { Id = user.Id, NumberPhone = user.NumberPhone, Created_at = user.Created_at, OrderCount = user.Orders.Count() }).ToListAsync();
                data.Products = await ProductQuery(context).OrderByDescending(product => product.OrderItems.Count()).ThenBy(product => product.Id)
                    .Select(product => new ProductCard { Id = product.Id, Name = product.Name, Author = product.Author,
                        Category = product.Category1.Name, Price = product.Price, BookCount = product.BookCount,
                        MinBookCount = product.MinBookCount, ImagePreview = product.ImagePreview }).ToListAsync();
                AuthService.RequireStaff();
                if (!ReferenceEquals(session, AuthService.Current)) throw new UnauthorizedAccessException("Сеанс завершён.");
                return data;
            }
        }

        private static IQueryable<Product> ProductQuery(BookBaseEntities context) {
            return context.Products.AsNoTracking();
        }

        public static async Task<List<ProductCard>> LoadCatalogAsync() {
            using (BookBaseEntities context = new BookBaseEntities()) {
                context.Database.CommandTimeout = 10;
                return await ProductQuery(context).OrderBy(product => product.Name)
                    .Select(product => new ProductCard { Id = product.Id, Name = product.Name, Author = product.Author,
                        Category = product.Category1.Name, Price = product.Price, BookCount = product.BookCount,
                        MinBookCount = product.MinBookCount, ImagePreview = product.ImagePreview }).ToListAsync();
            }
        }

        public static async Task<List<string>> LoadOrderItemsAsync(int orderId) {
            AuthService.RequireStaff();
            using (BookBaseEntities context = new BookBaseEntities()) {
                context.Database.CommandTimeout = 10;
                return await context.OrderItems.AsNoTracking().Where(item => item.OrderId == orderId)
                    .Select(item => item.Product.Name).ToListAsync();
            }
        }
    }
}

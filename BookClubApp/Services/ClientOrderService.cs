using BookClubApp.Entity;
using System;

using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;

using System.Threading.Tasks;

namespace BookClubApp.Services {
    public sealed class CartRequestLine {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }
    public static class ClientOrderService {
        public const string PickupName = "Пункт выдачи №1";
        public const string PickupAddress = "г. Таганрог, ул. Чехова, 336";
        private static UserSession Client() {
            var session = AuthService.Current;
            if (session == null || session.Role != 1) throw new UnauthorizedAccessException("Для оформления войдите в аккаунт клиента.");
            return session;
        }
        private static void SameSession(UserSession session) {
            if (!ReferenceEquals(session, AuthService.Current)) throw new UnauthorizedAccessException("Сеанс завершён. Войдите снова.");
        }
        public static async Task<int> CreateCashOrderAsync(List<CartRequestLine> request, Guid requestId, decimal expectedAmount) {
            var session = Client();
            if (requestId == Guid.Empty || request == null || request.Count == 0 || request.Any(line => line.Quantity < 1 || line.Quantity > 9999)
                || request.Sum(line => (long)line.Quantity) > 10000 || request.GroupBy(line => line.ProductId).Any(group => group.Count() != 1))
                throw new ArgumentException("Проверьте состав корзины и количество товаров.");
            request = request.Select(line => new CartRequestLine { ProductId = line.ProductId, Quantity = line.Quantity }).ToList();
            using (var context = new BookBaseEntities())
            using (var transaction = context.Database.BeginTransaction(IsolationLevel.Serializable)) {
                context.Database.CommandTimeout = 15;
                if (!await context.Users.AnyAsync(user => user.Id == session.Id && user.Role == 1)) throw new UnauthorizedAccessException("Аккаунт клиента недоступен.");
                string talon = "Н-" + requestId.ToString("N").Substring(0, 22);
                // Reserve the idempotency key before stock updates, including concurrent requests.
                var existing = await context.Orders.SqlQuery("SELECT * FROM dbo.[Order] WITH (UPDLOCK,HOLDLOCK) WHERE Talon=@talon AND UserId=@user",
                    new SqlParameter("@talon", talon), new SqlParameter("@user", session.Id)).SingleOrDefaultAsync();
                if (existing != null) { SameSession(session); return existing.Id; }
                var ids = request.Select(line => line.ProductId).ToList();
                var products = await context.Products.Where(product => ids.Contains(product.Id)).OrderBy(product => product.Id).ToListAsync();
                if (products.Count != ids.Count) throw new ManagementException("Один из товаров удалён. Проверьте корзину.");
                decimal amount = 0;
                foreach (var line in request) {
                    var product = products.Single(item => item.Id == line.ProductId);
                    if (product.Price < 0 || product.BookCount < line.Quantity) throw new ManagementException("Изменилось наличие товара «" + product.Name + "».");
                    amount += product.Price * line.Quantity;
                    product.BookCount -= line.Quantity;
                }
                if (amount != expectedAmount) throw new ManagementException("Цена изменилась. Проверьте новую сумму перед оформлением.");
                if (amount > 922337203685477.5807m) throw new ArgumentException("Сумма заказа превышает допустимое значение.");
                var orderToSave = new Order { UserId = session.Id, Date = DateTime.Now, Amount = amount, Status = "Новый", Payment = "Наличные", Talon = talon };
                foreach (var line in request) for (int i = 0; i < line.Quantity; i++) orderToSave.OrderItems.Add(new OrderItem { ProductId = line.ProductId });
                context.Orders.Add(orderToSave); SameSession(session); await context.SaveChangesAsync(); SameSession(session);
                transaction.Commit(); DataChanges.Notify(); return orderToSave.Id;
            }
        }
        public static async Task<List<OrderRow>> LoadMyOrdersAsync() {
            UserSession session = Client();
            using (BookBaseEntities context = new BookBaseEntities()) {
                context.Database.CommandTimeout = 10;
                var orders = await context.Orders.AsNoTracking().Where(order => order.UserId == session.Id).OrderByDescending(order => order.Date)
                    .Select(order => new OrderRow { Id = order.Id, Date = order.Date, Amount = order.Amount, Status = order.Status,
                        Payment = order.Payment, Talon = order.Talon, ItemCount = order.OrderItems.Count() }).ToListAsync();
                SameSession(session); return orders;
            }
        }

        public static async Task<string> LoadPhoneAsync() {
            UserSession session = Client();
            using (BookBaseEntities context = new BookBaseEntities()) {
                context.Database.CommandTimeout = 10;
                string phone = await context.Users.Where(user => user.Id == session.Id && user.Role == 1).Select(user => user.NumberPhone).SingleOrDefaultAsync();
                SameSession(session);
                if (phone == null) throw new UnauthorizedAccessException("Аккаунт клиента недоступен.");
                return BookClubApp.Helpers.PhoneNumber.Format(phone);
            }
        }
    }
}

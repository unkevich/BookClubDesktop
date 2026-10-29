using BookClubApp.Entity;
using BookClubApp.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BookClubApp.Services {
    public sealed class ReceiptLine {
        public string Name { get; set; }
        public int Quantity { get; set; }
    }
    public sealed class OrderReceipt {
        public int OrderId { get; set; }
        public DateTime Date { get; set; }
        public DateTime GeneratedAt { get; set; }
        public decimal Amount { get; set; }
        public string Phone { get; set; }
        public string Payment { get; set; }
        public string Status { get; set; }
        public string Talon { get; set; }
        public bool DemoData { get; set; }
        public List<ReceiptLine> Lines { get; set; }
        public bool IsTestPayment { get { return (Payment ?? "").IndexOf("тест", StringComparison.OrdinalIgnoreCase) >= 0; } }
    }

    public static class ReceiptService {
        public static async Task<OrderReceipt> LoadAsync(int orderId) {
            UserSession session = AuthService.Current;
            if (session == null || (!session.IsStaff && session.Role != 1)) throw new UnauthorizedAccessException("Войдите в аккаунт для получения чека.");
            using (BookBaseEntities context = new BookBaseEntities())
            using (var transaction = context.Database.BeginTransaction(IsolationLevel.Serializable)) {
                context.Database.CommandTimeout = 10;
                var query = context.Orders.AsNoTracking().Where(order => order.Id == orderId);
                if (!session.IsStaff) query = query.Where(order => order.UserId == session.Id);
                OrderReceipt receipt = await query.Select(order => new OrderReceipt {
                    OrderId = order.Id, Date = order.Date, Amount = order.Amount, Phone = order.User.NumberPhone,
                    Payment = order.Payment, Status = order.Status, Talon = order.Talon
                }).SingleOrDefaultAsync();
                if (receipt == null) throw new ManagementException("Чек заказа недоступен.");
                receipt.Lines = await context.OrderItems.AsNoTracking().Where(item => item.OrderId == orderId)
                    .GroupBy(item => new { item.ProductId, item.Product.Name }).OrderBy(group => group.Key.Name)
                    .Select(group => new ReceiptLine { Name = group.Key.Name, Quantity = group.Count() }).ToListAsync();
                if (!ReferenceEquals(session, AuthService.Current)) throw new UnauthorizedAccessException("Сеанс завершён.");
                receipt.Phone = PhoneNumber.Format(receipt.Phone); receipt.GeneratedAt = DateTime.Now;
                transaction.Commit(); return receipt;
            }
        }

        public static async Task SaveAsync(int orderId, string path) {
            UserSession session = AuthService.Current;
            OrderReceipt receipt = await LoadAsync(orderId);
            byte[] bytes = await Task.Run(() => ReceiptPdfWriter.Create(receipt));
            if (!ReferenceEquals(session, AuthService.Current)) throw new UnauthorizedAccessException("Сеанс завершён.");
            string destination = Path.GetFullPath(path);
            string temporary = Path.Combine(Path.GetDirectoryName(destination), ".receipt-" + Guid.NewGuid().ToString("N") + ".tmp");
            try {
                using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write)) await stream.WriteAsync(bytes, 0, bytes.Length);
                if (File.Exists(destination)) File.Replace(temporary, destination, null);
                else File.Move(temporary, destination);
            } finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}

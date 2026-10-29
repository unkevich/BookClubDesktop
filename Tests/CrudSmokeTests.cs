using BookClubApp.Entity;
using BookClubApp.Helpers;
using BookClubApp.Services;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

// Опциональная проверка CRUD. Создаёт только помеченные временные записи
// и удаляет их в finally, не затрагивая существующие данные.
internal static class CrudSmokeTests {
    private static readonly string Prefix = "CRUD-CHECK-" + Guid.NewGuid().ToString("N");
    private static string phone;
    private static bool ownsPhone;
    private static int clientId, firstId, secondId, orderId;

    private static int Main() {
        int result = 0;
        try { RunAsync().GetAwaiter().GetResult(); Console.WriteLine("All management checks passed."); }
        catch (Exception error) { Console.Error.WriteLine(error); result = 1; }
        finally {
            try { CleanupAsync().GetAwaiter().GetResult(); Console.WriteLine("Temporary test records removed."); }
            catch (Exception error) { Console.Error.WriteLine("CLEANUP FAILED: " + error); result = 2; }
            AuthService.SignOut();
        }
        return result;
    }

    private static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    private static void SetRole(int role) {
        var session = Activator.CreateInstance(typeof(UserSession), BindingFlags.Instance | BindingFlags.NonPublic,
            null, new object[] { -1, "80000000000", role }, null);
        typeof(AuthService).GetField("<Current>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, session);
    }
    private static async Task Reject(Func<Task> action, string name) {
        bool denied = false;
        try { await action(); } catch (ArgumentException) { denied = true; } catch (InvalidOperationException) { denied = true; } catch (UnauthorizedAccessException) { denied = true; }
        Assert(denied, name + " was not rejected");
    }
    private static async Task<int> Stock(int id) { return (await ManagementService.LoadProductAsync(id)).BookCount; }

    private static async Task RunAsync() {
        SetRole(1);
        await Reject(async () => { await ManagementService.SaveProductAsync(new ProductDraft()); }, "Client write");
        SetRole(2);
        await Reject(async () => { await ManagementService.SaveProductAsync(new ProductDraft { Name = "", Price = -1 }); }, "Invalid product");
        phone = "000" + ((Guid.NewGuid().GetHashCode() & int.MaxValue) % 10000000).ToString("D7");
        using (var context = new BookBaseEntities()) Assert(!context.Users.Any(user => user.NumberPhone == "8" + phone || user.NumberPhone == phone), "Reserved test phone already exists");
        ownsPhone = true;
        clientId = await ManagementService.SaveClientAsync(new ClientDraft { Phone = phone, Password = "FixturePassword123!", CreatedAt = DateTime.Now });
        using (var context = new BookBaseEntities()) {
            User client = await context.Users.SingleAsync(user => user.Id == clientId);
            Assert(client.Role == 1 && PasswordHasher.Verify("FixturePassword123!", client.Password), "Client password/role not saved");
        }
        await Reject(async () => { await ManagementService.SaveClientAsync(new ClientDraft { Phone = phone, Password = "Another123!", CreatedAt = DateTime.Now }); }, "Duplicate phone");
        var clientDraft = await ManagementService.LoadClientAsync(clientId);
        string staleClient = clientDraft.Version;
        clientDraft.CreatedAt = clientDraft.CreatedAt.AddDays(-1);
        await ManagementService.SaveClientAsync(clientDraft);
        clientDraft.Version = staleClient;
        await Reject(async () => { await ManagementService.SaveClientAsync(clientDraft); }, "Stale client");
        Console.WriteLine("Client create/edit, hashed password, duplicate phone and stale edit: OK");

        firstId = await ManagementService.SaveProductAsync(new ProductDraft { Name = Prefix + "-first", Author = "Test", NewCategory = Prefix, Price = 100, BookCount = 10, MinBookCount = 2 });
        var first = await ManagementService.LoadProductAsync(firstId);
        secondId = await ManagementService.SaveProductAsync(new ProductDraft { Name = Prefix + "-second", CategoryId = first.CategoryId, Price = 200, BookCount = 5, MinBookCount = 1 });
        first.OldPrice = 120; first.Description = "Updated fixture";
        await ManagementService.SaveProductAsync(first);
        await Reject(async () => { await ManagementService.SaveProductAsync(first); }, "Stale product");
        Assert((await ManagementService.LoadProductAsync(firstId)).Description == "Updated fixture", "Product edit failed");
        Console.WriteLine("Product create/edit, category creation and stale edit: OK");

        orderId = await ManagementService.SaveOrderAsync(new OrderDraft { ClientId = clientId, Date = DateTime.Now, Status = "Новый", Payment = "Наличные", Talon = "TEST-" + Prefix.Substring(11, 12), Lines = new List<OrderLineDraft> {
            new OrderLineDraft { ProductId = firstId, Quantity = 2 }, new OrderLineDraft { ProductId = secondId, Quantity = 1 } } });
        var order = await ManagementService.LoadOrderAsync(orderId);
        Assert(order.Amount == 400 && order.Lines.Sum(line => line.Quantity) == 3, "Order amount/composition incorrect");
        Assert(await Stock(firstId) == 8 && await Stock(secondId) == 4, "Stock not reserved");
        var productForDelete = await ManagementService.LoadProductAsync(firstId);
        await Reject(() => ManagementService.DeleteProductAsync(firstId, productForDelete.Version), "Referenced product deletion");
        var clientForDelete = await ManagementService.LoadClientAsync(clientId);
        await Reject(() => ManagementService.DeleteClientAsync(clientId, clientForDelete.Version), "Referenced client deletion");
        string staleOrder = order.Version;
        order.Lines.Single(line => line.ProductId == firstId).Quantity = 3;
        await ManagementService.SaveOrderAsync(order);
        await Reject(async () => { order.Version = staleOrder; await ManagementService.SaveOrderAsync(order); }, "Stale order");
        order = await ManagementService.LoadOrderAsync(orderId);
        Assert(order.Amount == 500 && await Stock(firstId) == 7, "Edited order not repriced/reserved");
        first = await ManagementService.LoadProductAsync(firstId); first.Price = 150;
        await ManagementService.SaveProductAsync(first);
        await ManagementService.ChangeStatusAsync(orderId, "Готов к выдаче");
        Assert((await ManagementService.LoadOrderAsync(orderId)).Amount == 500, "Status update repriced existing order");
        await ManagementService.ChangeStatusAsync(orderId, "Отменён");
        Assert(await Stock(firstId) == 10 && await Stock(secondId) == 5, "Cancellation did not restore stock");
        first = await ManagementService.LoadProductAsync(firstId); first.BookCount = 0;
        await ManagementService.SaveProductAsync(first);
        await Reject(() => ManagementService.ChangeStatusAsync(orderId, "Новый"), "Overdrawn stock");
        Assert(await Stock(firstId) == 0 && await Stock(secondId) == 5 && (await ManagementService.LoadOrderAsync(orderId)).Status == "Отменён", "Failed edit partially saved");
        first = await ManagementService.LoadProductAsync(firstId); first.BookCount = 10;
        await ManagementService.SaveProductAsync(first);
        SetRole(3); await ManagementService.ChangeStatusAsync(orderId, "Новый");
        order = await ManagementService.LoadOrderAsync(orderId);
        await ManagementService.DeleteOrderAsync(orderId, order.Version);
        Assert(await Stock(firstId) == 10 && await Stock(secondId) == 5, "Order deletion did not restore stock");
        using (var context = new BookBaseEntities()) Assert(!context.OrderItems.Any(line => line.OrderId == orderId), "Order item orphan left behind");
        orderId = 0;
        first = await ManagementService.LoadProductAsync(firstId); await ManagementService.DeleteProductAsync(firstId, first.Version); firstId = 0;
        var second = await ManagementService.LoadProductAsync(secondId); await ManagementService.DeleteProductAsync(secondId, second.Version); secondId = 0;
        clientForDelete = await ManagementService.LoadClientAsync(clientId); await ManagementService.DeleteClientAsync(clientId, clientForDelete.Version); clientId = 0;
        Console.WriteLine("Order CRUD, quantity, stock reservation/cancel/delete, atomic failure, reference guards and admin writes: OK");
    }

    private static async Task CleanupAsync() {
        using (var context = new BookBaseEntities())
        using (var transaction = context.Database.BeginTransaction()) {
            var products = await context.Products.Where(product => product.Name.StartsWith(Prefix)).ToListAsync();
            var productIds = products.Select(product => product.Id).ToList();
            var clients = !ownsPhone ? new List<User>() : await context.Users.Where(user => user.NumberPhone == "8" + phone).ToListAsync();
            var clientIds = clients.Select(client => client.Id).ToList();
            var orders = await context.Orders.Where(order => clientIds.Contains(order.UserId)).ToListAsync();
            var orderIds = orders.Select(order => order.Id).ToList();
            var items = await context.OrderItems.Where(item => orderIds.Contains(item.OrderId)).ToListAsync();
            context.OrderItems.RemoveRange(items); context.Orders.RemoveRange(orders); context.Products.RemoveRange(products); context.Users.RemoveRange(clients);
            await context.SaveChangesAsync();
            var categories = await context.Categories.Where(category => category.Name == Prefix).ToListAsync();
            context.Categories.RemoveRange(categories); await context.SaveChangesAsync(); transaction.Commit();
        }
    }
}

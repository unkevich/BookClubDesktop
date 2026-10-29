using BookClubApp.Entity;
using BookClubApp.Helpers;
using BookClubApp.Services;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

internal static class CheckoutSmokeTests {
    private static readonly string Prefix = "CHECKOUT-CHECK-" + Guid.NewGuid().ToString("N");
    private static string phone;
    private static bool ownsPhone;
    private static int clientId, productId;
    private static int notifications;
    private static int Main() {
        int result = 0;
        try { RunAsync().GetAwaiter().GetResult(); Console.WriteLine("Cash checkout, ownership and live signature checks passed."); }
        catch (Exception error) { Console.Error.WriteLine(error); result = 1; }
        finally {
            try { CleanupAsync().GetAwaiter().GetResult(); Console.WriteLine("Checkout fixtures removed."); }
            catch (Exception error) { Console.Error.WriteLine("CLEANUP FAILED: " + error); result = 2; }
            AuthService.SignOut();
        }
        return result;
    }
    private static void SetRole(int role, int id = -1) {
        var session = Activator.CreateInstance(typeof(UserSession), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { id, "8" + phone, role }, null);
        typeof(AuthService).GetField("<Current>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, session);
    }
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static async Task Reject(Func<Task> action, string message) {
        bool rejected = false;
        try { await action(); } catch (ArgumentException) { rejected = true; } catch (ManagementException) { rejected = true; } catch (UnauthorizedAccessException) { rejected = true; }
        Assert(rejected, message);
    }
    private static List<CartRequestLine> Cart(int quantity = 2) { return new List<CartRequestLine> { new CartRequestLine { ProductId = productId, Quantity = quantity } }; }
    private static Task<string> Signature() { return (Task<string>)typeof(LiveDataWatcher).GetMethod("SignatureAsync", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { AuthService.Current }); }

    private static async Task RunAsync() {
        Assert(PhoneNumber.Format("89777080939") == "+7 (977) 708-09-39", "Phone display incorrect");
        AuthService.SignOut();
        await Reject(async () => { await ClientOrderService.CreateCashOrderAsync(Cart(), Guid.NewGuid(), 200); }, "Guest allowed to order");
        SetRole(2);
        await Reject(async () => { await ClientOrderService.CreateCashOrderAsync(Cart(), Guid.NewGuid(), 200); }, "Staff impersonated client");
        phone = "000" + ((Guid.NewGuid().GetHashCode() & int.MaxValue) % 10000000).ToString("D7");
        using (var context = new BookBaseEntities()) Assert(!context.Users.Any(user => user.NumberPhone == "8" + phone), "Reserved fixture phone exists");
        ownsPhone = true;
        DataChanges.Changed += (sender, e) => notifications++;
        clientId = await ManagementService.SaveClientAsync(new ClientDraft { Phone = phone, Password = "CheckoutFixture123!", CreatedAt = DateTime.Now });
        productId = await ManagementService.SaveProductAsync(new ProductDraft { Name = Prefix, NewCategory = Prefix, Price = 100, OldPrice = 150, BookCount = 10 });
        string oldSignature = await Signature();
        var product = await ManagementService.LoadProductAsync(productId); product.Description = "Changed field";
        await ManagementService.SaveProductAsync(product);
        Assert(oldSignature != await Signature(), "Live signature missed field-only change");
        Assert(notifications >= 3, "Local notifications missing");
        SetRole(1, clientId);
        Assert(await ClientOrderService.LoadPhoneAsync() == PhoneNumber.Format("8" + phone), "Current phone display incorrect");
        await Reject(async () => { await ClientOrderService.CreateCashOrderAsync(Cart(0), Guid.NewGuid(), 0); }, "Zero quantity accepted");
        await Reject(async () => { await ClientOrderService.CreateCashOrderAsync(Cart(11), Guid.NewGuid(), 1100); }, "Overstock basket accepted");
        await Reject(async () => { await ClientOrderService.CreateCashOrderAsync(Cart(), Guid.Empty, 200); }, "Empty key accepted");
        await Reject(async () => { await ClientOrderService.CreateCashOrderAsync(new List<CartRequestLine> { Cart()[0], Cart()[0] }, Guid.NewGuid(), 400); }, "Duplicate line accepted");
        await Reject(async () => { await ClientOrderService.CreateCashOrderAsync(Cart(), Guid.NewGuid(), 199); }, "Changed amount accepted");
        Assert((await ClientOrderService.LoadMyOrdersAsync()).Count == 0, "Failed checkout left order");
        using (var context = new BookBaseEntities()) Assert(context.Products.Single(x => x.Id == productId).BookCount == 10, "Failed checkout changed stock");
        SetRole(1, -999);
        await Reject(async () => { await ClientOrderService.CreateCashOrderAsync(Cart(), Guid.NewGuid(), 200); }, "Deleted client allowed to order");
        SetRole(1, clientId);
        Guid request = Guid.NewGuid();
        int[] orders = await Task.WhenAll(ClientOrderService.CreateCashOrderAsync(Cart(), request, 200), ClientOrderService.CreateCashOrderAsync(Cart(), request, 200));
        Assert(orders[0] == orders[1], "Duplicate order on repeated confirmation");
        Assert(await ClientOrderService.CreateCashOrderAsync(Cart(), request, 200) == orders[0], "Retry not idempotent");
        var mine = await ClientOrderService.LoadMyOrdersAsync();
        Assert(mine.Count == 1 && mine[0].Amount == 200 && mine[0].Payment == "Наличные" && mine[0].ItemCount == 2 && mine[0].Status == "Новый", "Cash order invalid");
        var receipt = await ReceiptService.LoadAsync(orders[0]);
        Assert(receipt.Amount == 200 && receipt.Payment == "Наличные" && !receipt.IsTestPayment && receipt.Talon == mine[0].Talon, "Cash receipt data inconsistent");
        using (var context = new BookBaseEntities()) {
            Assert(context.Products.Single(x => x.Id == productId).BookCount == 8, "Stock deducted twice");
            Assert(context.Users.Single(x => x.Id == clientId).NumberPhone.Trim() == "8" + phone, "Stored phone modified");
        }
        SetRole(1, -999);
        Assert((await ClientOrderService.LoadMyOrdersAsync()).Count == 0, "Other client's orders exposed");
        await Reject(async () => { await ReceiptService.LoadAsync(orders[0]); }, "Other client's receipt exposed");
        SetRole(2);
        Assert((await ReceiptService.LoadAsync(orders[0])).OrderId == orders[0], "Staff receipt access failed");
        var draft = await ManagementService.LoadOrderAsync(orders[0]); draft.Payment = "Карта";
        await Reject(async () => { await ManagementService.SaveOrderAsync(draft); }, "New non-cash payment accepted");
        AuthService.SignOut();
        await Reject(async () => { await ReceiptService.LoadAsync(orders[0]); }, "Guest receipt access allowed");
        Console.WriteLine("Cash only, authorization, validation, rollback, concurrent retries, stock, receipt data and order ownership: OK");
    }
    private static async Task CleanupAsync() {
        using (var context = new BookBaseEntities())
        using (var transaction = context.Database.BeginTransaction()) {
            var clients = !ownsPhone ? new List<User>() : await context.Users.Where(user => user.NumberPhone == "8" + phone).ToListAsync();
            var clientIds = clients.Select(user => user.Id).ToList();
            var orders = await context.Orders.Where(order => clientIds.Contains(order.UserId)).ToListAsync(); var ids = orders.Select(order => order.Id).ToList();
            context.OrderItems.RemoveRange(await context.OrderItems.Where(line => ids.Contains(line.OrderId)).ToListAsync()); context.Orders.RemoveRange(orders);
            context.Products.RemoveRange(await context.Products.Where(product => product.Name == Prefix).ToListAsync()); context.Users.RemoveRange(clients);
            await context.SaveChangesAsync();
            context.Categories.RemoveRange(await context.Categories.Where(category => category.Name == Prefix).ToListAsync()); await context.SaveChangesAsync(); transaction.Commit();
        }
    }
}

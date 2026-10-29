using BookClubApp.Helpers;
using BookClubApp.Services;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

// Проверка без изменения базы. Компилируется отдельно от WPF-приложения.
internal static class BackendSmokeTests {
    private static int Main(string[] args) {
        try {
            CheckPasswords();
            CheckRoles();
            if (args.Contains("--database")) CheckDatabaseAsync().GetAwaiter().GetResult();
            Console.WriteLine("All checks passed.");
            return 0;
        } catch (Exception error) {
            Console.Error.WriteLine(error);
            return 1;
        } finally {
            AuthService.SignOut();
        }
    }

    private static void Assert(bool condition, string message) {
        if (!condition) throw new Exception(message);
    }

    private static void CheckPasswords() {
        string first = PasswordHasher.Hash("Test password 123!");
        string second = PasswordHasher.Hash("Test password 123!");
        Assert(first.Length <= 64, "Hash does not fit Password column");
        Assert(first != second, "Salt is not random");
        Assert(PasswordHasher.Verify("Test password 123!", first), "Correct password rejected");
        Assert(!PasswordHasher.Verify("test password 123!", first), "Case mismatch accepted");
        Assert(!PasswordHasher.Verify("wrong", first), "Wrong password accepted");
        Assert(!PasswordHasher.Verify("", first), "Empty password accepted");
        Assert(!PasswordHasher.Verify("test", "p2$600000$invalid$invalid"), "Malformed hash accepted");
        Assert(!PasswordHasher.VerifyExisting("p2$broken", "p2$broken"), "Malformed versioned hash treated as plain text");
        Assert(PasswordHasher.VerifyExisting("Legacy123!", "Legacy123!"), "Legacy migration verification failed");
        Assert(!PasswordHasher.VerifyExisting("legacy123!", "Legacy123!"), "Legacy password is case-insensitive");
        Assert(!PasswordHasher.VerifyExisting("", ""), "Empty legacy password accepted");
        Console.WriteLine("PBKDF2, random salts, column length, invalid hashes, legacy verification: OK");
    }

    private static void SetSession(int role) {
        UserSession session = (UserSession)Activator.CreateInstance(typeof(UserSession), BindingFlags.Instance | BindingFlags.NonPublic,
            null, new object[] { -1, "89001234567", role }, null);
        typeof(AuthService).GetField("<Current>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, session);
    }

    private static void ExpectDenied() {
        bool denied = false;
        try { AuthService.RequireStaff(); } catch (UnauthorizedAccessException) { denied = true; }
        Assert(denied, "Staff access was granted incorrectly");
    }

    private static void CheckRoles() {
        AuthService.SignOut(); ExpectDenied();
        SetSession(1); ExpectDenied();
        SetSession(2); AuthService.RequireStaff();
        SetSession(3); AuthService.RequireStaff();
        SetSession(99); ExpectDenied();
        AuthService.SignOut(); ExpectDenied();
        Assert(AuthService.SignInAsync("abc", "wrong").GetAwaiter().GetResult() == null, "Invalid phone accepted");
        Assert(AuthService.SignInAsync("9001234567", "").GetAwaiter().GetResult() == null, "Empty password accepted");
        Console.WriteLine("Guest, client, manager, admin, invalid role and logout access: OK");
    }

    private static async Task CheckDatabaseAsync() {
        SetSession(2);
        DashboardData data = await DashboardService.LoadAsync();
        Assert(data.OrderCount == data.Orders.Count, "Order count mismatch");
        Assert(data.ProductCount == data.Products.Count, "Product count mismatch");
        Assert(data.ClientCount == data.Clients.Count, "Client count mismatch");
        var catalog = await DashboardService.LoadCatalogAsync();
        Assert(catalog.Count == data.ProductCount, "Catalog count mismatch");
        var items = await DashboardService.LoadOrderItemsAsync(int.MinValue);
        Assert(items.Count == 0, "Unknown order has items");
        SetSession(3);
        await DashboardService.LoadAsync();
        AuthService.SignOut();
        // Никаких записей или тестовых пользователей не создаётся.
        Assert(await AuthService.SignInAsync("0000000000", Guid.NewGuid().ToString()) == null, "Unknown account accepted");
        Assert(AuthService.Current == null, "Failed login left an active session");
        Console.WriteLine("EF model, dashboard/catalog/order queries and unknown-account sign-in (read only): OK");
    }
}

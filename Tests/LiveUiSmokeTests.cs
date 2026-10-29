using BookClubApp;
using BookClubApp.Controls;
using BookClubApp.Entity;
using BookClubApp.Pages;
using BookClubApp.Services;
using System;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

internal static class LiveUiSmokeTests {
    private static readonly string Prefix = "LIVE-CHECK-" + Guid.NewGuid().ToString("N");
    private static int productId;
    private static Window host;
    private static int result;
    [STAThread]
    private static int Main() {
        Application app = new Application(); app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        foreach (string dictionary in new[] { "ColorStyles", "AuthIllustrations", "AuthStyles", "DashboardStyles", "ManagementStyles" })
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/BookClubApp;component/Styles/" + dictionary + ".xaml", UriKind.Relative) });
        Dispatcher.CurrentDispatcher.BeginInvoke(new Action(async delegate {
            try { await RunAsync(); Console.WriteLine("Cross-instance polling, unsaved draft/filter preservation, cache refresh and toast checks passed."); }
            catch (Exception error) { Console.Error.WriteLine(error); result = 1; }
            if (host != null) host.Close();
            try { await CleanupAsync(); Console.WriteLine("Live UI fixture removed."); } catch (Exception error) { Console.Error.WriteLine(error); result = 2; }
            AuthService.SignOut(); app.Shutdown();
        }));
        app.Run(); return result;
    }
    private static object Field(object value, string name) { return value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value); }
    private static void SetField(object value, string name, object field) { value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(value, field); }
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static async Task Wait(Func<bool> condition, string message) {
        Stopwatch clock = Stopwatch.StartNew();
        while (!condition()) { if (clock.Elapsed.TotalSeconds > 12) throw new Exception(message); await Task.Delay(80); }
    }
    private static async Task ExternalPrice(decimal price) {
        // Прямой EF без DataChanges.Notify имитирует другое приложение/пользователя.
        using (var context = new BookBaseEntities()) { var product = await context.Products.SingleAsync(item => item.Id == productId); product.Price = price; await context.SaveChangesAsync(); }
    }
    private static async Task RunAsync() {
        var session = Activator.CreateInstance(typeof(UserSession), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { -1, "89001234567", 2 }, null);
        typeof(AuthService).GetField("<Current>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, session);
        productId = await ManagementService.SaveProductAsync(new ProductDraft { Name = Prefix, NewCategory = Prefix, Price = 100, BookCount = 10 });
        var page = new HomePage();
        host = new Window { Content = page, Width = 1400, Height = 900, Left = -10000, Top = -10000,
            WindowStartupLocation = WindowStartupLocation.Manual, ShowInTaskbar = false, ShowActivated = false };
        host.Show();
        await Wait(() => Field(page, "data") != null, "Initial home load failed");
        var menu = (ListBox)page.FindName("listMenu"); menu.SelectedIndex = 2;
        var view = (ManagementView)page.FindName("managementView");
        await Wait(() => Field(view, "data") != null && !(bool)Field(view, "busy"), "Initial management load failed");
        view.BeginEdit(productId);
        await Wait(() => Field(view, "productDraft") != null && !(bool)Field(view, "busy"), "Editor did not open");
        var draft = (ProductDraft)Field(view, "productDraft");
        var name = (TextBox)view.FindName("txtBoxProductName"); name.Text = Prefix + " unsaved";
        var sort = (ComboBox)view.FindName("comboSort"); sort.SelectedIndex = 2;
        var category = (ComboBox)view.FindName("comboCategoryFilter");
        category.SelectedItem = category.Items.Cast<Category>().Single(item => item.Id == draft.CategoryId);
        Assert((bool)Field(view, "dirty"), "Typing did not mark draft dirty");
        await ExternalPrice(120);
        await Wait(() => ((DashboardData)Field(view, "data")).Products.Single(item => item.Id == productId).Price == 120, "External write did not reach manager automatically");
        Assert(name.Text == Prefix + " unsaved", "Background refresh overwrote typed text");
        Assert(sort.SelectedIndex == 2 && ((Category)category.SelectedItem).Id == draft.CategoryId, "Filters lost on refresh");
        await Wait(() => ((TextBlock)view.FindName("txtBlockEditorError")).Visibility == Visibility.Visible, "Stale draft warning missing");
        bool rejected = false;
        try { draft.Name = Prefix + " unsaved"; await ManagementService.SaveProductAsync(draft); } catch (ManagementException) { rejected = true; }
        Assert(rejected, "Stale draft overwrote another user's edit");

        SetField(view, "dirty", false); view.TryLeave(); menu.SelectedIndex = 0;
        await ExternalPrice(130);
        await Wait(() => ((DashboardData)Field(page, "data")).Products.Single(item => item.Id == productId).Price == 130, "External write did not refresh dashboard");
        menu.SelectedIndex = 2;
        await Wait(() => Field(view, "data") != null && ((DashboardData)Field(view, "data")).Products.Single(item => item.Id == productId).Price == 130, "Returning to management showed stale cache");
        var panel = (Border)view.FindName("messagePanel"); panel.Visibility = Visibility.Visible;
        var toast = (DispatcherTimer)Field(view, "toastTimer"); toast.Interval = TimeSpan.FromMilliseconds(50); toast.Start();
        await Wait(() => panel.Visibility == Visibility.Collapsed, "Success toast did not hide");
        Assert(page.FindName("btnRefresh") == null, "Refresh button is still present");
    }
    private static async Task CleanupAsync() {
        using (var context = new BookBaseEntities())
        using (var transaction = context.Database.BeginTransaction()) {
            if (productId != 0) { var product = await context.Products.SingleOrDefaultAsync(item => item.Id == productId); if (product != null) context.Products.Remove(product); }
            await context.SaveChangesAsync();
            context.Categories.RemoveRange(await context.Categories.Where(item => item.Name == Prefix).ToListAsync()); await context.SaveChangesAsync(); transaction.Commit();
        }
    }
}

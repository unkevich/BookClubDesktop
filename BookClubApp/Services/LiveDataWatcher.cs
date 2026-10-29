using BookClubApp.Entity;
using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows.Controls.Primitives;

namespace BookClubApp.Services {
    public static class DataChanges {
        public static event EventHandler Changed;
        public static void Notify() { Changed?.Invoke(null, EventArgs.Empty); }
    }

    // Без изменения схемы SQL Server: сильная контрольная сумма, локальное событие
    // сразу после commit и проверка внешних изменений раз в секунду.
    public sealed class LiveDataWatcher {
        private readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private readonly Func<Task<bool>> refresh;
        private readonly Action error;
        private string version;
        private bool active, checking, force;

        public LiveDataWatcher(Func<Task<bool>> refresh, Action error) {
            this.refresh = refresh; this.error = error;
            timer.Tick += (sender, e) => Check();
        }
        public void Start() {
            if (active) return;
            active = true; version = null; DataChanges.Changed += LocalChange; timer.Start(); Check();
        }
        public void Stop() { active = false; timer.Stop(); DataChanges.Changed -= LocalChange; }
        private void LocalChange(object sender, EventArgs e) { force = true; Check(); }
        private async void Check() {
            if (!active || checking) return;
            checking = true;
            try {
                UserSession session = AuthService.Current;
                string next = await SignatureAsync(session);
                if (!active || !ReferenceEquals(session, AuthService.Current)) return;
                if (Mouse.Captured is MenuBase) return;
                bool requested = force; force = false;
                if (next != version || requested) {
                    if (await refresh()) version = next;
                    else force = true;
                }
            } catch (Exception) { if (active) error?.Invoke(); }
            finally { checking = false; }
        }

        private static async Task<string> SignatureAsync(UserSession session) {
            using (BookBaseEntities context = new BookBaseEntities()) {
                var connection = context.Database.Connection;
                await connection.OpenAsync();
                using (var command = connection.CreateCommand()) {
                    command.CommandTimeout = 5;
                    command.CommandText = "SELECT Id,Name,Author,Description,Category,Price,OldPrice,BookCount,MinBookCount,HASHBYTES('SHA2_256',ImagePreview) FROM dbo.Product ORDER BY Id; SELECT Id,Name FROM dbo.Category ORDER BY Id;";
                    if (session != null) {
                        command.CommandText += "SELECT Id,UserId,Date,Amount,Status,Payment,Talon FROM dbo.[Order]" + (session.IsStaff ? "" : " WHERE UserId=@userId") + " ORDER BY Id;";
                        command.CommandText += "SELECT i.Id,i.OrderId,i.ProductId FROM dbo.OrderItems i INNER JOIN dbo.[Order] o ON o.Id=i.OrderId" + (session.IsStaff ? "" : " WHERE o.UserId=@userId") + " ORDER BY i.Id;";
                        if (session.IsStaff) command.CommandText += "SELECT Id,NumberPhone,Role,Created_at,HASHBYTES('SHA2_256',CONVERT(varbinary(max),Password)) FROM dbo.[User] ORDER BY Id; SELECT Code,Name FROM dbo.Role ORDER BY Code;";
                        else command.CommandText += "SELECT Id,NumberPhone,Role,Created_at FROM dbo.[User] WHERE Id=@userId;";
                        var parameter = command.CreateParameter(); parameter.ParameterName = "@userId"; parameter.DbType = DbType.Int32; parameter.Value = session.Id; command.Parameters.Add(parameter);
                    }
                    using (var reader = await command.ExecuteReaderAsync())
                    using (MemoryStream buffer = new MemoryStream())
                    using (BinaryWriter writer = new BinaryWriter(buffer, Encoding.UTF8, true)) {
                        do {
                            writer.Write(reader.FieldCount);
                            while (await reader.ReadAsync()) {
                                writer.Write((byte)1);
                                for (int i = 0; i < reader.FieldCount; i++) {
                                    object value = reader.GetValue(i);
                                    writer.Write(value == DBNull.Value ? (byte)0 : (byte)1);
                                    if (value != DBNull.Value) writer.Write(value is byte[] ? Convert.ToBase64String((byte[])value)
                                        : value is DateTime ? ((DateTime)value).Ticks.ToString(CultureInfo.InvariantCulture) : Convert.ToString(value, CultureInfo.InvariantCulture));
                                }
                            }
                            writer.Write((byte)0);
                        } while (await reader.NextResultAsync());
                        writer.Flush(); buffer.Position = 0;
                        using (SHA256 hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(buffer));
                    }
                }
            }
        }
    }
}

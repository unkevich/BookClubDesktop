using BookClubApp.Services;
using Microsoft.Win32;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace BookClubApp.Helpers {
    public static class ReceiptExport {
        public static async Task<bool> SaveAsync(Window owner, int orderId) {
            SaveFileDialog dialog = new SaveFileDialog { Filter = "PDF-документ|*.pdf", DefaultExt = ".pdf", AddExtension = true,
                FileName = "Чек-заказа-" + orderId + ".pdf", Title = "Сохранить чек и талон", OverwritePrompt = true };
            if (dialog.ShowDialog(owner) != true) return false;
            try { await ReceiptService.SaveAsync(orderId, dialog.FileName); return true; }
            catch (Exception error) {
                string message = error is ManagementException || error is UnauthorizedAccessException ? error.Message
                    : error is IOException ? "Проверьте доступ к папке и закройте PDF, если он открыт в другой программе."
                    : "Не удалось создать PDF. Повторите сохранение из меню заказа.";
                MessageBox.Show(owner, message, "Сохранение чека", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }
    }
}

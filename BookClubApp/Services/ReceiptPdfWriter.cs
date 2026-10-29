using PdfSharp.Drawing;
using PdfSharp.Pdf;
using QRCoder;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace BookClubApp.Services {
    public static class ReceiptPdfWriter {
        public static byte[] Create(OrderReceipt receipt) {
            if (receipt == null) throw new ArgumentNullException("receipt");
            if (string.IsNullOrWhiteSpace(receipt.Talon)) throw new ManagementException("Для заказа не указан талон. Укажите его в редакторе заказа.");
            if (receipt.Lines == null || receipt.Lines.Any(line => line.Quantity < 1)) throw new ArgumentException("Некорректный состав заказа.");
            using (Canvas canvas = new Canvas(receipt)) return canvas.Build();
        }

        private sealed class Canvas : IDisposable {
            private readonly OrderReceipt receipt;
            private readonly PdfDocument document = new PdfDocument();
            private readonly XBrush ink = new XSolidBrush(XColor.FromArgb(15, 23, 42));
            private readonly XBrush muted = new XSolidBrush(XColor.FromArgb(100, 116, 139));
            private readonly XBrush red = new XSolidBrush(XColor.FromArgb(255, 31, 61));
            private readonly XBrush pale = new XSolidBrush(XColor.FromArgb(248, 249, 251));
            private readonly XPen line = new XPen(XColor.FromArgb(226, 232, 240), 0.7);
            private readonly XFont regular, small, bold, heading, brand, code;
            private XGraphics graphics;
            private PdfPage page;
            private double y;
            private const double Left = 42, Width = 511, Bottom = 784;

            public Canvas(OrderReceipt receipt) {
                this.receipt = receipt;
                XPdfFontOptions options = new XPdfFontOptions(PdfFontEncoding.Unicode);
                regular = new XFont("Segoe UI", 11, XFontStyle.Regular, options);
                small = new XFont("Segoe UI", 9, XFontStyle.Regular, options);
                bold = new XFont("Segoe UI", 11, XFontStyle.Bold, options);
                heading = new XFont("Segoe UI", 23, XFontStyle.Bold, options);
                brand = new XFont("Segoe UI", 16, XFontStyle.Bold, options);
                code = new XFont("Consolas", 11, XFontStyle.Bold, options);
                document.Info.Title = "Чек и талон заказа #" + receipt.OrderId;
                document.Info.Author = "Книжный клуб";
                document.Info.Subject = receipt.DemoData ? "Демонстрационный чек на тестовых данных" : "Информационный чек заказа и талон получения";
            }
            private static string Clean(string text) {
                return (text ?? "").Replace('\u2011', '-').Replace('\u2013', '-').Replace('\u2014', '-').Replace('\r', ' ');
            }
            private void Text(string text, XFont font, XBrush brush, double x, double top, double width, XStringAlignment alignment = XStringAlignment.Near) {
                graphics.DrawString(Clean(text), font, brush, new XRect(x, top, width, font.Height + 4), new XStringFormat { Alignment = alignment, LineAlignment = XLineAlignment.Near });
            }
            private List<string> Wrap(string text, XFont font, double width) {
                List<string> result = new List<string>();
                foreach (string paragraph in Clean(text).Split('\n')) {
                    string current = "";
                    foreach (string word in paragraph.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) {
                        string candidate = current.Length == 0 ? word : current + " " + word;
                        if (graphics.MeasureString(candidate, font).Width <= width) { current = candidate; continue; }
                        if (current.Length > 0) { result.Add(current); current = ""; }
                        var elements = StringInfo.GetTextElementEnumerator(word);
                        while (elements.MoveNext()) {
                            string element = elements.GetTextElement();
                            if (current.Length > 0 && graphics.MeasureString(current + element, font).Width > width) { result.Add(current); current = ""; }
                            current += element;
                        }
                    }
                    result.Add(current);
                }
                return result;
            }
            private void Paragraph(string text, XFont font, XBrush brush, double x, double width, double step = 16) {
                foreach (string row in Wrap(text, font, width)) { Text(row, font, brush, x, y, width); y += step; }
            }
            private void NewPage(bool continuation) {
                if (graphics != null) graphics.Dispose();
                page = document.AddPage(); page.Size = PdfSharp.PageSize.A4;
                graphics = XGraphics.FromPdfPage(page);
                graphics.DrawRectangle(red, Left, 38, 4, 33);
                Text("Книжный клуб", brand, ink, Left + 16, 36, 330);
                Text("Книги и канцелярские товары", small, muted, Left + 16, 61, 330);
                Text("ЗАКАЗ #" + receipt.OrderId, bold, muted, 395, 43, 158, XStringAlignment.Far);
                graphics.DrawLine(line, Left, 89, Left + Width, 89);
                y = 110;
                if (continuation) { Text("Состав заказа - продолжение", bold, ink, Left, y, Width); y += 28; TableHeader(); }
            }
            private void TableHeader() {
                graphics.DrawRectangle(pale, Left, y, Width, 29);
                Text("Наименование товара", small, muted, Left + 12, y + 8, 385);
                Text("Количество", small, muted, Left + 415, y + 8, 84, XStringAlignment.Far);
                y += 35;
            }
            public byte[] Build() {
                NewPage(false);
                Text("Чек заказа #" + receipt.OrderId, heading, ink, Left, y, Width); y += 38;
                Paragraph("Информационный документ. Не является фискальным чеком.", small, muted, Left, Width, 14); y += 10;
                if (receipt.IsTestPayment || receipt.DemoData) {
                    graphics.DrawRectangle(new XSolidBrush(XColor.FromArgb(255, 232, 235)), Left, y, Width, 34);
                    Text(receipt.DemoData ? "ДЕМОНСТРАЦИОННЫЕ ДАННЫЕ" : "ТЕСТОВАЯ ОПЛАТА - БЕЗ СПИСАНИЯ ДЕНЕГ", bold, red, Left + 12, y + 10, Width - 24); y += 48;
                }
                Paragraph("Дата заказа: " + receipt.Date.ToString("dd.MM.yyyy HH:mm"), regular, ink, Left, Width);
                Paragraph("Покупатель: " + receipt.Phone, regular, ink, Left, Width);
                Paragraph("Оплата: " + receipt.Payment, regular, ink, Left, Width);
                Paragraph("Статус заказа: " + receipt.Status, regular, ink, Left, Width); y += 18;
                TableHeader();
                if (receipt.Lines.Count == 0) { Paragraph("Состав заказа не указан.", regular, muted, Left + 12, Width - 24); y += 10; }
                foreach (ReceiptLine item in receipt.Lines) {
                    var rows = Wrap(item.Name, regular, 391);
                    double height = Math.Max(34, rows.Count * 16 + 16);
                    if (y + height > Bottom) NewPage(true);
                    double top = y + 7;
                    foreach (string row in rows) { Text(row, regular, ink, Left + 12, top, 391); top += 16; }
                    Text(item.Quantity.ToString(CultureInfo.InvariantCulture) + " шт.", bold, ink, Left + 415, y + 7, 84, XStringAlignment.Far);
                    y += height; graphics.DrawLine(line, Left, y, Left + Width, y); y += 2;
                }
                if (y + 320 > Bottom) NewPage(false);
                y += 18;
                Text("ИТОГО", brand, ink, Left + 12, y, 180);
                Text(receipt.Amount.ToString("N2", CultureInfo.GetCultureInfo("ru-RU")) + " ₽", heading, ink, Left + 190, y - 4, Width - 202, XStringAlignment.Far); y += 35;
                Paragraph("Стоимость приведена общей суммой заказа.", small, muted, Left + 12, Width - 24, 14); y += 20;
                double ticketTop = y;
                graphics.DrawRoundedRectangle(line, pale, new XRect(Left, ticketTop, Width, 204), new XSize(8, 8));
                Text("ТАЛОН НА ПОЛУЧЕНИЕ", bold, red, Left + 18, ticketTop + 18, 310);
                Text(receipt.Talon, code, ink, Left + 18, ticketTop + 49, 305);
                y = ticketTop + 82;
                Paragraph(ClientOrderService.PickupName, bold, ink, Left + 18, 300);
                Paragraph(ClientOrderService.PickupAddress, regular, ink, Left + 18, 300); y += 10;
                Paragraph("Покажите талон или QR-код при получении заказа.", small, muted, Left + 18, 300, 14);
                using (QRCodeGenerator generator = new QRCodeGenerator())
                using (QRCodeData data = generator.CreateQrCode(receipt.Talon, QRCodeGenerator.ECCLevel.Q, true, false, QRCodeGenerator.EciMode.Utf8))
                using (PngByteQRCode qr = new PngByteQRCode(data))
                using (MemoryStream stream = new MemoryStream(qr.GetGraphic(12)))
                using (XImage image = XImage.FromStream(stream)) {
                    graphics.DrawImage(image, Left + Width - 166, ticketTop + 22, 146, 146);
                }
                Text("QR содержит код талона", small, muted, Left + Width - 173, ticketTop + 175, 160, XStringAlignment.Center);
                y = ticketTop + 219;
                Paragraph("Состав и статус приведены на момент формирования документа.", small, muted, Left, Width, 14);
                graphics.Dispose(); graphics = null;
                for (int i = 0; i < document.PageCount; i++) {
                    using (XGraphics footer = XGraphics.FromPdfPage(document.Pages[i])) {
                        footer.DrawLine(line, Left, 797, Left + Width, 797);
                        footer.DrawString("Сформирован " + receipt.GeneratedAt.ToString("dd.MM.yyyy HH:mm"), small, muted, new XRect(Left, 808, 350, 15), XStringFormats.TopLeft);
                        footer.DrawString((i + 1) + " / " + document.PageCount, small, muted, new XRect(Left + 380, 808, 131, 15), XStringFormats.TopRight);
                    }
                }
                using (MemoryStream output = new MemoryStream()) { document.Save(output, false); return output.ToArray(); }
            }
            public void Dispose() { if (graphics != null) graphics.Dispose(); document.Dispose(); }
        }
    }
}

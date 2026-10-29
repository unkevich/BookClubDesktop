using BookClubApp.Services;
using PdfSharp.Pdf.IO;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using ZXing;

internal static class ReceiptPdfChecks {
    private static int Main(string[] args) {
        try {
            if (args.Length == 3 && args[0] == "--decode") {
                var reader = new BarcodeReader();
                reader.Options.PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE }; reader.Options.TryHarder = true;
                using (Bitmap bitmap = new Bitmap(args[1])) {
                    var result = reader.Decode(bitmap);
                    if (result == null || result.Text != File.ReadAllText(args[2], Encoding.UTF8)) throw new Exception("QR from rendered PDF does not match the talon");
                }
                Console.WriteLine("QR decoded from rendered PDF: exact talon match"); return 0;
            }
            Directory.CreateDirectory(args[0]); Directory.CreateDirectory(args[1]);
            OrderReceipt data = new OrderReceipt { OrderId = 1254, Date = new DateTime(2026, 10, 29, 12, 30, 0), GeneratedAt = new DateTime(2026, 10, 29, 12, 32, 0),
                Amount = 1450, Phone = "+7 (900) 123-45-67", Payment = "Оплачено (тест)", Status = "Новый", Talon = "Т-1234567890abcdef123456", DemoData = true,
                Lines = new List<ReceiptLine> { new ReceiptLine { Name = "1984 - Джордж Оруэлл", Quantity = 1 },
                    new ReceiptLine { Name = "Гарри Поттер и философский камень", Quantity = 1 }, new ReceiptLine { Name = "Тетрадь в клетку", Quantity = 2 } } };
            byte[] bytes = ReceiptPdfWriter.Create(data);
            using (var pdf = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import)) {
                if (pdf.PageCount != 1) throw new Exception("Normal receipt should fit one page");
            }
            File.WriteAllBytes(Path.Combine(args[0], "receipt-example.pdf"), bytes);
            File.WriteAllText(Path.Combine(args[1], "expected-talon.txt"), data.Talon, new UTF8Encoding(false));
            data.Lines = Enumerable.Range(1, 32).Select(index => new ReceiptLine {
                Name = "Очень длинное наименование товара №" + index + ": собрание произведений русской и зарубежной литературы для проверки переноса строк",
                Quantity = index % 3 + 1 }).ToList();
            data.Amount = 29990;
            byte[] longBytes = ReceiptPdfWriter.Create(data);
            using (var pdf = PdfReader.Open(new MemoryStream(longBytes), PdfDocumentOpenMode.Import)) {
                if (pdf.PageCount < 2) throw new Exception("Long order should use multiple pages");
                Console.WriteLine("Long receipt pages: " + pdf.PageCount);
            }
            File.WriteAllBytes(Path.Combine(args[1], "receipt-long.pdf"), longBytes);
            data.Talon = "";
            bool rejected = false;
            try { ReceiptPdfWriter.Create(data); } catch (ManagementException) { rejected = true; }
            if (!rejected) throw new Exception("Empty talon was accepted");
            Console.WriteLine("Single/multipage PDF, Cyrillic text and missing-talon guard: OK"); return 0;
        } catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}

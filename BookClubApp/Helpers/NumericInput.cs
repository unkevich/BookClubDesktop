using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BookClubApp.Helpers {
    public static class NumericInput {
        public static bool IsDigits(string text) {
            return !string.IsNullOrEmpty(text) && text.All(character => character >= '0' && character <= '9');
        }

        // функция оставляет в тексте только цифры, удаляя все остальные символы.
        public static void KeepDigits(TextBox box) {
            string text = new string(box.Text.Where(character => character >= '0' && character <= '9')
                .Take(box.MaxLength > 0 ? box.MaxLength : int.MaxValue).ToArray());
            if (text == box.Text) return;

            int caret = box.Text.Take(box.CaretIndex).Count(character => character >= '0' && character <= '9');
            box.Text = text;
            box.CaretIndex = System.Math.Min(caret, text.Length);
        }

        public static void Attach(TextBox box) {
            box.PreviewTextInput += txtBox_PreviewTextInput;
            box.PreviewKeyDown += txtBox_PreviewKeyDown;
            box.TextChanged += txtBox_TextChanged;
            DataObject.AddPastingHandler(box, txtBox_Pasting);
        }

        private static bool CanInsert(TextBox box, string text) {
            return IsDigits(text) && (box.MaxLength == 0 || box.Text.Length - box.SelectionLength + text.Length <= box.MaxLength);
        }

        private static void txtBox_PreviewTextInput(object sender, TextCompositionEventArgs e) {
            e.Handled = !CanInsert((TextBox)sender, e.Text);
        }

        private static void txtBox_PreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Space) e.Handled = true;
        }

        private static void txtBox_TextChanged(object sender, TextChangedEventArgs e) {
            KeepDigits((TextBox)sender);
        }

        private static void txtBox_Pasting(object sender, DataObjectPastingEventArgs e) {
            string text = e.DataObject.GetData(typeof(string)) as string;
            if (!CanInsert((TextBox)sender, text)) e.CancelCommand();
        }
    }
}

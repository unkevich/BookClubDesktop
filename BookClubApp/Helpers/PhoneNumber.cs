using System.Linq;

namespace BookClubApp.Helpers {
    public static class PhoneNumber {
        public static string Format(string value) {
            string digits = new string((value ?? "").Where(character => character >= '0' && character <= '9').ToArray());
            if (digits.Length == 11 && (digits[0] == '8' || digits[0] == '7')) digits = digits.Substring(1);
            return digits.Length == 10 ? "+7 (" + digits.Substring(0, 3) + ") " + digits.Substring(3, 3) + "-" + digits.Substring(6, 2) + "-" + digits.Substring(8, 2) : (value ?? "").Trim();
        }
    }
}

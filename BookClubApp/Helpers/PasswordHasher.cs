using System;
using System.Globalization;
using System.Security.Cryptography;

namespace BookClubApp.Helpers {
    public static class PasswordHasher {
        // 63 символа: версия, число итераций, соль (16 байт), ключ (20 байт).
        // формат помещается в существующий столбец Password nvarchar(64).
        private const int Iterations = 600000;

        public static string Hash(string password) {
            if (string.IsNullOrEmpty(password)) throw new ArgumentException("Пароль не должен быть пустым.");
            byte[] salt = new byte[16];
            using (RandomNumberGenerator random = RandomNumberGenerator.Create()) random.GetBytes(salt);
            using (Rfc2898DeriveBytes derive = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256)) {
                return "p2$" + Iterations.ToString(CultureInfo.InvariantCulture) + "$" + Convert.ToBase64String(salt)
                    + "$" + Convert.ToBase64String(derive.GetBytes(20));
            }
        }

        public static bool IsHash(string value) {
            return value != null && value.StartsWith("p2$", StringComparison.Ordinal);
        }

        public static bool VerifyExisting(string password, string stored) {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(stored)) return false;
            return IsHash(stored) ? Verify(password, stored) : string.Equals(password, stored, StringComparison.Ordinal);
        }

        public static bool Verify(string password, string stored) {
            if (string.IsNullOrEmpty(password) || !IsHash(stored)) return false;
            string[] parts = stored.Split('$');
            if (parts.Length != 4 || parts[1] != Iterations.ToString(CultureInfo.InvariantCulture)) return false;
            try {
                byte[] salt = Convert.FromBase64String(parts[2]);
                byte[] expected = Convert.FromBase64String(parts[3]);
                if (salt.Length != 16 || expected.Length != 20) return false;
                using (Rfc2898DeriveBytes derive = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256)) {
                    byte[] actual = derive.GetBytes(expected.Length);
                    int difference = 0;
                    for (int i = 0; i < actual.Length; i++) difference |= actual[i] ^ expected[i];
                    return difference == 0;
                }
            } catch (FormatException) {
                return false;
            }
        }
    }
}

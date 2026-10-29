using BookClubApp.Entity;
using BookClubApp.Helpers;
using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace BookClubApp.Services {
    public sealed class UserSession {
        public int Id { get; private set; }
        public string NumberPhone { get; private set; }
        public string DisplayPhone { get { return PhoneNumber.Format(NumberPhone); } }
        public int Role { get; private set; }
        public string RoleName { get { return Role == 3 ? "Администратор" : Role == 2 ? "Менеджер" : "Клиент"; } }
        public bool IsStaff { get { return Role == 2 || Role == 3; } }

        internal UserSession(int id, string phone, int role) {
            Id = id; NumberPhone = phone; Role = role;
        }
    }

    public static class AuthService {
        public static UserSession Current { get; private set; }

        public static async Task<UserSession> SignInAsync(string digits, string password) {
            Current = null;
            if (digits == null || digits.Length != 10 || !NumericInput.IsDigits(digits) || string.IsNullOrEmpty(password)) return null;
            using (BookBaseEntities context = new BookBaseEntities()) {
                context.Database.CommandTimeout = 10;
                // Поддерживаем существующие записи с 8, +7, 7, пробелами и скобками.
                var candidates = await context.Users.Where(user =>
                    user.NumberPhone.Replace(" ", "").Replace("+", "").Replace("-", "").Replace("(", "").Replace(")", "") == "8" + digits ||
                    user.NumberPhone.Replace(" ", "").Replace("+", "").Replace("-", "").Replace("(", "").Replace(")", "") == "7" + digits ||
                    user.NumberPhone.Replace(" ", "").Replace("+", "").Replace("-", "").Replace("(", "").Replace(")", "") == digits).Take(2).ToListAsync();
                // Не выбираем случайную учётную запись при дублирующемся номере.
                if (candidates.Count != 1) return null;
                User account = candidates[0];
                string stored = account.Password;
                bool hashed = PasswordHasher.IsHash(stored);
                bool valid = await Task.Run(() => PasswordHasher.VerifyExisting(password, stored));
                if (!valid || account.Role < 1 || account.Role > 3) return null;
                if (!hashed) {
                    account.Password = await Task.Run(() => PasswordHasher.Hash(password));
                    await context.SaveChangesAsync();
                }
                Current = new UserSession(account.Id, "8" + digits, account.Role);
                return Current;
            }
        }

        public static void SignOut() {
            Current = null;
        }

        public static void RequireStaff() {
            if (Current == null || !Current.IsStaff) throw new UnauthorizedAccessException("Раздел доступен менеджеру и администратору.");
        }
    }
}

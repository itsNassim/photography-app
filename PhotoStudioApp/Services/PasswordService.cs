using System;
using System.Security.Cryptography;

namespace PhotoStudioApp.Services
{
    /// <summary>Salted PBKDF2 password hashing - never store or compare plaintext passwords.</summary>
    public static class PasswordService
    {
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int Iterations = 100_000;

        public static void Hash(string password, out string hashBase64, out string saltBase64)
        {
            byte[] salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(salt);

            byte[] hash = DeriveBytes(password, salt);
            hashBase64 = Convert.ToBase64String(hash);
            saltBase64 = Convert.ToBase64String(salt);
        }

        public static bool Verify(string password, string hashBase64, string saltBase64)
        {
            if (string.IsNullOrEmpty(hashBase64) || string.IsNullOrEmpty(saltBase64))
                return false;

            byte[] salt = Convert.FromBase64String(saltBase64);
            byte[] expected = Convert.FromBase64String(hashBase64);
            byte[] actual = DeriveBytes(password, salt);

            // Constant-time compare.
            if (actual.Length != expected.Length) return false;
            int diff = 0;
            for (int i = 0; i < actual.Length; i++)
                diff |= actual[i] ^ expected[i];
            return diff == 0;
        }

        private static byte[] DeriveBytes(string password, byte[] salt)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(password ?? "", salt, Iterations, HashAlgorithmName.SHA256))
                return pbkdf2.GetBytes(HashSize);
        }
    }
}

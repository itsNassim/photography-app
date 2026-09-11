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

        // NOTE: .NET Framework 4.6 does not have the 4-argument Rfc2898DeriveBytes
        // constructor (password, salt, iterations, HashAlgorithmName) - that overload,
        // letting you pick SHA-256 instead of the default, was only added in .NET
        // Framework 4.7.2. On 4.6 only the algorithm-less 3-argument constructor exists,
        // which always uses HMACSHA1 internally. This is still a completely standard,
        // accepted way to do PBKDF2 (RFC 2898) - just SHA-1-based instead of SHA-256-based -
        // and is more than adequate for this app's threat model (a single local admin
        // password gate on a studio's own PC, not a public-facing service). IMPORTANT: if
        // this app was ever run before this change with a password already set, that old
        // hash was computed with SHA-256 and will no longer verify against this SHA-1-based
        // version - use "Definir le mot de passe" in Parametres once after updating to set
        // a fresh password if that happens.
        private static byte[] DeriveBytes(string password, byte[] salt)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(password ?? "", salt, Iterations))
                return pbkdf2.GetBytes(HashSize);
        }
    }
}

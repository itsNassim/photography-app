using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using PhotoStudioApp.Data;

namespace PhotoStudioApp.Services
{
    /// <summary>Encrypts the content of the login/studio-action log tables so a curious person
    /// opening the .db file in a generic SQLite browser or text editor sees unreadable base64
    /// instead of a plain audit trail of every password attempt and every file touched in the
    /// studio folders. Threat model matches the rest of the app (Windows 7 desktop, single
    /// shared admin password, see PasswordService's own note on this) - this exists to keep the
    /// logs unreadable OUTSIDE the app itself, as asked, not to resist a determined attacker
    /// with access to the compiled exe.
    ///
    /// AES-256-CBC, one random IV per encrypted value (prefixed to the ciphertext). The key is
    /// derived via PBKDF2 from a compiled-in application secret plus a random salt generated
    /// once per installation and stored in Settings (self-healing - see GetKey - exactly the
    /// same "runs every startup, safe/idempotent" pattern SchemaInitializer uses elsewhere).</summary>
    public static class LogCryptoService
    {
        // Not a user-facing secret - just keeps the derived key from being the salt alone,
        // same spirit as every other password-adjacent value in this app.
        private const string AppPepper = "PhotoStudioApp.LogGuard.v1.9f3Qz7mE2rL8pT";
        private const int Iterations = 50_000;
        private const int KeySizeBytes = 32; // AES-256

        private static readonly SettingsRepository SettingsRepo = new SettingsRepository();
        private static byte[] _cachedKey;
        private static readonly object Lock = new object();

        public static string Encrypt(string plaintext)
        {
            if (plaintext == null) plaintext = "";
            byte[] key = GetKey();

            using (var aes = new AesManaged { KeySize = 256, Key = key, Mode = CipherMode.CBC, Padding = PaddingMode.PKCS7 })
            {
                aes.GenerateIV();
                using (var encryptor = aes.CreateEncryptor())
                using (var ms = new MemoryStream())
                {
                    // IV is not secret - it only needs to be unique per encryption, so it's
                    // simply prefixed to the ciphertext and read back on decrypt.
                    ms.Write(aes.IV, 0, aes.IV.Length);
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        byte[] plainBytes = Encoding.UTF8.GetBytes(plaintext);
                        cs.Write(plainBytes, 0, plainBytes.Length);
                        cs.FlushFinalBlock();
                        return Convert.ToBase64String(ms.ToArray());
                    }
                }
            }
        }

        public static string Decrypt(string base64)
        {
            if (string.IsNullOrEmpty(base64)) return "";
            byte[] key = GetKey();
            byte[] full = Convert.FromBase64String(base64);

            using (var aes = new AesManaged { KeySize = 256, Key = key, Mode = CipherMode.CBC, Padding = PaddingMode.PKCS7 })
            {
                byte[] iv = new byte[16];
                Array.Copy(full, 0, iv, 0, 16);
                aes.IV = iv;

                using (var decryptor = aes.CreateDecryptor())
                using (var ms = new MemoryStream(full, 16, full.Length - 16))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var reader = new StreamReader(cs, Encoding.UTF8))
                    return reader.ReadToEnd();
            }
        }

        private static byte[] GetKey()
        {
            if (_cachedKey != null) return _cachedKey;

            lock (Lock)
            {
                if (_cachedKey != null) return _cachedKey;

                string saltBase64 = SettingsRepo.Get("LogEncryptionSalt");
                byte[] salt;
                if (string.IsNullOrEmpty(saltBase64))
                {
                    salt = new byte[16];
                    using (var rng = RandomNumberGenerator.Create())
                        rng.GetBytes(salt);
                    saltBase64 = Convert.ToBase64String(salt);
                    SettingsRepo.Set("LogEncryptionSalt", saltBase64); // generated once, then persisted
                }
                else
                {
                    salt = Convert.FromBase64String(saltBase64);
                }

                // 3-arg Rfc2898DeriveBytes only - .NET Framework 4.6 doesn't have the
                // 4-arg/HashAlgorithmName overload (see PasswordService for the same note).
                using (var pbkdf2 = new Rfc2898DeriveBytes(AppPepper, salt, Iterations))
                    _cachedKey = pbkdf2.GetBytes(KeySizeBytes);

                return _cachedKey;
            }
        }
    }
}

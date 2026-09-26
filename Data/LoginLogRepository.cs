using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.Text;
using PhotoStudioApp.Models;
using PhotoStudioApp.Services;

namespace PhotoStudioApp.Data
{
    /// <summary>Stores every admin-password prompt shown anywhere in the app. ActionName,
    /// ButtonLabel, AttemptsMade, Success and Details are all encrypted at rest as one blob
    /// (see LogCryptoService) - only Id and the plain UTC timestamp are stored in the clear,
    /// since the timestamp alone is enough to enforce the configured retention period without
    /// decrypting every row.</summary>
    public class LoginLogRepository
    {
        public void Insert(LoginLogEntry entry)
        {
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand(
                "INSERT INTO LoginLogs (CreatedUtc, Payload) VALUES (@c, @p);", conn))
            {
                cmd.Parameters.AddWithValue("@c", entry.Timestamp.ToUniversalTime().ToString("o"));
                cmd.Parameters.AddWithValue("@p", LogCryptoService.Encrypt(Encode(entry)));
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>Returns every entry, newest first. Expected volume is small (roughly one row
        /// per password prompt), so loading and decrypting everything here - rather than
        /// filtering with SQL, which the encrypted Payload column can't support - is fine; the
        /// UI applies date/attempts/action filters and search itself after this.</summary>
        public List<LoginLogEntry> GetAll()
        {
            var list = new List<LoginLogEntry>();
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand("SELECT Id, CreatedUtc, Payload FROM LoginLogs ORDER BY Id DESC;", conn))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    LoginLogEntry entry;
                    try
                    {
                        entry = Decode(r.GetString(2));
                    }
                    catch
                    {
                        // A row that fails to decrypt/parse (e.g. the encryption salt setting
                        // was ever cleared by hand) is skipped rather than crashing the whole
                        // log view - one unreadable row shouldn't hide every other one.
                        continue;
                    }
                    entry.Id = r.GetInt32(0);
                    entry.Timestamp = DateTime.Parse(r.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToLocalTime();
                    list.Add(entry);
                }
            }
            return list;
        }

        /// <summary>Deletes entries older than the given number of days. 0 or less means the
        /// retention feature is off (nothing is ever auto-deleted) - matches the
        /// ArchiveRetentionDays convention elsewhere in this app.</summary>
        public int DeleteOlderThan(int retentionDays)
        {
            if (retentionDays <= 0) return 0;
            DateTime cutoff = DateTime.UtcNow.AddDays(-retentionDays);
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand("DELETE FROM LoginLogs WHERE CreatedUtc < @cutoff;", conn))
            {
                cmd.Parameters.AddWithValue("@cutoff", cutoff.ToString("o"));
                return cmd.ExecuteNonQuery();
            }
        }

        public void DeleteAll()
        {
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand("DELETE FROM LoginLogs;", conn))
                cmd.ExecuteNonQuery();
        }

        // Each field is individually base64-encoded, then joined with '|' - base64's restricted
        // character set can never contain '|', so no separate escaping logic is needed for
        // arbitrary content (accents, punctuation, someone typing a '|' into a details field...).
        private static string Encode(LoginLogEntry e)
        {
            return string.Join("|",
                B64(e.ActionName), B64(e.ButtonLabel), e.AttemptsMade.ToString(CultureInfo.InvariantCulture),
                e.Success ? "1" : "0", B64(e.Details));
        }

        private static LoginLogEntry Decode(string encryptedPayload)
        {
            string plain = LogCryptoService.Decrypt(encryptedPayload);
            string[] parts = plain.Split('|');
            return new LoginLogEntry
            {
                ActionName = parts.Length > 0 ? UnB64(parts[0]) : "",
                ButtonLabel = parts.Length > 1 ? UnB64(parts[1]) : "",
                AttemptsMade = parts.Length > 2 && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0,
                Success = parts.Length > 3 && parts[3] == "1",
                Details = parts.Length > 4 ? UnB64(parts[4]) : ""
            };
        }

        private static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s ?? ""));
        private static string UnB64(string s) => string.IsNullOrEmpty(s) ? "" : Encoding.UTF8.GetString(Convert.FromBase64String(s));
    }
}

using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.Text;
using PhotoStudioApp.Models;
using PhotoStudioApp.Services;

namespace PhotoStudioApp.Data
{
    /// <summary>Stores every file-level event inside the studio's category folders (a photo
    /// entering a folder, being removed, renamed, purged, or archived). FileName, Action,
    /// CategoryName and Details are all encrypted at rest as one blob (see LogCryptoService) -
    /// only Id and the plain UTC timestamp are stored in the clear, for the same reason as
    /// LoginLogRepository.</summary>
    public class StudioActionLogRepository
    {
        public void Insert(StudioActionLogEntry entry)
        {
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand(
                "INSERT INTO StudioActionLogs (CreatedUtc, Payload) VALUES (@c, @p);", conn))
            {
                cmd.Parameters.AddWithValue("@c", entry.Timestamp.ToUniversalTime().ToString("o"));
                cmd.Parameters.AddWithValue("@p", LogCryptoService.Encrypt(Encode(entry)));
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>Returns every entry, newest first. See LoginLogRepository.GetAll for why
        /// filtering happens in the UI rather than in SQL here.</summary>
        public List<StudioActionLogEntry> GetAll()
        {
            var list = new List<StudioActionLogEntry>();
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand("SELECT Id, CreatedUtc, Payload FROM StudioActionLogs ORDER BY Id DESC;", conn))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    StudioActionLogEntry entry;
                    try
                    {
                        entry = Decode(r.GetString(2));
                    }
                    catch
                    {
                        continue; // one unreadable row shouldn't hide every other one
                    }
                    entry.Id = r.GetInt32(0);
                    entry.Timestamp = DateTime.Parse(r.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToLocalTime();
                    list.Add(entry);
                }
            }
            return list;
        }

        public int DeleteOlderThan(int retentionDays)
        {
            if (retentionDays <= 0) return 0;
            DateTime cutoff = DateTime.UtcNow.AddDays(-retentionDays);
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand("DELETE FROM StudioActionLogs WHERE CreatedUtc < @cutoff;", conn))
            {
                cmd.Parameters.AddWithValue("@cutoff", cutoff.ToString("o"));
                return cmd.ExecuteNonQuery();
            }
        }

        public void DeleteAll()
        {
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand("DELETE FROM StudioActionLogs;", conn))
                cmd.ExecuteNonQuery();
        }

        private static string Encode(StudioActionLogEntry e)
        {
            return string.Join("|", B64(e.FileName), B64(e.Action), B64(e.CategoryName), B64(e.Details));
        }

        private static StudioActionLogEntry Decode(string encryptedPayload)
        {
            string plain = LogCryptoService.Decrypt(encryptedPayload);
            string[] parts = plain.Split('|');
            return new StudioActionLogEntry
            {
                FileName = parts.Length > 0 ? UnB64(parts[0]) : "",
                Action = parts.Length > 1 ? UnB64(parts[1]) : "",
                CategoryName = parts.Length > 2 ? UnB64(parts[2]) : "",
                Details = parts.Length > 3 ? UnB64(parts[3]) : ""
            };
        }

        private static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s ?? ""));
        private static string UnB64(string s) => string.IsNullOrEmpty(s) ? "" : Encoding.UTF8.GetString(Convert.FromBase64String(s));
    }
}

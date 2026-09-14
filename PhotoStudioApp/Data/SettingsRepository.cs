using System.Data.SQLite;
using PhotoStudioApp.Models;

namespace PhotoStudioApp.Data
{
    public class SettingsRepository
    {
        public string Get(string key, string fallback = null)
        {
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand("SELECT Value FROM Settings WHERE Key=@k;", conn))
            {
                cmd.Parameters.AddWithValue("@k", key);
                var result = cmd.ExecuteScalar();
                return result == null || result == System.DBNull.Value ? fallback : (string)result;
            }
        }

        public void Set(string key, string value)
        {
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand(@"
                INSERT INTO Settings (Key, Value) VALUES (@k, @v)
                ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value;", conn))
            {
                cmd.Parameters.AddWithValue("@k", key);
                cmd.Parameters.AddWithValue("@v", (object)value ?? System.DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }

        public AppSettings LoadAll()
        {
            return new AppSettings
            {
                StudioRootPath = Get("StudioRootPath"),
                ArchiveRootPath = Get("ArchiveRootPath"),
                DateFolderFormat = Get("DateFolderFormat", "dd MMMM"),
                PasswordHash = Get("PasswordHash"),
                PasswordSalt = Get("PasswordSalt"),
                ClosedWeekdaysCsv = Get("ClosedWeekdaysCsv", "Friday"),
                CurrentWorkDate = Get("CurrentWorkDate"),
                MaxPasswordAttempts = int.TryParse(Get("MaxPasswordAttempts", "5"), out var n) ? n : 5,
                PurgedFilePrefixesCsv = Get("PurgedFilePrefixesCsv", "re,GSW"),
                ArchiveRetentionDays = int.TryParse(Get("ArchiveRetentionDays", "0"), out var ard) ? ard : 0,
                WorkerArchiveFeatureEnabled = Get("WorkerArchiveFeatureEnabled", "0") == "1",
                WorkerArchiveArmed = Get("WorkerArchiveArmed", "0") == "1"
            };
        }

        public void SaveAll(AppSettings s)
        {
            Set("StudioRootPath", s.StudioRootPath);
            Set("ArchiveRootPath", s.ArchiveRootPath);
            Set("DateFolderFormat", s.DateFolderFormat);
            Set("PasswordHash", s.PasswordHash);
            Set("PasswordSalt", s.PasswordSalt);
            Set("ClosedWeekdaysCsv", s.ClosedWeekdaysCsv);
            Set("CurrentWorkDate", s.CurrentWorkDate);
            Set("MaxPasswordAttempts", s.MaxPasswordAttempts.ToString());
            Set("PurgedFilePrefixesCsv", s.PurgedFilePrefixesCsv);
            Set("ArchiveRetentionDays", s.ArchiveRetentionDays.ToString());
            Set("WorkerArchiveFeatureEnabled", s.WorkerArchiveFeatureEnabled ? "1" : "0");
            Set("WorkerArchiveArmed", s.WorkerArchiveArmed ? "1" : "0");
        }
    }
}

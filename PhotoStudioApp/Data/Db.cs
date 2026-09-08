using System.Data.SQLite;

namespace PhotoStudioApp.Data
{
    /// <summary>Central place that knows the DB file path and hands out open connections.</summary>
    public static class Db
    {
        public static string DatabasePath { get; set; }

        public static SQLiteConnection OpenConnection()
        {
            var csb = new SQLiteConnectionStringBuilder
            {
                DataSource = DatabasePath,
                ForeignKeys = true,
                JournalMode = SQLiteJournalModeEnum.Wal
            };
            var conn = new SQLiteConnection(csb.ConnectionString);
            conn.Open();
            return conn;
        }
    }
}

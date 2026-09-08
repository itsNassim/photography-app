using System;
using System.Data.SQLite;
using System.IO;

namespace PhotoStudioApp.Data
{
    public static class SchemaInitializer
    {
        public static void EnsureDatabase(string dbPath)
        {
            Db.DatabasePath = dbPath;
            bool isNew = !File.Exists(dbPath);

            if (isNew)
                SQLiteConnection.CreateFile(dbPath);

            using (var conn = Db.OpenConnection())
            using (var tx = conn.BeginTransaction())
            {
                Exec(conn, @"
                    CREATE TABLE IF NOT EXISTS Settings (
                        Key   TEXT PRIMARY KEY,
                        Value TEXT
                    );");

                Exec(conn, @"
                    CREATE TABLE IF NOT EXISTS Categories (
                        Id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name                TEXT NOT NULL,
                        FolderRelativePath  TEXT NOT NULL,
                        IsCustom            INTEGER NOT NULL DEFAULT 1,
                        IsActive            INTEGER NOT NULL DEFAULT 1
                    );");

                Exec(conn, @"
                    CREATE TABLE IF NOT EXISTS CategoryPriceHistory (
                        Id             INTEGER PRIMARY KEY AUTOINCREMENT,
                        CategoryId     INTEGER NOT NULL REFERENCES Categories(Id),
                        Price          REAL NOT NULL,
                        EffectiveFrom  TEXT NOT NULL
                    );");

                Exec(conn, @"
                    CREATE TABLE IF NOT EXISTS StockItems (
                        Id                 INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name               TEXT NOT NULL,
                        Category           TEXT,
                        Quantity           INTEGER NOT NULL DEFAULT 0,
                        UnitPrice          REAL NOT NULL DEFAULT 0,
                        LowStockThreshold  INTEGER NOT NULL DEFAULT 0,
                        IsActive           INTEGER NOT NULL DEFAULT 1
                    );");

                Exec(conn, @"
                    CREATE TABLE IF NOT EXISTS CustomCharges (
                        Id                   INTEGER PRIMARY KEY AUTOINCREMENT,
                        WorkDate             TEXT NOT NULL,
                        Description          TEXT NOT NULL,
                        Amount               REAL NOT NULL,
                        LinkedStockItemId    INTEGER REFERENCES StockItems(Id),
                        Quantity             INTEGER,
                        CreatedAt            TEXT NOT NULL
                    );");

                Exec(conn, @"
                    CREATE TABLE IF NOT EXISTS DailyCloses (
                        Id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                        CloseDate           TEXT NOT NULL,
                        ArchiveFolderPath   TEXT NOT NULL,
                        ClosedAt            TEXT NOT NULL,
                        GrandTotal          REAL NOT NULL
                    );");

                Exec(conn, @"
                    CREATE TABLE IF NOT EXISTS DailyCloseLineItems (
                        Id             INTEGER PRIMARY KEY AUTOINCREMENT,
                        DailyCloseId   INTEGER NOT NULL REFERENCES DailyCloses(Id),
                        CategoryId     INTEGER,
                        CategoryName   TEXT NOT NULL,
                        Count          INTEGER NOT NULL,
                        UnitPrice      REAL NOT NULL
                    );");

                Exec(conn, @"
                    CREATE TABLE IF NOT EXISTS DailyCloseCustomCharges (
                        Id             INTEGER PRIMARY KEY AUTOINCREMENT,
                        DailyCloseId   INTEGER NOT NULL REFERENCES DailyCloses(Id),
                        Description    TEXT NOT NULL,
                        Amount         REAL NOT NULL
                    );");

                tx.Commit();
            }

            if (isNew)
                SeedDefaults();
        }

        private static void SeedDefaults()
        {
            var categoryRepo = new CategoryRepository();
            var settingsRepo = new SettingsRepository();

            // Default studio root: a "studio" folder next to the exe's data directory by default.
            // The user changes this on first run via the Settings tab.
            string defaultRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PhotoStudio", "studio");
            string defaultArchive = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PhotoStudio", "archive");

            settingsRepo.Set("StudioRootPath", defaultRoot);
            settingsRepo.Set("ArchiveRootPath", defaultArchive);
            settingsRepo.Set("TodayFolderName", "today");
            settingsRepo.Set("DateFolderFormat", "dd-MMMM");
            settingsRepo.Set("ClosedWeekdaysCsv", "Friday");
            settingsRepo.Set("MaxPasswordAttempts", "5");
            settingsRepo.Set("CurrentWorkDate", DateTime.Today.ToString("yyyy-MM-dd"));
            settingsRepo.Set("PasswordHash", null);
            settingsRepo.Set("PasswordSalt", null);

            // Default/fixed categories exactly as specified.
            categoryRepo.AddCategory("8 identite", "8 identite", isCustom: false, initialPrice: 0m);
            categoryRepo.AddCategory("4 identite", "4 identite", isCustom: false, initialPrice: 0m);
            categoryRepo.AddCategory("4 visa", "4 visa", isCustom: false, initialPrice: 0m);
            categoryRepo.AddCategory("8 visa", "8 visa", isCustom: false, initialPrice: 0m);
            categoryRepo.AddCategory("12 identite (amature)", Path.Combine("amature", "12 identite"), isCustom: false, initialPrice: 0m);
            categoryRepo.AddCategory("16 identite (amature)", Path.Combine("amature", "16 identite"), isCustom: false, initialPrice: 0m);
        }

        private static void Exec(SQLiteConnection conn, string sql)
        {
            using (var cmd = new SQLiteCommand(sql, conn))
                cmd.ExecuteNonQuery();
        }
    }
}

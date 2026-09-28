using System;
using System.Collections.Generic;
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
                        HasFolder           INTEGER NOT NULL DEFAULT 1,
                        FolderRelativePath  TEXT,
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
                        LinkedCategoryId     INTEGER REFERENCES Categories(Id),
                        Quantity             INTEGER,
                        CreatedAt            TEXT NOT NULL
                    );");

                Exec(conn, @"
                    CREATE TABLE IF NOT EXISTS DailyCloses (
                        Id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                        CloseDate           TEXT NOT NULL,
                        ArchiveFolderPath   TEXT NOT NULL,
                        ClosedAt            TEXT NOT NULL,
                        GrandTotal          REAL NOT NULL,
                        TotalExpenses       REAL NOT NULL DEFAULT 0,
                        NetTotal            REAL NOT NULL DEFAULT 0
                    );");

                Exec(conn, @"
                    CREATE TABLE IF NOT EXISTS DailyCloseLineItems (
                        Id             INTEGER PRIMARY KEY AUTOINCREMENT,
                        DailyCloseId   INTEGER NOT NULL REFERENCES DailyCloses(Id),
                        CategoryId     INTEGER,
                        CategoryName   TEXT NOT NULL,
                        Count          INTEGER NOT NULL,
                        UnitPrice      REAL NOT NULL,
                        PurgedCount    INTEGER NOT NULL DEFAULT 0
                    );");

                Exec(conn, @"
                    CREATE TABLE IF NOT EXISTS DailyCloseCustomCharges (
                        Id             INTEGER PRIMARY KEY AUTOINCREMENT,
                        DailyCloseId   INTEGER NOT NULL REFERENCES DailyCloses(Id),
                        Description    TEXT NOT NULL,
                        Amount         REAL NOT NULL
                    );");

                Exec(conn, @"
                    CREATE TABLE IF NOT EXISTS PendingBalances (
                        Id               INTEGER PRIMARY KEY AUTOINCREMENT,
                        Description      TEXT NOT NULL,
                        TotalAmount      REAL NOT NULL DEFAULT 0,
                        RemainingAmount  REAL NOT NULL,
                        CreatedDate      TEXT NOT NULL
                    );");

                Exec(conn, @"
                    CREATE TABLE IF NOT EXISTS Expenses (
                        Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                        WorkDate     TEXT NOT NULL,
                        Description  TEXT NOT NULL,
                        Amount       REAL NOT NULL,
                        CreatedAt    TEXT NOT NULL
                    );");

                Exec(conn, @"
                    CREATE TABLE IF NOT EXISTS DailyCloseExpenses (
                        Id             INTEGER PRIMARY KEY AUTOINCREMENT,
                        DailyCloseId   INTEGER NOT NULL REFERENCES DailyCloses(Id),
                        Description    TEXT NOT NULL,
                        Amount         REAL NOT NULL
                    );");

                // Login logs (every admin-password prompt) and studio action logs (every
                // file-level event in the category folders). CreatedUtc is the only plain/
                // queryable column - Payload holds every other field encrypted as one blob
                // (see LogCryptoService) so the log content itself can't be read outside the
                // app, e.g. by opening this .db file in a generic SQLite browser.
                Exec(conn, @"
                    CREATE TABLE IF NOT EXISTS LoginLogs (
                        Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                        CreatedUtc  TEXT NOT NULL,
                        Payload     TEXT NOT NULL
                    );");

                Exec(conn, @"
                    CREATE TABLE IF NOT EXISTS StudioActionLogs (
                        Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                        CreatedUtc  TEXT NOT NULL,
                        Payload     TEXT NOT NULL
                    );");

                // Manual, month-level expenses (rent, subscriptions, etc.) that aren't tied to
                // any single closed day - CRUD'd from the "Résumé mensuel" sub-tab of Historique.
                Exec(conn, @"
                    CREATE TABLE IF NOT EXISTS MonthlyExpenses (
                        Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                        YearMonth   TEXT NOT NULL,
                        Description TEXT NOT NULL,
                        Amount      REAL NOT NULL
                    );");

                // Optional extras attached to a (service) category - e.g. "10x15 photo" can offer a
                // frame (stock item), "Traitement" and "Assemblage" (other categories) as extras a
                // cashier may tick when adding a charge. Exactly one of AddOnCategoryId /
                // AddOnStockItemId is set per row (enforced in CategoryAddOnRepository).
                Exec(conn, @"
                    CREATE TABLE IF NOT EXISTS CategoryAddOns (
                        Id               INTEGER PRIMARY KEY AUTOINCREMENT,
                        ParentCategoryId INTEGER NOT NULL REFERENCES Categories(Id),
                        AddOnCategoryId  INTEGER REFERENCES Categories(Id),
                        AddOnStockItemId INTEGER REFERENCES StockItems(Id)
                    );");

                tx.Commit();
            }

            // Migration path for databases created by earlier versions of this app, before
            // HasFolder / LinkedCategoryId existed - CREATE TABLE IF NOT EXISTS above won't
            // add columns to an already-existing table, so do that here if needed.
            AddColumnIfMissing("Categories", "HasFolder", "INTEGER NOT NULL DEFAULT 1");
            AddColumnIfMissing("CustomCharges", "LinkedCategoryId", "INTEGER REFERENCES Categories(Id)");
            AddColumnIfMissing("DailyCloseLineItems", "PurgedCount", "INTEGER NOT NULL DEFAULT 0");
            AddColumnIfMissing("DailyCloses", "TotalExpenses", "REAL NOT NULL DEFAULT 0");
            AddColumnIfMissing("DailyCloses", "NetTotal", "REAL NOT NULL DEFAULT 0");
            AddColumnIfMissing("PendingBalances", "TotalAmount", "REAL NOT NULL DEFAULT 0");
            AddColumnIfMissing("MonthlyExpenses", "DateAdded", "TEXT NOT NULL DEFAULT ''");

            // Backfill for MonthlyExpenses rows that existed before DateAdded was added (they
            // get '' from the column's DEFAULT above) - the exact date they were added isn't
            // recoverable, so the 1st of their already-known YearMonth is the best available
            // approximation. Runs every startup, safe/idempotent: only touches rows still at ''.
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand(
                "UPDATE MonthlyExpenses SET DateAdded = YearMonth || '-01' WHERE DateAdded = '';", conn))
                cmd.ExecuteNonQuery();

            // Self-healing backfill (same pattern/lesson as the NetTotal fix above - see
            // areas/photostudio-app.md bug #13): a balance created before TotalAmount existed
            // has TotalAmount=0 from the column's DEFAULT, which is wrong. The true original
            // total isn't recoverable for those old rows (only RemainingAmount was ever
            // tracked), so RemainingAmount is the best available approximation - this only
            // matters for balances that existed before this update and haven't been touched
            // since; every new balance going forward gets its real TotalAmount from
            // AddCustomChargeDialog at creation time. Runs every startup, safe/idempotent: only
            // touches rows where TotalAmount is still 0.
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand(
                "UPDATE PendingBalances SET TotalAmount = RemainingAmount WHERE TotalAmount = 0;", conn))
                cmd.ExecuteNonQuery();

            // SELF-HEALING FIX (not just a one-time migration): a stale NetTotal=0 can exist not
            // only right when the column is first added, but also on any database that already
            // had the column from an earlier build of this app that shipped before NetTotal was
            // being computed correctly at close time. A one-time "only when the column is brand
            // new" backfill misses that second case entirely - the exact bug the user hit,
            // where days closed with an older build stayed stuck at NetTotal=0 forever even
            // after upgrading, while newly-closed days computed correctly. This now runs on
            // EVERY startup and is fully idempotent/safe: it only touches rows where the stored
            // NetTotal is 0 AND that's actually wrong (GrandTotal-TotalExpenses isn't really 0
            // for that row) - a day that's genuinely all-zero (no revenue, no expenses) is
            // correctly left alone, and a day with a normally-computed nonzero NetTotal is never
            // touched since it isn't 0 to begin with.
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand(
                "UPDATE DailyCloses SET NetTotal = GrandTotal - TotalExpenses " +
                "WHERE NetTotal = 0 AND (GrandTotal - TotalExpenses) <> 0;", conn))
                cmd.ExecuteNonQuery();

            // Older databases created Categories.FolderRelativePath as NOT NULL, back when
            // every category had a folder. SQLite can't just drop a NOT NULL constraint with
            // ALTER TABLE, so rebuild the table if that's still the case.
            DropNotNullIfNeeded("Categories", "FolderRelativePath");

            // One-time correction for a spelling mistake in the original default category seed
            // data below ("amature" instead of "amateur") - fixes any database that already
            // seeded the old spelling. Runs every startup and is fully idempotent: once the DB
            // rows and the folder on disk are both already correct, it finds nothing to do.
            MigrateAmatureToAmateur();

            if (isNew)
                SeedDefaults();
        }

        /// <summary>Fixes the "amature" -> "amateur" spelling mistake wherever it shows up in an
        /// existing database: the category Name (e.g. "12 identite (amature)") and
        /// FolderRelativePath (e.g. "amature\12 identite"), plus renaming the physical "amature"
        /// folder on disk to "amateur" if it's still there - which brings every photo already
        /// inside it along for free, since renaming a folder moves its entire contents in one
        /// filesystem operation. Best-effort on the folder rename (e.g. skipped if a file inside
        /// is currently open) - the DB rows are corrected regardless, so at worst the user
        /// renames the folder by hand to match.</summary>
        private static void MigrateAmatureToAmateur()
        {
            var categoryRepo = new CategoryRepository();
            foreach (var cat in categoryRepo.GetAllCategories(includeInactive: true))
            {
                if (cat.Name != null && cat.Name.IndexOf("amature", StringComparison.OrdinalIgnoreCase) >= 0)
                    categoryRepo.RenameCategory(cat.Id, ReplaceCaseInsensitive(cat.Name, "amature", "amateur"));

                if (cat.FolderRelativePath != null && cat.FolderRelativePath.IndexOf("amature", StringComparison.OrdinalIgnoreCase) >= 0)
                    categoryRepo.UpdateFolderPath(cat.Id, cat.HasFolder, ReplaceCaseInsensitive(cat.FolderRelativePath, "amature", "amateur"));
            }

            var settingsRepo = new SettingsRepository();
            string studioRoot = settingsRepo.Get("StudioRootPath");
            if (string.IsNullOrWhiteSpace(studioRoot) || !Directory.Exists(studioRoot))
                return;

            string oldDir = Path.Combine(studioRoot, "amature");
            string newDir = Path.Combine(studioRoot, "amateur");
            if (Directory.Exists(oldDir) && !Directory.Exists(newDir))
            {
                try { Directory.Move(oldDir, newDir); }
                catch { /* best-effort - see summary above */ }
            }
        }

        /// <summary>Replaces the first case-insensitive match of oldValue in input with newValue
        /// (there's only ever one "amature" per Name/FolderRelativePath string above, so a single
        /// replacement is all MigrateAmatureToAmateur needs).</summary>
        private static string ReplaceCaseInsensitive(string input, string oldValue, string newValue)
        {
            int index = input.IndexOf(oldValue, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return input;
            return input.Substring(0, index) + newValue + input.Substring(index + oldValue.Length);
        }

        /// <summary>Plain struct instead of a C# tuple - named-tuple syntax needs the
        /// ValueTuple types, which only became part of mscorlib in .NET Framework 4.7+; on
        /// 4.6 they're simply not there. Field names match what the tuple's named elements
        /// were, so every usage below reads identically. `default(ColumnInfo)` (what
        /// List&lt;T&gt;.Find returns on no match) has Name == null just like the tuple did.</summary>
        private struct ColumnInfo
        {
            public string Name;
            public string Type;
            public bool NotNull;
            public string DefaultValue;
            public bool IsPk;
        }

        /// <summary>If the given column on the given table currently has a NOT NULL constraint,
        /// rebuilds the table without it (SQLite has no direct "ALTER COLUMN" for this), copying
        /// every existing row and every column across unchanged. Safe to call every startup -
        /// it's a no-op once the constraint is already gone.</summary>
        private static void DropNotNullIfNeeded(string table, string column)
        {
            using (var conn = Db.OpenConnection())
            {
                var columns = new List<ColumnInfo>();
                using (var cmd = new SQLiteCommand($"PRAGMA table_info({table});", conn))
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        columns.Add(new ColumnInfo
                        {
                            Name = r.GetString(1),
                            Type = r.GetString(2),
                            NotNull = r.GetInt32(3) == 1,
                            DefaultValue = r.IsDBNull(4) ? null : r.GetValue(4).ToString(),
                            IsPk = r.GetInt32(5) > 0
                        });
                    }
                }

                var targetCol = columns.Find(c => string.Equals(c.Name, column, StringComparison.OrdinalIgnoreCase));
                if (targetCol.Name == null || !targetCol.NotNull)
                    return; // column doesn't exist yet, or is already nullable - nothing to do

                // Other tables (CustomCharges, CategoryPriceHistory, DailyCloseLineItems) reference
                // this table via inline "REFERENCES" foreign keys. PRAGMA foreign_keys=OFF is a
                // documented no-op once a transaction is open, so it MUST be issued here, before
                // BeginTransaction - otherwise DROP TABLE below fails with "FOREIGN KEY constraint
                // failed" even though no row data is actually violating anything.
                Exec(conn, "PRAGMA foreign_keys=OFF;");

                using (var tx = conn.BeginTransaction())
                {
                    string tempTable = table + "_migrating";

                    var colDefs = new List<string>();
                    var colNames = new List<string>();
                    foreach (var c in columns)
                    {
                        colNames.Add(c.Name);
                        string def = $"{c.Name} {c.Type}";
                        if (c.IsPk) def += " PRIMARY KEY AUTOINCREMENT";
                        // Drop NOT NULL specifically on the target column; keep it for every other column.
                        else if (c.NotNull && !string.Equals(c.Name, column, StringComparison.OrdinalIgnoreCase))
                            def += " NOT NULL";
                        if (c.DefaultValue != null)
                            def += $" DEFAULT {c.DefaultValue}";
                        colDefs.Add(def);
                    }

                    Exec(conn, $"CREATE TABLE {tempTable} ({string.Join(", ", colDefs)});");

                    string colList = string.Join(", ", colNames);
                    Exec(conn, $"INSERT INTO {tempTable} ({colList}) SELECT {colList} FROM {table};");

                    Exec(conn, $"DROP TABLE {table};");
                    Exec(conn, $"ALTER TABLE {tempTable} RENAME TO {table};");

                    tx.Commit();
                }

                // Re-enable for the rest of this connection's lifetime (also a no-op-safe call
                // outside a transaction).
                Exec(conn, "PRAGMA foreign_keys=ON;");
            }
        }

        /// <summary>Adds the column if it doesn't already exist. Returns true only when the
        /// column was just added by this call (false if it already existed) - callers use this
        /// to run a one-time backfill exactly once, right when a column is introduced, rather
        /// than every time the app starts.</summary>
        private static bool AddColumnIfMissing(string table, string column, string columnDefSql)
        {
            using (var conn = Db.OpenConnection())
            {
                bool exists = false;
                using (var cmd = new SQLiteCommand($"PRAGMA table_info({table});", conn))
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        // column 1 in PRAGMA table_info output is the column name
                        if (string.Equals(r.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                        {
                            exists = true;
                            break;
                        }
                    }
                }

                if (!exists)
                {
                    using (var cmd = new SQLiteCommand($"ALTER TABLE {table} ADD COLUMN {column} {columnDefSql};", conn))
                        cmd.ExecuteNonQuery();
                    return true;
                }
                return false;
            }
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
            settingsRepo.Set("DateFolderFormat", "dd MMMM");
            settingsRepo.Set("ClosedWeekdaysCsv", "Friday");
            settingsRepo.Set("MaxPasswordAttempts", "5");
            settingsRepo.Set("PasswordLockoutMinutes", "5");
            settingsRepo.Set("PurgedFilePrefixesCsv", "re,GSW");
            settingsRepo.Set("ArchiveRetentionDays", "0");
            settingsRepo.Set("CurrentWorkDate", DateTime.Today.ToString("yyyy-MM-dd"));
            settingsRepo.Set("PasswordHash", null);
            settingsRepo.Set("PasswordSalt", null);

            // Default/fixed categories exactly as specified - all folder-backed.
            categoryRepo.AddCategory("8 identite", true, "8 identite", isCustom: false, initialPrice: 0m);
            categoryRepo.AddCategory("4 identite", true, "4 identite", isCustom: false, initialPrice: 0m);
            categoryRepo.AddCategory("4 visa", true, "4 visa", isCustom: false, initialPrice: 0m);
            categoryRepo.AddCategory("8 visa", true, "8 visa", isCustom: false, initialPrice: 0m);
            categoryRepo.AddCategory("12 identite (amateur)", true, Path.Combine("amateur", "12 identite"), isCustom: false, initialPrice: 0m);
            categoryRepo.AddCategory("16 identite (amateur)", true, Path.Combine("amateur", "16 identite"), isCustom: false, initialPrice: 0m);
        }

        private static void Exec(SQLiteConnection conn, string sql)
        {
            using (var cmd = new SQLiteCommand(sql, conn))
                cmd.ExecuteNonQuery();
        }
    }
}

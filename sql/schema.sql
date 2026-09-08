-- Reference copy of the schema PhotoStudioApp creates automatically on first run
-- (see Data/SchemaInitializer.cs). You do not need to run this by hand - it's here
-- for documentation / DBA reference only.

CREATE TABLE IF NOT EXISTS Settings (
    Key   TEXT PRIMARY KEY,
    Value TEXT
);

CREATE TABLE IF NOT EXISTS Categories (
    Id                  INTEGER PRIMARY KEY AUTOINCREMENT,
    Name                TEXT NOT NULL,
    FolderRelativePath  TEXT NOT NULL,
    IsCustom            INTEGER NOT NULL DEFAULT 1,   -- 0 = one of the 6 fixed categories, 1 = custom
    IsActive            INTEGER NOT NULL DEFAULT 1    -- soft-delete flag
);

-- Every price change inserts a new row here instead of overwriting the old price,
-- so a DailyClose that already happened keeps reporting the price that was live
-- on that day (DailyCloseLineItems stores its own UnitPrice snapshot too).
CREATE TABLE IF NOT EXISTS CategoryPriceHistory (
    Id             INTEGER PRIMARY KEY AUTOINCREMENT,
    CategoryId     INTEGER NOT NULL REFERENCES Categories(Id),
    Price          REAL NOT NULL,
    EffectiveFrom  TEXT NOT NULL   -- ISO datetime; "current price" = latest row <= now
);

CREATE TABLE IF NOT EXISTS StockItems (
    Id                 INTEGER PRIMARY KEY AUTOINCREMENT,
    Name               TEXT NOT NULL,
    Category           TEXT,
    Quantity           INTEGER NOT NULL DEFAULT 0,
    UnitPrice          REAL NOT NULL DEFAULT 0,
    LowStockThreshold  INTEGER NOT NULL DEFAULT 0,
    IsActive           INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS CustomCharges (
    Id                   INTEGER PRIMARY KEY AUTOINCREMENT,
    WorkDate             TEXT NOT NULL,   -- yyyy-MM-dd, the logical "today" this belongs to
    Description          TEXT NOT NULL,
    Amount               REAL NOT NULL,
    LinkedStockItemId    INTEGER REFERENCES StockItems(Id),
    Quantity             INTEGER,
    CreatedAt            TEXT NOT NULL
);

-- One permanent row per closed business day.
CREATE TABLE IF NOT EXISTS DailyCloses (
    Id                  INTEGER PRIMARY KEY AUTOINCREMENT,
    CloseDate           TEXT NOT NULL,
    ArchiveFolderPath   TEXT NOT NULL,
    ClosedAt            TEXT NOT NULL,
    GrandTotal          REAL NOT NULL
);

CREATE TABLE IF NOT EXISTS DailyCloseLineItems (
    Id             INTEGER PRIMARY KEY AUTOINCREMENT,
    DailyCloseId   INTEGER NOT NULL REFERENCES DailyCloses(Id),
    CategoryId     INTEGER,          -- kept even if the category is later deactivated
    CategoryName   TEXT NOT NULL,    -- snapshotted so renames don't rewrite history
    Count          INTEGER NOT NULL,
    UnitPrice      REAL NOT NULL     -- snapshotted price in effect that day
);

CREATE TABLE IF NOT EXISTS DailyCloseCustomCharges (
    Id             INTEGER PRIMARY KEY AUTOINCREMENT,
    DailyCloseId   INTEGER NOT NULL REFERENCES DailyCloses(Id),
    Description    TEXT NOT NULL,
    Amount         REAL NOT NULL
);

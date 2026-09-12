using System;
using System.Collections.Generic;
using System.Data.SQLite;
using PhotoStudioApp.Models;
using PhotoStudioApp.Services;

namespace PhotoStudioApp.Data
{
    public class DailyCloseRepository
    {
        /// <summary>Persists a fully-built DailyClose (with line items and custom charges) inside
        /// the given open transaction, so it commits atomically with the file-move step.</summary>
        public int Save(DailyClose close, SQLiteConnection conn, SQLiteTransaction tx)
        {
            int closeId;
            using (var cmd = new SQLiteCommand(@"
                INSERT INTO DailyCloses (CloseDate, ArchiveFolderPath, ClosedAt, GrandTotal, TotalExpenses, NetTotal)
                VALUES (@date, @archive, @closedAt, @total, @expenses, @net);
                SELECT last_insert_rowid();", conn, tx))
            {
                cmd.Parameters.AddWithValue("@date", close.CloseDate);
                cmd.Parameters.AddWithValue("@archive", close.ArchiveFolderPath);
                cmd.Parameters.AddWithValue("@closedAt", close.ClosedAt.ToString("s"));
                cmd.Parameters.AddWithValue("@total", close.GrandTotal);
                cmd.Parameters.AddWithValue("@expenses", close.TotalExpenses);
                cmd.Parameters.AddWithValue("@net", close.NetTotal);
                closeId = Convert.ToInt32((long)cmd.ExecuteScalar());
            }

            foreach (var li in close.LineItems)
            {
                using (var cmd = new SQLiteCommand(@"
                    INSERT INTO DailyCloseLineItems (DailyCloseId, CategoryId, CategoryName, Count, UnitPrice, PurgedCount)
                    VALUES (@cid, @catId, @catName, @count, @price, @purged);", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@cid", closeId);
                    cmd.Parameters.AddWithValue("@catId", li.CategoryId);
                    cmd.Parameters.AddWithValue("@catName", li.CategoryName);
                    cmd.Parameters.AddWithValue("@count", li.Count);
                    cmd.Parameters.AddWithValue("@price", li.UnitPrice);
                    cmd.Parameters.AddWithValue("@purged", li.PurgedCount);
                    cmd.ExecuteNonQuery();
                }
            }

            foreach (var cc in close.CustomCharges)
            {
                using (var cmd = new SQLiteCommand(@"
                    INSERT INTO DailyCloseCustomCharges (DailyCloseId, Description, Amount)
                    VALUES (@cid, @desc, @amt);", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@cid", closeId);
                    cmd.Parameters.AddWithValue("@desc", cc.Description);
                    cmd.Parameters.AddWithValue("@amt", cc.Amount);
                    cmd.ExecuteNonQuery();
                }
            }

            foreach (var ex in close.Expenses)
            {
                using (var cmd = new SQLiteCommand(@"
                    INSERT INTO DailyCloseExpenses (DailyCloseId, Description, Amount)
                    VALUES (@cid, @desc, @amt);", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@cid", closeId);
                    cmd.Parameters.AddWithValue("@desc", ex.Description);
                    cmd.Parameters.AddWithValue("@amt", ex.Amount);
                    cmd.ExecuteNonQuery();
                }
            }

            close.Id = closeId;
            return closeId;
        }

        public List<DailyClose> GetHistory()
        {
            var list = new List<DailyClose>();
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand(@"
                SELECT Id, CloseDate, ArchiveFolderPath, ClosedAt, GrandTotal, TotalExpenses, NetTotal
                FROM DailyCloses ORDER BY CloseDate DESC;", conn))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    list.Add(new DailyClose
                    {
                        Id = r.GetInt32(0),
                        CloseDate = r.GetString(1),
                        ArchiveFolderPath = r.GetString(2),
                        ClosedAt = DateTime.Parse(r.GetString(3)),
                        GrandTotal = Convert.ToDecimal(r.GetDouble(4)),
                        TotalExpenses = Convert.ToDecimal(r.GetDouble(5)),
                        NetTotal = Convert.ToDecimal(r.GetDouble(6))
                    });
                }
            }
            return list;
        }

        public DailyClose GetFullById(int id)
        {
            DailyClose close;
            using (var conn = Db.OpenConnection())
            {
                using (var cmd = new SQLiteCommand(@"
                    SELECT Id, CloseDate, ArchiveFolderPath, ClosedAt, GrandTotal, TotalExpenses, NetTotal
                    FROM DailyCloses WHERE Id=@id;", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var r = cmd.ExecuteReader())
                    {
                        if (!r.Read()) return null;
                        close = new DailyClose
                        {
                            Id = r.GetInt32(0),
                            CloseDate = r.GetString(1),
                            ArchiveFolderPath = r.GetString(2),
                            ClosedAt = DateTime.Parse(r.GetString(3)),
                            GrandTotal = Convert.ToDecimal(r.GetDouble(4)),
                            TotalExpenses = Convert.ToDecimal(r.GetDouble(5)),
                            NetTotal = Convert.ToDecimal(r.GetDouble(6))
                        };
                    }
                }

                using (var cmd = new SQLiteCommand(@"
                    SELECT CategoryId, CategoryName, Count, UnitPrice, PurgedCount
                    FROM DailyCloseLineItems WHERE DailyCloseId=@id;", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            close.LineItems.Add(new DailyCloseLineItem
                            {
                                CategoryId = r.IsDBNull(0) ? 0 : r.GetInt32(0),
                                CategoryName = r.GetString(1),
                                Count = r.GetInt32(2),
                                UnitPrice = Convert.ToDecimal(r.GetDouble(3)),
                                PurgedCount = r.IsDBNull(4) ? 0 : r.GetInt32(4)
                            });
                        }
                    }
                }

                using (var cmd = new SQLiteCommand(@"
                    SELECT Description, Amount FROM DailyCloseCustomCharges WHERE DailyCloseId=@id;", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            close.CustomCharges.Add(new DailyCloseCustomChargeSnapshot
                            {
                                Description = r.GetString(0),
                                Amount = Convert.ToDecimal(r.GetDouble(1))
                            });
                        }
                    }
                }

                using (var cmd = new SQLiteCommand(@"
                    SELECT Description, Amount FROM DailyCloseExpenses WHERE DailyCloseId=@id;", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            close.Expenses.Add(new DailyCloseExpenseSnapshot
                            {
                                Description = r.GetString(0),
                                Amount = Convert.ToDecimal(r.GetDouble(1))
                            });
                        }
                    }
                }
            }
            return close;
        }

        /// <summary>Aggregates every closed day by calendar month (year+month of CloseDate),
        /// most recent month first - powers the "Resume mensuel" sub-tab.</summary>
        public List<MonthSummary> GetMonthlySummaries()
        {
            var list = new List<MonthSummary>();
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand(@"
                SELECT substr(CloseDate, 1, 7) AS YearMonth, COUNT(*), SUM(GrandTotal), SUM(TotalExpenses), SUM(NetTotal)
                FROM DailyCloses
                GROUP BY YearMonth
                ORDER BY YearMonth DESC;", conn))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    string yearMonth = r.GetString(0);
                    DateTime firstOfMonth;
                    string label = yearMonth;
                    if (DateTime.TryParseExact(yearMonth + "-01", "yyyy-MM-dd", null,
                        System.Globalization.DateTimeStyles.None, out firstOfMonth))
                        label = firstOfMonth.ToString("MMMM yyyy", AppCulture.French);

                    list.Add(new MonthSummary
                    {
                        YearMonth = yearMonth,
                        MonthLabel = label,
                        DaysClosed = r.GetInt32(1),
                        TotalRevenue = Convert.ToDecimal(r.GetDouble(2)),
                        TotalExpenses = Convert.ToDecimal(r.GetDouble(3)),
                        NetRevenue = Convert.ToDecimal(r.GetDouble(4))
                    });
                }
            }
            return list;
        }

        /// <summary>Every closed day (basic fields only - date/total/archive path, no line
        /// items) whose CloseDate falls within the given "yyyy-MM" month, oldest first - used to
        /// print the day-by-day breakdown under a month's summary report.</summary>
        public List<DailyClose> GetClosesForMonth(string yearMonth)
        {
            var list = new List<DailyClose>();
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand(@"
                SELECT Id, CloseDate, ArchiveFolderPath, ClosedAt, GrandTotal, TotalExpenses, NetTotal
                FROM DailyCloses
                WHERE substr(CloseDate, 1, 7) = @ym
                ORDER BY CloseDate ASC;", conn))
            {
                cmd.Parameters.AddWithValue("@ym", yearMonth);
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new DailyClose
                        {
                            Id = r.GetInt32(0),
                            CloseDate = r.GetString(1),
                            ArchiveFolderPath = r.GetString(2),
                            ClosedAt = DateTime.Parse(r.GetString(3)),
                            GrandTotal = Convert.ToDecimal(r.GetDouble(4)),
                            TotalExpenses = Convert.ToDecimal(r.GetDouble(5)),
                            NetTotal = Convert.ToDecimal(r.GetDouble(6))
                        });
                    }
                }
            }
            return list;
        }

        /// <summary>Deletes one closed day's DB record (and its line items / custom charges).
        /// Does NOT touch the filesystem - callers that also need the archive folder removed
        /// should do that themselves (see Services/ArchiveCleanupService).</summary>
        public void DeleteClose(int id)
        {
            using (var conn = Db.OpenConnection())
            using (var tx = conn.BeginTransaction())
            {
                Exec(conn, tx, "DELETE FROM DailyCloseLineItems WHERE DailyCloseId=@id;", id);
                Exec(conn, tx, "DELETE FROM DailyCloseCustomCharges WHERE DailyCloseId=@id;", id);
                Exec(conn, tx, "DELETE FROM DailyCloseExpenses WHERE DailyCloseId=@id;", id);
                Exec(conn, tx, "DELETE FROM DailyCloses WHERE Id=@id;", id);
                tx.Commit();
            }
        }

        /// <summary>Wipes every closed-day record from the database (all history, all line
        /// items, all custom-charge and expense snapshots). Does NOT touch the filesystem - see
        /// Services/ArchiveCleanupService for deleting the actual archived folders.</summary>
        public void DeleteAllHistory()
        {
            using (var conn = Db.OpenConnection())
            using (var tx = conn.BeginTransaction())
            {
                using (var cmd = new SQLiteCommand("DELETE FROM DailyCloseLineItems;", conn, tx))
                    cmd.ExecuteNonQuery();
                using (var cmd = new SQLiteCommand("DELETE FROM DailyCloseCustomCharges;", conn, tx))
                    cmd.ExecuteNonQuery();
                using (var cmd = new SQLiteCommand("DELETE FROM DailyCloseExpenses;", conn, tx))
                    cmd.ExecuteNonQuery();
                using (var cmd = new SQLiteCommand("DELETE FROM DailyCloses;", conn, tx))
                    cmd.ExecuteNonQuery();
                tx.Commit();
            }
        }

        private void Exec(SQLiteConnection conn, SQLiteTransaction tx, string sql, int id)
        {
            using (var cmd = new SQLiteCommand(sql, conn, tx))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }
    }
}

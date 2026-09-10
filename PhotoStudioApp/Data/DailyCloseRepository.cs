using System;
using System.Collections.Generic;
using System.Data.SQLite;
using PhotoStudioApp.Models;

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
                INSERT INTO DailyCloses (CloseDate, ArchiveFolderPath, ClosedAt, GrandTotal)
                VALUES (@date, @archive, @closedAt, @total);
                SELECT last_insert_rowid();", conn, tx))
            {
                cmd.Parameters.AddWithValue("@date", close.CloseDate);
                cmd.Parameters.AddWithValue("@archive", close.ArchiveFolderPath);
                cmd.Parameters.AddWithValue("@closedAt", close.ClosedAt.ToString("s"));
                cmd.Parameters.AddWithValue("@total", close.GrandTotal);
                closeId = Convert.ToInt32((long)cmd.ExecuteScalar());
            }

            foreach (var li in close.LineItems)
            {
                using (var cmd = new SQLiteCommand(@"
                    INSERT INTO DailyCloseLineItems (DailyCloseId, CategoryId, CategoryName, Count, UnitPrice)
                    VALUES (@cid, @catId, @catName, @count, @price);", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@cid", closeId);
                    cmd.Parameters.AddWithValue("@catId", li.CategoryId);
                    cmd.Parameters.AddWithValue("@catName", li.CategoryName);
                    cmd.Parameters.AddWithValue("@count", li.Count);
                    cmd.Parameters.AddWithValue("@price", li.UnitPrice);
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

            close.Id = closeId;
            return closeId;
        }

        public List<DailyClose> GetHistory()
        {
            var list = new List<DailyClose>();
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand(@"
                SELECT Id, CloseDate, ArchiveFolderPath, ClosedAt, GrandTotal
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
                        GrandTotal = Convert.ToDecimal(r.GetDouble(4))
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
                    SELECT Id, CloseDate, ArchiveFolderPath, ClosedAt, GrandTotal
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
                            GrandTotal = Convert.ToDecimal(r.GetDouble(4))
                        };
                    }
                }

                using (var cmd = new SQLiteCommand(@"
                    SELECT CategoryId, CategoryName, Count, UnitPrice
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
                                UnitPrice = Convert.ToDecimal(r.GetDouble(3))
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
            }
            return close;
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
                Exec(conn, tx, "DELETE FROM DailyCloses WHERE Id=@id;", id);
                tx.Commit();
            }
        }

        /// <summary>Wipes every closed-day record from the database (all history, all line
        /// items, all custom-charge snapshots). Does NOT touch the filesystem - see
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

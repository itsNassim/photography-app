using System;
using System.Collections.Generic;
using System.Data.SQLite;
using PhotoStudioApp.Models;

namespace PhotoStudioApp.Data
{
    public class StockRepository
    {
        public List<StockItem> GetAll(bool activeOnly = true)
        {
            var list = new List<StockItem>();
            string sql = @"SELECT Id, Name, Category, Quantity, UnitPrice, LowStockThreshold, IsActive
                            FROM StockItems" + (activeOnly ? " WHERE IsActive = 1" : "") + " ORDER BY Name;";
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand(sql, conn))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    list.Add(new StockItem
                    {
                        Id = r.GetInt32(0),
                        Name = r.GetString(1),
                        Category = r.IsDBNull(2) ? "" : r.GetString(2),
                        Quantity = r.GetInt32(3),
                        UnitPrice = Convert.ToDecimal(r.GetDouble(4)),
                        LowStockThreshold = r.GetInt32(5),
                        IsActive = r.GetInt64(6) == 1
                    });
                }
            }
            return list;
        }

        public StockItem GetById(int id)
        {
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand(@"SELECT Id, Name, Category, Quantity, UnitPrice, LowStockThreshold, IsActive
                                                  FROM StockItems WHERE Id=@id;", conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;
                    return new StockItem
                    {
                        Id = r.GetInt32(0),
                        Name = r.GetString(1),
                        Category = r.IsDBNull(2) ? "" : r.GetString(2),
                        Quantity = r.GetInt32(3),
                        UnitPrice = Convert.ToDecimal(r.GetDouble(4)),
                        LowStockThreshold = r.GetInt32(5),
                        IsActive = r.GetInt64(6) == 1
                    };
                }
            }
        }

        public int Add(StockItem item)
        {
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand(@"
                INSERT INTO StockItems (Name, Category, Quantity, UnitPrice, LowStockThreshold, IsActive)
                VALUES (@name, @cat, @qty, @price, @low, 1);
                SELECT last_insert_rowid();", conn))
            {
                cmd.Parameters.AddWithValue("@name", item.Name);
                cmd.Parameters.AddWithValue("@cat", (object)item.Category ?? "");
                cmd.Parameters.AddWithValue("@qty", item.Quantity);
                cmd.Parameters.AddWithValue("@price", item.UnitPrice);
                cmd.Parameters.AddWithValue("@low", item.LowStockThreshold);
                return Convert.ToInt32((long)cmd.ExecuteScalar());
            }
        }

        public void Update(StockItem item)
        {
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand(@"
                UPDATE StockItems SET Name=@name, Category=@cat, Quantity=@qty,
                       UnitPrice=@price, LowStockThreshold=@low WHERE Id=@id;", conn))
            {
                cmd.Parameters.AddWithValue("@name", item.Name);
                cmd.Parameters.AddWithValue("@cat", (object)item.Category ?? "");
                cmd.Parameters.AddWithValue("@qty", item.Quantity);
                cmd.Parameters.AddWithValue("@price", item.UnitPrice);
                cmd.Parameters.AddWithValue("@low", item.LowStockThreshold);
                cmd.Parameters.AddWithValue("@id", item.Id);
                cmd.ExecuteNonQuery();
            }
        }

        public void Deactivate(int id)
        {
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand("UPDATE StockItems SET IsActive=0 WHERE Id=@id;", conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>Adjusts quantity by delta (negative to consume, positive to restock).
        /// Clamped at 0 so consuming never drives stock negative silently. Returns the new quantity.</summary>
        public int AdjustQuantity(int id, int delta)
        {
            using (var conn = Db.OpenConnection())
            {
                using (var cmd = new SQLiteCommand("UPDATE StockItems SET Quantity = MAX(0, Quantity + @d) WHERE Id=@id;", conn))
                {
                    cmd.Parameters.AddWithValue("@d", delta);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
                using (var cmd2 = new SQLiteCommand("SELECT Quantity FROM StockItems WHERE Id=@id;", conn))
                {
                    cmd2.Parameters.AddWithValue("@id", id);
                    return Convert.ToInt32((long)cmd2.ExecuteScalar());
                }
            }
        }
    }
}

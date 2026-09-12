using System;
using System.Collections.Generic;
using System.Data.SQLite;
using PhotoStudioApp.Models;

namespace PhotoStudioApp.Data
{
    public class CustomChargeRepository
    {
        public List<CustomCharge> GetForWorkDate(string workDate)
        {
            var list = new List<CustomCharge>();
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand(@"
                SELECT cc.Id, cc.WorkDate, cc.Description, cc.Amount, cc.LinkedStockItemId,
                       s.Name, cc.LinkedCategoryId, cat.Name, cc.Quantity, cc.CreatedAt
                FROM CustomCharges cc
                LEFT JOIN StockItems s ON s.Id = cc.LinkedStockItemId
                LEFT JOIN Categories cat ON cat.Id = cc.LinkedCategoryId
                WHERE cc.WorkDate = @wd
                ORDER BY cc.Id;", conn))
            {
                cmd.Parameters.AddWithValue("@wd", workDate);
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new CustomCharge
                        {
                            Id = r.GetInt32(0),
                            WorkDate = r.GetString(1),
                            Description = r.GetString(2),
                            Amount = Convert.ToDecimal(r.GetDouble(3)),
                            LinkedStockItemId = r.IsDBNull(4) ? (int?)null : r.GetInt32(4),
                            LinkedStockItemName = r.IsDBNull(5) ? null : r.GetString(5),
                            LinkedCategoryId = r.IsDBNull(6) ? (int?)null : r.GetInt32(6),
                            LinkedCategoryName = r.IsDBNull(7) ? null : r.GetString(7),
                            Quantity = r.IsDBNull(8) ? (int?)null : r.GetInt32(8),
                            CreatedAt = DateTime.Parse(r.GetString(9))
                        });
                    }
                }
            }
            return list;
        }

        public int Add(CustomCharge c)
        {
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand(@"
                INSERT INTO CustomCharges (WorkDate, Description, Amount, LinkedStockItemId, LinkedCategoryId, Quantity, CreatedAt)
                VALUES (@wd, @desc, @amt, @stock, @cat, @qty, @created);
                SELECT last_insert_rowid();", conn))
            {
                cmd.Parameters.AddWithValue("@wd", c.WorkDate);
                cmd.Parameters.AddWithValue("@desc", c.Description);
                cmd.Parameters.AddWithValue("@amt", c.Amount);
                cmd.Parameters.AddWithValue("@stock", (object)c.LinkedStockItemId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@cat", (object)c.LinkedCategoryId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@qty", (object)c.Quantity ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@created", DateTime.Now.ToString("s"));
                return Convert.ToInt32((long)cmd.ExecuteScalar());
            }
        }

        public CustomCharge GetById(int id)
        {
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand(@"
                SELECT Id, WorkDate, Description, Amount, LinkedStockItemId, LinkedCategoryId, Quantity, CreatedAt
                FROM CustomCharges WHERE Id=@id;", conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;
                    return new CustomCharge
                    {
                        Id = r.GetInt32(0),
                        WorkDate = r.GetString(1),
                        Description = r.GetString(2),
                        Amount = Convert.ToDecimal(r.GetDouble(3)),
                        LinkedStockItemId = r.IsDBNull(4) ? (int?)null : r.GetInt32(4),
                        LinkedCategoryId = r.IsDBNull(5) ? (int?)null : r.GetInt32(5),
                        Quantity = r.IsDBNull(6) ? (int?)null : r.GetInt32(6),
                        CreatedAt = DateTime.Parse(r.GetString(7))
                    };
                }
            }
        }

        public void Update(CustomCharge c)
        {
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand(@"
                UPDATE CustomCharges SET Description=@desc, Amount=@amt,
                       LinkedStockItemId=@stock, LinkedCategoryId=@cat, Quantity=@qty WHERE Id=@id;", conn))
            {
                cmd.Parameters.AddWithValue("@desc", c.Description);
                cmd.Parameters.AddWithValue("@amt", c.Amount);
                cmd.Parameters.AddWithValue("@stock", (object)c.LinkedStockItemId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@cat", (object)c.LinkedCategoryId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@qty", (object)c.Quantity ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@id", c.Id);
                cmd.ExecuteNonQuery();
            }
        }

        public void Delete(int id)
        {
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand("DELETE FROM CustomCharges WHERE Id=@id;", conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }

        public void DeleteAllForWorkDate(string workDate, SQLiteConnection conn, SQLiteTransaction tx)
        {
            using (var cmd = new SQLiteCommand("DELETE FROM CustomCharges WHERE WorkDate=@wd;", conn, tx))
            {
                cmd.Parameters.AddWithValue("@wd", workDate);
                cmd.ExecuteNonQuery();
            }
        }
    }
}

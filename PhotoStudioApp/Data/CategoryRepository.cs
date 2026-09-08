using System;
using System.Collections.Generic;
using System.Data.SQLite;
using PhotoStudioApp.Models;

namespace PhotoStudioApp.Data
{
    public class CategoryRepository
    {
        /// <summary>All active categories with their current (latest-effective) price.</summary>
        public List<Category> GetActiveCategories()
        {
            var list = new List<Category>();
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand(@"
                SELECT c.Id, c.Name, c.FolderRelativePath, c.IsCustom,
                       (SELECT Price FROM CategoryPriceHistory ph
                         WHERE ph.CategoryId = c.Id AND ph.EffectiveFrom <= datetime('now')
                         ORDER BY ph.EffectiveFrom DESC LIMIT 1) AS CurrentPrice
                FROM Categories c
                WHERE c.IsActive = 1
                ORDER BY c.IsCustom, c.Name;", conn))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    list.Add(new Category
                    {
                        Id = r.GetInt32(0),
                        Name = r.GetString(1),
                        FolderRelativePath = r.GetString(2),
                        IsCustom = r.GetInt64(3) == 1,
                        IsActive = true,
                        CurrentPrice = r.IsDBNull(4) ? 0m : Convert.ToDecimal(r.GetDouble(4))
                    });
                }
            }
            return list;
        }

        public List<Category> GetAllCategories(bool includeInactive = true)
        {
            var list = new List<Category>();
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand(@"
                SELECT c.Id, c.Name, c.FolderRelativePath, c.IsCustom, c.IsActive,
                       (SELECT Price FROM CategoryPriceHistory ph
                         WHERE ph.CategoryId = c.Id AND ph.EffectiveFrom <= datetime('now')
                         ORDER BY ph.EffectiveFrom DESC LIMIT 1) AS CurrentPrice
                FROM Categories c
                " + (includeInactive ? "" : "WHERE c.IsActive = 1 ") + @"
                ORDER BY c.IsCustom, c.Name;", conn))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    list.Add(new Category
                    {
                        Id = r.GetInt32(0),
                        Name = r.GetString(1),
                        FolderRelativePath = r.GetString(2),
                        IsCustom = r.GetInt64(3) == 1,
                        IsActive = r.GetInt64(4) == 1,
                        CurrentPrice = r.IsDBNull(5) ? 0m : Convert.ToDecimal(r.GetDouble(5))
                    });
                }
            }
            return list;
        }

        public int AddCategory(string name, string folderRelativePath, bool isCustom, decimal initialPrice)
        {
            using (var conn = Db.OpenConnection())
            using (var tx = conn.BeginTransaction())
            {
                int categoryId;
                using (var cmd = new SQLiteCommand(@"
                    INSERT INTO Categories (Name, FolderRelativePath, IsCustom, IsActive)
                    VALUES (@name, @path, @custom, 1);
                    SELECT last_insert_rowid();", conn))
                {
                    cmd.Parameters.AddWithValue("@name", name);
                    cmd.Parameters.AddWithValue("@path", folderRelativePath);
                    cmd.Parameters.AddWithValue("@custom", isCustom ? 1 : 0);
                    categoryId = Convert.ToInt32((long)cmd.ExecuteScalar());
                }
                InsertPrice(conn, categoryId, initialPrice);
                tx.Commit();
                return categoryId;
            }
        }

        /// <summary>Sets a new price effective now. Historical closes keep whatever price was
        /// in effect on their close date, because they read from CategoryPriceHistory with
        /// EffectiveFrom &lt;= that day's date at close time (snapshotted into DailyCloseLineItems).</summary>
        public void SetPrice(int categoryId, decimal newPrice)
        {
            using (var conn = Db.OpenConnection())
            {
                InsertPrice(conn, categoryId, newPrice);
            }
        }

        private void InsertPrice(SQLiteConnection conn, int categoryId, decimal price)
        {
            using (var cmd = new SQLiteCommand(@"
                INSERT INTO CategoryPriceHistory (CategoryId, Price, EffectiveFrom)
                VALUES (@cid, @price, datetime('now'));", conn))
            {
                cmd.Parameters.AddWithValue("@cid", categoryId);
                cmd.Parameters.AddWithValue("@price", price);
                cmd.ExecuteNonQuery();
            }
        }

        public void RenameCategory(int categoryId, string newName)
        {
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand("UPDATE Categories SET Name=@n WHERE Id=@id;", conn))
            {
                cmd.Parameters.AddWithValue("@n", newName);
                cmd.Parameters.AddWithValue("@id", categoryId);
                cmd.ExecuteNonQuery();
            }
        }

        public void UpdateFolderPath(int categoryId, string newRelativePath)
        {
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand("UPDATE Categories SET FolderRelativePath=@p WHERE Id=@id;", conn))
            {
                cmd.Parameters.AddWithValue("@p", newRelativePath);
                cmd.Parameters.AddWithValue("@id", categoryId);
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>Soft-delete only - historical DailyCloseLineItems reference the category
        /// by name/price snapshot already, so they remain intact and reprintable.</summary>
        public void Deactivate(int categoryId)
        {
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand("UPDATE Categories SET IsActive=0 WHERE Id=@id;", conn))
            {
                cmd.Parameters.AddWithValue("@id", categoryId);
                cmd.ExecuteNonQuery();
            }
        }

        public void Reactivate(int categoryId)
        {
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand("UPDATE Categories SET IsActive=1 WHERE Id=@id;", conn))
            {
                cmd.Parameters.AddWithValue("@id", categoryId);
                cmd.ExecuteNonQuery();
            }
        }
    }
}

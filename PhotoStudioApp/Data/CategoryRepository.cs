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
                SELECT c.Id, c.Name, c.HasFolder, c.FolderRelativePath, c.IsCustom,
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
                        HasFolder = r.GetInt64(2) == 1,
                        FolderRelativePath = r.IsDBNull(3) ? null : r.GetString(3),
                        IsCustom = r.GetInt64(4) == 1,
                        IsActive = true,
                        CurrentPrice = r.IsDBNull(5) ? 0m : Convert.ToDecimal(r.GetDouble(5))
                    });
                }
            }
            return list;
        }

        /// <summary>Active, folder-backed categories only - what the Home dashboard counts from disk.</summary>
        public List<Category> GetActiveFolderCategories()
        {
            var list = new List<Category>();
            foreach (var c in GetActiveCategories())
                if (c.HasFolder) list.Add(c);
            return list;
        }

        /// <summary>EVERY folder-backed category, active or not - used only for setting up
        /// studio-action-log watchers (HomeTab.SetupFolderWatchers), so a deactivated category's
        /// folder still gets logged even though it no longer counts toward revenue or shows on
        /// the Home dashboard. Deliberately separate from GetActiveFolderCategories, which is
        /// still what counting/revenue logic should use.</summary>
        public List<Category> GetAllFolderCategories()
        {
            var list = new List<Category>();
            foreach (var c in GetAllCategories(includeInactive: true))
                if (c.HasFolder) list.Add(c);
            return list;
        }

        /// <summary>Active, no-folder fixed-price "service" categories only - what the
        /// "add extra charge" screen offers alongside stock items.</summary>
        public List<Category> GetActiveServiceCategories()
        {
            var list = new List<Category>();
            foreach (var c in GetActiveCategories())
                if (!c.HasFolder) list.Add(c);
            return list;
        }

        public List<Category> GetAllCategories(bool includeInactive = true)
        {
            var list = new List<Category>();
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand(@"
                SELECT c.Id, c.Name, c.HasFolder, c.FolderRelativePath, c.IsCustom, c.IsActive,
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
                        HasFolder = r.GetInt64(2) == 1,
                        FolderRelativePath = r.IsDBNull(3) ? null : r.GetString(3),
                        IsCustom = r.GetInt64(4) == 1,
                        IsActive = r.GetInt64(5) == 1,
                        CurrentPrice = r.IsDBNull(6) ? 0m : Convert.ToDecimal(r.GetDouble(6))
                    });
                }
            }
            return list;
        }

        public Category GetById(int id)
        {
            foreach (var c in GetAllCategories(includeInactive: true))
                if (c.Id == id) return c;
            return null;
        }

        public int AddCategory(string name, bool hasFolder, string folderRelativePath, bool isCustom, decimal initialPrice)
        {
            using (var conn = Db.OpenConnection())
            using (var tx = conn.BeginTransaction())
            {
                int categoryId;
                using (var cmd = new SQLiteCommand(@"
                    INSERT INTO Categories (Name, HasFolder, FolderRelativePath, IsCustom, IsActive)
                    VALUES (@name, @hasFolder, @path, @custom, 1);
                    SELECT last_insert_rowid();", conn))
                {
                    cmd.Parameters.AddWithValue("@name", name);
                    cmd.Parameters.AddWithValue("@hasFolder", hasFolder ? 1 : 0);
                    cmd.Parameters.AddWithValue("@path", (object)folderRelativePath ?? DBNull.Value);
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

        /// <summary>Permanently deletes a category, its price history, and any CategoryAddOn
        /// links where it's the parent or an offered extra. Refuses (throws
        /// InvalidOperationException) if the category has any PENDING, not-yet-closed custom
        /// charge today referencing it - close the day or remove those charges first. Already
        /// CLOSED days are never affected: DailyCloseLineItems stores CategoryName/UnitPrice as
        /// plain snapshotted text/values with no foreign key back to Categories, so every past
        /// archived report stays exactly as it was, forever, even after the category itself is
        /// gone.</summary>
        public void Delete(int categoryId)
        {
            using (var conn = Db.OpenConnection())
            using (var tx = conn.BeginTransaction())
            {
                using (var check = new SQLiteCommand(
                    "SELECT COUNT(*) FROM CustomCharges WHERE LinkedCategoryId=@id;", conn, tx))
                {
                    check.Parameters.AddWithValue("@id", categoryId);
                    long pendingCharges = (long)check.ExecuteScalar();
                    if (pendingCharges > 0)
                        throw new InvalidOperationException(
                            "Cette catégorie a des charges additionnelles non encore clôturées aujourd'hui. " +
                            "Clôturez la journée ou supprimez ces charges avant de supprimer la catégorie.");
                }

                using (var cmd = new SQLiteCommand(
                    "DELETE FROM CategoryAddOns WHERE ParentCategoryId=@id OR AddOnCategoryId=@id;", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@id", categoryId);
                    cmd.ExecuteNonQuery();
                }
                using (var cmd = new SQLiteCommand(
                    "DELETE FROM CategoryPriceHistory WHERE CategoryId=@id;", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@id", categoryId);
                    cmd.ExecuteNonQuery();
                }
                using (var cmd = new SQLiteCommand("DELETE FROM Categories WHERE Id=@id;", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@id", categoryId);
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
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

        public void UpdateFolderPath(int categoryId, bool hasFolder, string newRelativePath)
        {
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand("UPDATE Categories SET HasFolder=@h, FolderRelativePath=@p WHERE Id=@id;", conn))
            {
                cmd.Parameters.AddWithValue("@h", hasFolder ? 1 : 0);
                cmd.Parameters.AddWithValue("@p", (object)newRelativePath ?? DBNull.Value);
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

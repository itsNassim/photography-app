using System;
using System.Collections.Generic;
using System.Data.SQLite;
using PhotoStudioApp.Models;

namespace PhotoStudioApp.Data
{
    /// <summary>Persistence for CategoryAddOn - which optional extras (other categories or stock
    /// items) are offered when a given parent category is chosen in "Ajouter une charge".</summary>
    public class CategoryAddOnRepository
    {
        public List<CategoryAddOn> GetForParent(int parentCategoryId)
        {
            var list = new List<CategoryAddOn>();
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand(@"
                SELECT Id, ParentCategoryId, AddOnCategoryId, AddOnStockItemId
                FROM CategoryAddOns WHERE ParentCategoryId=@p ORDER BY Id;", conn))
            {
                cmd.Parameters.AddWithValue("@p", parentCategoryId);
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new CategoryAddOn
                        {
                            Id = r.GetInt32(0),
                            ParentCategoryId = r.GetInt32(1),
                            AddOnCategoryId = r.IsDBNull(2) ? (int?)null : r.GetInt32(2),
                            AddOnStockItemId = r.IsDBNull(3) ? (int?)null : r.GetInt32(3)
                        });
                    }
                }
            }
            return list;
        }

        /// <summary>Number of extras per parent category id - used to show which categories
        /// offer extras (Categories grid, and the hint in the charge dialog's dropdown).</summary>
        public Dictionary<int, int> GetCountsByParent()
        {
            var counts = new Dictionary<int, int>();
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand(
                "SELECT ParentCategoryId, COUNT(*) FROM CategoryAddOns GROUP BY ParentCategoryId;", conn))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                    counts[r.GetInt32(0)] = Convert.ToInt32(r.GetInt64(1));
            }
            return counts;
        }

        /// <summary>Adds an extra to a parent. Silently does nothing if that exact extra is
        /// already attached, so it can never be listed twice.</summary>
        public void Add(int parentCategoryId, int? addOnCategoryId, int? addOnStockItemId)
        {
            if ((addOnCategoryId == null) == (addOnStockItemId == null))
                throw new ArgumentException("Exactly one of addOnCategoryId / addOnStockItemId must be set.");

            using (var conn = Db.OpenConnection())
            {
                using (var check = new SQLiteCommand(@"
                    SELECT COUNT(*) FROM CategoryAddOns
                    WHERE ParentCategoryId=@p AND AddOnCategoryId IS @c AND AddOnStockItemId IS @s;", conn))
                {
                    check.Parameters.AddWithValue("@p", parentCategoryId);
                    check.Parameters.AddWithValue("@c", (object)addOnCategoryId ?? DBNull.Value);
                    check.Parameters.AddWithValue("@s", (object)addOnStockItemId ?? DBNull.Value);
                    if ((long)check.ExecuteScalar() > 0) return;
                }

                using (var cmd = new SQLiteCommand(@"
                    INSERT INTO CategoryAddOns (ParentCategoryId, AddOnCategoryId, AddOnStockItemId)
                    VALUES (@p, @c, @s);", conn))
                {
                    cmd.Parameters.AddWithValue("@p", parentCategoryId);
                    cmd.Parameters.AddWithValue("@c", (object)addOnCategoryId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@s", (object)addOnStockItemId ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void Remove(int addOnId)
        {
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand("DELETE FROM CategoryAddOns WHERE Id=@id;", conn))
            {
                cmd.Parameters.AddWithValue("@id", addOnId);
                cmd.ExecuteNonQuery();
            }
        }
    }
}

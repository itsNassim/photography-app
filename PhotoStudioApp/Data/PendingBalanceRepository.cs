using System;
using System.Collections.Generic;
using System.Data.SQLite;
using PhotoStudioApp.Models;

namespace PhotoStudioApp.Data
{
    public class PendingBalanceRepository
    {
        public List<PendingBalance> GetAll()
        {
            var list = new List<PendingBalance>();
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand(@"
                SELECT Id, Description, RemainingAmount, CreatedDate
                FROM PendingBalances ORDER BY CreatedDate ASC, Id ASC;", conn))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    list.Add(new PendingBalance
                    {
                        Id = r.GetInt32(0),
                        Description = r.GetString(1),
                        RemainingAmount = Convert.ToDecimal(r.GetDouble(2)),
                        CreatedDate = r.GetString(3)
                    });
                }
            }
            return list;
        }

        public PendingBalance GetById(int id)
        {
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand(@"
                SELECT Id, Description, RemainingAmount, CreatedDate
                FROM PendingBalances WHERE Id=@id;", conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;
                    return new PendingBalance
                    {
                        Id = r.GetInt32(0),
                        Description = r.GetString(1),
                        RemainingAmount = Convert.ToDecimal(r.GetDouble(2)),
                        CreatedDate = r.GetString(3)
                    };
                }
            }
        }

        /// <summary>Creates a new pending balance (the unpaid remainder of a charge) and returns
        /// its new Id.</summary>
        public int Add(string description, decimal remainingAmount, string createdDate)
        {
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand(@"
                INSERT INTO PendingBalances (Description, RemainingAmount, CreatedDate)
                VALUES (@desc, @amt, @date);
                SELECT last_insert_rowid();", conn))
            {
                cmd.Parameters.AddWithValue("@desc", description);
                cmd.Parameters.AddWithValue("@amt", remainingAmount);
                cmd.Parameters.AddWithValue("@date", createdDate);
                return Convert.ToInt32((long)cmd.ExecuteScalar());
            }
        }

        /// <summary>Records a payment of amountPaidNow against the balance: reduces
        /// RemainingAmount, or deletes the row entirely once it reaches (approximately) zero.
        /// Returns the remaining amount AFTER the payment (0 if it was fully paid off/deleted).
        /// Clamps amountPaidNow so the balance never goes negative.</summary>
        public decimal ReduceOrDelete(int id, decimal amountPaidNow)
        {
            var current = GetById(id);
            if (current == null) return 0m;

            decimal newRemaining = current.RemainingAmount - amountPaidNow;
            if (newRemaining <= 0.005m)
            {
                Delete(id);
                return 0m;
            }

            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand("UPDATE PendingBalances SET RemainingAmount=@amt WHERE Id=@id;", conn))
            {
                cmd.Parameters.AddWithValue("@amt", newRemaining);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
            return newRemaining;
        }

        public void Delete(int id)
        {
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand("DELETE FROM PendingBalances WHERE Id=@id;", conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Data.SQLite;
using PhotoStudioApp.Models;

namespace PhotoStudioApp.Data
{
    /// <summary>CRUD for MonthlyExpense - manual, month-level expenses (rent, subscriptions,
    /// etc.) not tied to any single closed day. Powers the "Résumé mensuel" sub-tab's Ajouter/
    /// Modifier/Supprimer buttons.</summary>
    public class MonthlyExpenseRepository
    {
        public int Insert(MonthlyExpense expense)
        {
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand(@"
                INSERT INTO MonthlyExpenses (YearMonth, Description, Amount)
                VALUES (@ym, @desc, @amt);
                SELECT last_insert_rowid();", conn))
            {
                cmd.Parameters.AddWithValue("@ym", expense.YearMonth);
                cmd.Parameters.AddWithValue("@desc", expense.Description);
                cmd.Parameters.AddWithValue("@amt", expense.Amount);
                return Convert.ToInt32((long)cmd.ExecuteScalar());
            }
        }

        public void Update(MonthlyExpense expense)
        {
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand(@"
                UPDATE MonthlyExpenses SET Description=@desc, Amount=@amt WHERE Id=@id;", conn))
            {
                cmd.Parameters.AddWithValue("@desc", expense.Description);
                cmd.Parameters.AddWithValue("@amt", expense.Amount);
                cmd.Parameters.AddWithValue("@id", expense.Id);
                cmd.ExecuteNonQuery();
            }
        }

        public void Delete(int id)
        {
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand("DELETE FROM MonthlyExpenses WHERE Id=@id;", conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }

        public List<MonthlyExpense> GetForMonth(string yearMonth)
        {
            var list = new List<MonthlyExpense>();
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand(@"
                SELECT Id, YearMonth, Description, Amount FROM MonthlyExpenses
                WHERE YearMonth=@ym ORDER BY Id ASC;", conn))
            {
                cmd.Parameters.AddWithValue("@ym", yearMonth);
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new MonthlyExpense
                        {
                            Id = r.GetInt32(0),
                            YearMonth = r.GetString(1),
                            Description = r.GetString(2),
                            Amount = Convert.ToDecimal(r.GetDouble(3))
                        });
                    }
                }
            }
            return list;
        }

        /// <summary>Total manual monthly expenses per "yyyy-MM" - used by
        /// DailyCloseRepository.GetMonthlySummaries to fold these into each month's totals.</summary>
        public Dictionary<string, decimal> GetTotalsByMonth()
        {
            var totals = new Dictionary<string, decimal>();
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand(
                "SELECT YearMonth, SUM(Amount) FROM MonthlyExpenses GROUP BY YearMonth;", conn))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                    totals[r.GetString(0)] = Convert.ToDecimal(r.GetDouble(1));
            }
            return totals;
        }

        /// <summary>Deletes every manual monthly expense for the given month - used when a month
        /// is deleted wholesale from "Résumé mensuel" so no orphaned expense rows are left behind.</summary>
        public void DeleteForMonth(string yearMonth)
        {
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand("DELETE FROM MonthlyExpenses WHERE YearMonth=@ym;", conn))
            {
                cmd.Parameters.AddWithValue("@ym", yearMonth);
                cmd.ExecuteNonQuery();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Data.SQLite;
using PhotoStudioApp.Models;

namespace PhotoStudioApp.Data
{
    public class ExpenseRepository
    {
        public List<Expense> GetForWorkDate(string workDate)
        {
            var list = new List<Expense>();
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand(@"
                SELECT Id, WorkDate, Description, Amount, CreatedAt
                FROM Expenses WHERE WorkDate=@wd ORDER BY Id;", conn))
            {
                cmd.Parameters.AddWithValue("@wd", workDate);
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new Expense
                        {
                            Id = r.GetInt32(0),
                            WorkDate = r.GetString(1),
                            Description = r.GetString(2),
                            Amount = Convert.ToDecimal(r.GetDouble(3)),
                            CreatedAt = DateTime.Parse(r.GetString(4))
                        });
                    }
                }
            }
            return list;
        }

        public int Add(Expense e)
        {
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand(@"
                INSERT INTO Expenses (WorkDate, Description, Amount, CreatedAt)
                VALUES (@wd, @desc, @amt, @created);
                SELECT last_insert_rowid();", conn))
            {
                cmd.Parameters.AddWithValue("@wd", e.WorkDate);
                cmd.Parameters.AddWithValue("@desc", e.Description);
                cmd.Parameters.AddWithValue("@amt", e.Amount);
                cmd.Parameters.AddWithValue("@created", DateTime.Now.ToString("s"));
                return Convert.ToInt32((long)cmd.ExecuteScalar());
            }
        }

        public Expense GetById(int id)
        {
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand(@"
                SELECT Id, WorkDate, Description, Amount, CreatedAt
                FROM Expenses WHERE Id=@id;", conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;
                    return new Expense
                    {
                        Id = r.GetInt32(0),
                        WorkDate = r.GetString(1),
                        Description = r.GetString(2),
                        Amount = Convert.ToDecimal(r.GetDouble(3)),
                        CreatedAt = DateTime.Parse(r.GetString(4))
                    };
                }
            }
        }

        public void Update(Expense e)
        {
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand("UPDATE Expenses SET Description=@desc, Amount=@amt WHERE Id=@id;", conn))
            {
                cmd.Parameters.AddWithValue("@desc", e.Description);
                cmd.Parameters.AddWithValue("@amt", e.Amount);
                cmd.Parameters.AddWithValue("@id", e.Id);
                cmd.ExecuteNonQuery();
            }
        }

        public void Delete(int id)
        {
            using (var conn = Db.OpenConnection())
            using (var cmd = new SQLiteCommand("DELETE FROM Expenses WHERE Id=@id;", conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }

        public void DeleteAllForWorkDate(string workDate, SQLiteConnection conn, SQLiteTransaction tx)
        {
            using (var cmd = new SQLiteCommand("DELETE FROM Expenses WHERE WorkDate=@wd;", conn, tx))
            {
                cmd.Parameters.AddWithValue("@wd", workDate);
                cmd.ExecuteNonQuery();
            }
        }
    }
}

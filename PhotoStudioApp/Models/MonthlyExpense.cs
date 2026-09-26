namespace PhotoStudioApp.Models
{
    /// <summary>A manual expense entered against a whole calendar month rather than a single
    /// closed day (rent, a subscription, a one-off purchase not tied to one day's production) -
    /// CRUD'd from the "Résumé mensuel" sub-tab of Historique and folded into that month's
    /// Dépenses/Net totals alongside the per-day expenses recorded at close time.</summary>
    public class MonthlyExpense
    {
        public int Id { get; set; }
        /// <summary>"yyyy-MM", same format as MonthSummary.YearMonth.</summary>
        public string YearMonth { get; set; }
        public string Description { get; set; }
        public decimal Amount { get; set; }
    }
}

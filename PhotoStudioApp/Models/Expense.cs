using System;

namespace PhotoStudioApp.Models
{
    /// <summary>Money the studio/a worker spent that day (e.g. buying supplies for the store) -
    /// tracked separately from revenue and subtracted at close time to get a net total.</summary>
    public class Expense
    {
        public int Id { get; set; }
        public string WorkDate { get; set; }
        public string Description { get; set; }
        public decimal Amount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

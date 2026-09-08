using System;

namespace PhotoStudioApp.Models
{
    /// <summary>A one-off charge added to "today", optionally consuming a stock item.</summary>
    public class CustomCharge
    {
        public int Id { get; set; }
        public string WorkDate { get; set; } // yyyy-MM-dd, the logical "today" this belongs to
        public string Description { get; set; }
        public decimal Amount { get; set; }
        public int? LinkedStockItemId { get; set; }
        public string LinkedStockItemName { get; set; } // convenience for display
        public int? Quantity { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

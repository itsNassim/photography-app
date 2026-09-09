using System;

namespace PhotoStudioApp.Models
{
    /// <summary>A one-off charge added to "today", optionally consuming a stock item OR
    /// computed from a fixed-price, no-folder "service" category (quantity x price).
    /// Only one of LinkedStockItemId / LinkedCategoryId is normally set at a time.</summary>
    public class CustomCharge
    {
        public int Id { get; set; }
        public string WorkDate { get; set; } // yyyy-MM-dd, the logical "today" this belongs to
        public string Description { get; set; }
        public decimal Amount { get; set; }
        public int? LinkedStockItemId { get; set; }
        public string LinkedStockItemName { get; set; } // convenience for display
        public int? LinkedCategoryId { get; set; }
        public string LinkedCategoryName { get; set; } // convenience for display
        public int? Quantity { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}


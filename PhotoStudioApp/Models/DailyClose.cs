using System;
using System.Collections.Generic;

namespace PhotoStudioApp.Models
{
    public class DailyCloseLineItem
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public int Count { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal => Count * UnitPrice;
    }

    public class DailyCloseCustomChargeSnapshot
    {
        public string Description { get; set; }
        public decimal Amount { get; set; }
    }

    /// <summary>A permanent, immutable record of one closed business day.</summary>
    public class DailyClose
    {
        public int Id { get; set; }
        public string CloseDate { get; set; } // logical work date that was closed, yyyy-MM-dd
        public string ArchiveFolderPath { get; set; }
        public DateTime ClosedAt { get; set; }
        public decimal GrandTotal { get; set; }

        public List<DailyCloseLineItem> LineItems { get; set; } = new List<DailyCloseLineItem>();
        public List<DailyCloseCustomChargeSnapshot> CustomCharges { get; set; } = new List<DailyCloseCustomChargeSnapshot>();
    }
}

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

        /// <summary>How many rejected/duplicate files (matching a configured purge prefix like
        /// "re" or "GSW") were found and counted in this category's folder BEFORE they were
        /// deleted at close time. This is purely an audit trail - purged files are never counted
        /// in Count/Subtotal above - it just proves they were tallied, not silently discarded.</summary>
        public int PurgedCount { get; set; }
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

    /// <summary>Aggregated revenue for one calendar month, built from every DailyClose whose
    /// CloseDate falls in that month - used by the "Resume mensuel" sub-tab in History/Archive.</summary>
    public class MonthSummary
    {
        public string YearMonth { get; set; } // "yyyy-MM"
        public string MonthLabel { get; set; } // e.g. "septembre 2026", French
        public int DaysClosed { get; set; }
        public decimal TotalRevenue { get; set; }
    }
}

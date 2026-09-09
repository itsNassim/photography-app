using System;

using System;

namespace PhotoStudioApp.Models
{
    /// <summary>
    /// A production category (fixed or custom), e.g. "8 identite".
    /// FolderRelativePath is relative to the configured studio root, e.g. "8 identite"
    /// or "amature\12 identite" for nested folders.
    ///
    /// Some categories have no physical folder at all (HasFolder = false) - these are
    /// fixed-price "service" items (e.g. printing photos from a customer's phone at a
    /// fixed price per print). They're never counted from disk; they're only used from
    /// the "add extra charge" screen, where the staff types a quantity and the amount is
    /// computed automatically.
    /// </summary>
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool HasFolder { get; set; } = true;
        public string FolderRelativePath { get; set; }
        public bool IsCustom { get; set; }
        public bool IsActive { get; set; } = true;

        /// <summary>Current price, resolved from CategoryPriceHistory (latest EffectiveFrom &lt;= now).</summary>
        public decimal CurrentPrice { get; set; }

        // Populated for the Home tab dashboard only - not persisted directly.
        public int LiveCount { get; set; }
        public decimal Subtotal => LiveCount * CurrentPrice;
    }
}


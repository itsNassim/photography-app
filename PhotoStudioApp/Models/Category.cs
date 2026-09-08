using System;

namespace PhotoStudioApp.Models
{
    /// <summary>
    /// A production category (fixed or custom), e.g. "8 identite".
    /// FolderRelativePath is relative to the configured studio root, e.g. "8 identite"
    /// or "amature\12 identite" for nested folders.
    /// </summary>
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; }
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

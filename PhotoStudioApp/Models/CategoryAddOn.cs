namespace PhotoStudioApp.Models
{
    /// <summary>Links a parent (service) category to one optional extra a cashier can tick when
    /// adding a charge for that category - either another service category (e.g. "Assemblage",
    /// priced per unit/person) or a stock item (e.g. a frame). Exactly one of AddOnCategoryId /
    /// AddOnStockItemId is set.</summary>
    public class CategoryAddOn
    {
        public int Id { get; set; }
        public int ParentCategoryId { get; set; }
        public int? AddOnCategoryId { get; set; }
        public int? AddOnStockItemId { get; set; }
    }
}

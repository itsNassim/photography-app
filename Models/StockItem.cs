namespace PhotoStudioApp.Models
{
    public class StockItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public int LowStockThreshold { get; set; } = 0;
        public bool IsActive { get; set; } = true;

        public bool IsOutOfStock => Quantity <= 0;
        public bool IsLowStock => Quantity > 0 && Quantity <= LowStockThreshold;
    }
}

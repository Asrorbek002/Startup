namespace ShopManagementSystem.Entities;

public class Product
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal BuyPrice { get; set; }
    public decimal SellPrice { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
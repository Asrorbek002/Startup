namespace ShopManagementSystem.Entities;

public class Debtor
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
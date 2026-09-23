namespace ShopManagementSystem.Entities;

public class Element
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Length { get; set; }
    public decimal BuyPrice { get; set; }   // Tan narx (joriy)
    public decimal SellPrice { get; set; }  // Sotish narx (joriy) — xodim kabinetida shu ishlatiladi
    public string? ImageUrl { get; set; }   // Mahsulot surati (boshliq yuklaydi, xodim kabinetida shu ko'rsatiladi)
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public int ShopId { get; set; }
    public Shop? Shop { get; set; }
}
namespace ShopManagementSystem.Entities;

public class Sale
{
    public int Id { get; set; }
    public int ShopId { get; set; }              // Qaysi savdo nuqtasiga (do'konga) tegishli
    public int EmployeeId { get; set; }           // Sotuvni amalga oshirgan xodim
    public string EmployeeName { get; set; } = string.Empty; // Xodim ismi (tezkor ko'rsatish uchun)
    public string MenuCategory { get; set; } = string.Empty; // Menyu (Masalan: Hot-Dog, Pitsa)
    public int ProductId { get; set; }                        // Qaysi mahsulotga tegishli (tahrirlash uchun kerak)
    public string ProductName { get; set; } = string.Empty;  // Tovar nomi (Masalan: Coca Cola 1L)
    public int Quantity { get; set; }                        // Soni
    public decimal CostPrice { get; set; }                   // Tan narxi
    public decimal SalePrice { get; set; }                   // Sotish narxi (xodim haqiqatda sotgan narx)
    public decimal ListedPrice { get; set; }                 // Sotuv paytida belgilangan (standart) narx
    public decimal TotalSum => Quantity * SalePrice;         // Umumiy summa
    public decimal Profit => TotalSum - (Quantity * CostPrice); // Foyda (Yalpi daromad)
    // Belgilangan narxdan pastga sotilgan bo'lsa — shu farq (zarar), aks holda 0
    public decimal PriceDrop => ListedPrice > SalePrice ? (ListedPrice - SalePrice) * Quantity : 0;
    public DateTime SoldAt { get; set; } = DateTime.UtcNow;  // Sotilgan vaqti (Sana va vaqt filtrlari uchun)

    // ---- Qarzga sotish uchun qo'shimcha maydonlar ----
    public bool IsCredit { get; set; } = false;   // true bo'lsa — bu savdo naqd emas, qarzga sotilgan
    public int? DebtorId { get; set; }            // Qaysi qarzdorga bog'langan (IsCredit=true bo'lsa to'ldiriladi)
    public string? DebtorName { get; set; }       // Qarzdor ismi (tezkor ko'rsatish uchun, tarix o'zgarmasin deb saqlanadi)
}
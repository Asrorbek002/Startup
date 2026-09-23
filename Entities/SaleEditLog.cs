namespace ShopManagementSystem.Entities;

// Bir savdo yozuvi tahrirlanganda shu yerga yoziladi — boshliq kabinetida
// "kim, nimani, qachon tahrirlagani"ni ko'rsatish uchun ishlatiladi.
public class SaleEditLog
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public int SaleId { get; set; }               // Qaysi savdo tahrirlangani

    public int EmployeeId { get; set; }           // Tahrirlagan xodim
    public string EmployeeName { get; set; } = string.Empty;

    // Tahrirlashdan OLDINGI holat
    public string OldProductName { get; set; } = string.Empty;
    public int OldQuantity { get; set; }
    public decimal OldSalePrice { get; set; }
    public decimal OldTotalSum { get; set; }

    // Tahrirlashdan KEYINGI holat
    public string NewProductName { get; set; } = string.Empty;
    public int NewQuantity { get; set; }
    public decimal NewSalePrice { get; set; }
    public decimal NewTotalSum { get; set; }

    public DateTime EditedAt { get; set; } = DateTime.UtcNow;   // Qachon tahrirlangani
}

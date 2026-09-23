namespace ShopManagementSystem.Entities;

public class Employee
{
    public int Id { get; set; }
    public int ShopId { get; set; }              // Qaysi savdo nuqtasiga (do'konga) tegishli
    public string FullName { get; set; } = string.Empty; // To'liq ismi (F.I.O)
    public string Username { get; set; } = string.Empty; // Login
    public string PasswordHash { get; set; } = string.Empty; // Shifrlangan parol
    public string Phone { get; set; } = string.Empty;    // Telefon raqami
    public string Note { get; set; } = string.Empty;     // Izoh
    public string Role { get; set; } = string.Empty;     // Roli (Kassir, Kuryer, Ofitsiant...)
    public string Position { get; set; } = string.Empty; // Lavozim
    public int PinCode { get; set; }                     // Tezkor kirish uchun Pin kod
    public bool IsActive { get; set; } = true;
    public decimal Salary { get; set; }// Ishda yoki ishdan bo'shatilganligi
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
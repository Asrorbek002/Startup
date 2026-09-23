public class Shop
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "ShopOwner";
    public decimal Balance { get; set; } = 0;
    public decimal Tariff { get; set; } = 200000; // <-- Mana buni qo'shing
    public string Status { get; set; } = "Faollashtirilgan";
}
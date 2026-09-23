using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShopManagementSystem.Entities;

public class Payment
{
    [Key]
    public int Id { get; set; }

    public int ShopId { get; set; }

    public int EmployeeId { get; set; }

    [ForeignKey("EmployeeId")]
    public Employee? Employee { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public string? Note { get; set; }

    public DateTime PaidAt { get; set; } = DateTime.UtcNow;
}

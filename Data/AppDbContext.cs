using Microsoft.EntityFrameworkCore;
using ShopManagementSystem.Entities;
using System.Xml.Linq;

namespace ShopManagementSystem.Data;

public class AppDbContext : DbContext // <--- ": DbContext" meros olishi shart!
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Shop> Shops => Set<Shop>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<Element> Elements => Set<Element>(); // <--- Elementlar jadvali
    public DbSet<Product> Products => Set<Product>(); // <--- Tovar qabul qilish jadvali qo'shildi
    public DbSet<Payment> Payments => Set<Payment>(); // <--- Xodimlarga to'lovlar jadvali qo'shildi
    public DbSet<SaleEditLog> SaleEditLogs => Set<SaleEditLog>(); // <--- Savdo tahrirlash tarixi jadvali qo'shildi
}
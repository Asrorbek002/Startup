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
    public DbSet<Debtor> Debtors => Set<Debtor>(); // <--- Qarzdorlar jadvali
    public DbSet<DebtorPayment> DebtorPayments => Set<DebtorPayment>(); // <--- Qarzdorlarga qilingan har bir to'lov tarixi
    public DbSet<SaleEditLog> SaleEditLogs => Set<SaleEditLog>(); // <--- Savdo tahrirlash tarixi jadvali qo'shildi
    public DbSet<TelegramSettings> TelegramSettings => Set<TelegramSettings>(); // <--- Telegram bot sozlamalari
    public DbSet<MaterialUsage> MaterialUsages => Set<MaterialUsage>(); // <--- Sex xodimlari material sarfi
    public DbSet<Order> Orders => Set<Order>(); // <--- Buyurtmalar (avans bilan)
    public DbSet<OrderPayment> OrderPayments => Set<OrderPayment>(); // <--- Buyurtma to'lovlari tarixi
    public DbSet<Supplier> Suppliers => Set<Supplier>(); // <--- Ta'minotchilar
    public DbSet<SupplierPurchase> SupplierPurchases => Set<SupplierPurchase>(); // <--- Ta'minotchidan olingan tovarlar
    public DbSet<SupplierPayment> SupplierPayments => Set<SupplierPayment>(); // <--- Ta'minotchilarga to'lovlar
    public DbSet<BotDailyLog> BotDailyLogs => Set<BotDailyLog>(); // <--- Botga kunlik xabarlar yuborilganini eslab qoladi
}
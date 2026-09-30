using Microsoft.EntityFrameworkCore;
using ShopManagementSystem.Data;
using ShopManagementSystem.Entities;

namespace ShopManagementSystem.Services;

// Balans / oylik tarif / kredit limit qoidalari
public static class BillingLogic
{
    public const string StatusActive = "Faollashtirilgan";
    public const string StatusBlocked = "Bloklangan";   // balans limitdan oshib ketganda (avtomatik)
    public const string StatusInactive = "Faol emas";   // admin qo'lda o'chirgan

    // Balans 0 yoki undan yuqori bo'lsa do'kon ishlaydi.
    // Minusga tushib, minus miqdori limitga yetsa (masalan limit 20000 bo'lsa -20000 da) bloklanadi.
    // Limit 0 bo'lsa - birorta ham minusga ruxsat yo'q (balans 0 dan pastga tushsa bloklanadi).
    // Balans qayta ko'tarilib, shartdan chiqsa - qayta ochiladi.
    // Admin qo'lda o'chirgan ("Faol emas") do'konga tegmaydi.
    public static void SyncStatus(Shop shop)
    {
        if (shop.Status == StatusInactive) return;

        bool overLimit = shop.CreditLimit > 0
            ? shop.Balance <= -shop.CreditLimit
            : shop.Balance < 0;

        if (overLimit && shop.Status != StatusBlocked)
            shop.Status = StatusBlocked;
        else if (!overLimit && shop.Status == StatusBlocked)
            shop.Status = StatusActive;
    }

    // Muddati kelgan oylik tarifni yechadi (o'tkazib yuborilgan oylar bo'lsa, hammasini birma-bir).
    // true qaytarsa - do'kon o'zgargan (saqlash kerak).
    public static bool ApplyDueCharges(AppDbContext context, Shop shop, DateTime nowUtc)
    {
        bool changed = false;

        if (shop.NextBillingDate == null)
        {
            // Yangi tizimga o'tgan eski do'kon: birinchi yechish 1 oydan keyin
            shop.NextBillingDate = nowUtc.AddMonths(1);
            changed = true;
        }

        int guard = 0;
        while (shop.NextBillingDate!.Value <= nowUtc && guard++ < 12)
        {
            // Admin qo'lda o'chirib qo'ygan do'kondan pul yechilmaydi, faqat sana suriladi
            if (shop.Status != StatusInactive && shop.Tariff > 0)
            {
                shop.Balance -= shop.Tariff;
                context.BalanceTransactions.Add(new BalanceTransaction
                {
                    ShopId = shop.Id,
                    Type = "charge",
                    Amount = -shop.Tariff,
                    BalanceAfter = shop.Balance,
                    Note = "Oylik tarif (" + shop.NextBillingDate.Value.ToString("dd.MM.yyyy") + ")",
                    CreatedAt = nowUtc
                });
            }

            shop.NextBillingDate = shop.NextBillingDate.Value.AddMonths(1);
            changed = true;
        }

        // Juda uzoq vaqt o'tib ketgan bo'lsa (12 oydan ko'p), sanani hozirdan boshlab qayta qo'yamiz
        if (shop.NextBillingDate!.Value <= nowUtc)
        {
            shop.NextBillingDate = nowUtc.AddMonths(1);
            changed = true;
        }

        return changed;
    }

    // Barcha do'konlarni tekshiradi: kerak bo'lsa tarifni yechadi va limitga qarab holatni yangilaydi.
    // Hammasi bitta SaveChanges (bitta tranzaksiya) ichida saqlanadi.
    public static async Task<int> ProcessAllAsync(AppDbContext context, ILogger logger)
    {
        var now = DateTime.UtcNow;
        var shops = await context.Shops.ToListAsync();
        int affected = 0;

        foreach (var shop in shops)
        {
            var before = shop.Status;
            bool changed = ApplyDueCharges(context, shop, now);
            SyncStatus(shop);

            if (changed || before != shop.Status)
            {
                affected++;
                if (before != shop.Status)
                    logger.LogInformation("Do'kon #{Id} holati o'zgardi: {Old} -> {New} (balans: {Balance})", shop.Id, before, shop.Status, shop.Balance);
            }
        }

        if (affected > 0)
            await context.SaveChangesAsync();

        return affected;
    }
}

// Server ishlab turganda har 30 daqiqada oylik tarifni tekshiradi.
// Render uxlab qolib, keyin uyg'onsa ham - ishga tushgan zahoti tekshiradi va o'tkazib yuborilganini yechadi.
public class BillingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BillingBackgroundService> _logger;

    public BillingBackgroundService(IServiceScopeFactory scopeFactory, ILogger<BillingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Baza jadvallari (SchemaBootstrap) tayyor bo'lishi uchun ozgina kutamiz
        try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); } catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var n = await BillingLogic.ProcessAllAsync(context, _logger);
                if (n > 0) _logger.LogInformation("Billing: {Count} ta do'kon yangilandi.", n);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Billing tekshiruvida xatolik.");
            }

            try { await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken); } catch (OperationCanceledException) { return; }
        }
    }
}
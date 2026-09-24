using Microsoft.EntityFrameworkCore;
using ShopManagementSystem.Data;

var builder = WebApplication.CreateBuilder(args);

// Baza ulanishi: agar Render'da DATABASE_URL bo'lsa -> PostgreSQL ishlatiladi
// Agar DATABASE_URL bo'lmasa (masalan o'zingizning kompyuteringizda) -> SQLite ishlatiladi
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (!string.IsNullOrEmpty(databaseUrl))
    {
        // Render odatda "postgres://user:pass@host:port/dbname" formatida beradi.
        // Npgsql tushunadigan formatga o'giramiz.
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':');
        var npgsqlConnString =
            $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};" +
            $"Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true";

        options.UseNpgsql(npgsqlConnString);
    }
    else
    {
        // Lokal kompyuterda ishlashda hozirgidek SQLite qoladi
        var dbPath = Path.Combine(builder.Environment.ContentRootPath, "shopmanagement.db");
        options.UseSqlite($"Data Source={dbPath}");
    }
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Server ishga tushganda mavjud migratsiyalarni bazaga qo'llaydi.
// EnsureDeleted() YO'Q — shuning uchun mavjud ma'lumotlar hech qachon o'chirilmaydi.
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.Migrate();
}

// --- 1. SHU YERGA USEDEFAULTFILES() QO'SHING ---
app.UseDefaultFiles(); // index.html ni avtomatik ochish uchun
app.UseStaticFiles();   // wwwroot papkasidagi statik fayllarni ishlatish uchun

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
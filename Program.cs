using Microsoft.EntityFrameworkCore;
using ShopManagementSystem.Data;

var builder = WebApplication.CreateBuilder(args);

// SQLite uchun ulash — baza fayli doim loyiha papkasidagi bitta aniq manzilda turishi uchun
// (IIS Express, "dotnet run" yoki boshqa rejim - qayerdan ishga tushirilishidan qat'iy nazar bir xil joy)
var dbPath = Path.Combine(builder.Environment.ContentRootPath, "shopmanagement.db");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

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
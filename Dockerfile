# ---------- 1-bosqich: build (kod kompilyatsiya qilinadi) ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Loyihaning .csproj faylini nusxalab, avval restore qilamiz (cache tezroq ishlashi uchun)
COPY *.csproj .
RUN dotnet restore

# Qolgan barcha fayllarni nusxalab, publish (Release rejimida) qilamiz
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

# ---------- 2-bosqich: runtime (faqat ishga tushirish uchun kerakli qism) ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Render konteynerga PORT environment variable orqali qaysi portda tinglashni aytadi.
# Shu portni ASP.NET Core'ga ishga tushish paytida beramiz.
ENV DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080

# DIQQAT: "ShopManagementSystem.dll" o'rniga o'zingizning .csproj faylingiz nomini yozing
# (masalan agar .csproj = MyApp.csproj bo'lsa, bu yerda MyApp.dll bo'lishi kerak)
CMD ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet ShopManagementSystem.dll

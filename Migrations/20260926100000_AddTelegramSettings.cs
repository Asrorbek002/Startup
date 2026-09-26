using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ShopManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddTelegramSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TelegramSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ShopId = table.Column<int>(type: "integer", nullable: false),
                    BotToken = table.Column<string>(type: "text", nullable: false),
                    ChatId = table.Column<string>(type: "text", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LowStockThreshold = table.Column<decimal>(type: "numeric", nullable: false),
                    DailyReportHour = table.Column<int>(type: "integer", nullable: false),
                    DailyReportEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastDailyReportSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastLowStockAlertAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TelegramSettings_ShopId",
                table: "TelegramSettings",
                column: "ShopId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TelegramSettings");
        }
    }
}

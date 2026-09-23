using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class SaleEditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProductId",
                table: "Sales",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "SaleEditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ShopId = table.Column<int>(type: "INTEGER", nullable: false),
                    SaleId = table.Column<int>(type: "INTEGER", nullable: false),
                    EmployeeId = table.Column<int>(type: "INTEGER", nullable: false),
                    EmployeeName = table.Column<string>(type: "TEXT", nullable: false),
                    OldProductName = table.Column<string>(type: "TEXT", nullable: false),
                    OldQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    OldSalePrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    OldTotalSum = table.Column<decimal>(type: "TEXT", nullable: false),
                    NewProductName = table.Column<string>(type: "TEXT", nullable: false),
                    NewQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    NewSalePrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    NewTotalSum = table.Column<decimal>(type: "TEXT", nullable: false),
                    EditedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleEditLogs", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SaleEditLogs");

            migrationBuilder.DropColumn(
                name: "ProductId",
                table: "Sales");
        }
    }
}

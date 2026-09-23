using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddShopFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Shops");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Shops");

            migrationBuilder.DropColumn(
                name: "Login",
                table: "Shops");

            migrationBuilder.RenameColumn(
                name: "Version",
                table: "Shops",
                newName: "Username");

            migrationBuilder.RenameColumn(
                name: "Tariff",
                table: "Shops",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "ShopName",
                table: "Shops",
                newName: "Role");

            migrationBuilder.RenameColumn(
                name: "Region",
                table: "Shops",
                newName: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Username",
                table: "Shops",
                newName: "Version");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "Shops",
                newName: "Tariff");

            migrationBuilder.RenameColumn(
                name: "Role",
                table: "Shops",
                newName: "ShopName");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Shops",
                newName: "Region");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Shops",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Shops",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Login",
                table: "Shops",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}

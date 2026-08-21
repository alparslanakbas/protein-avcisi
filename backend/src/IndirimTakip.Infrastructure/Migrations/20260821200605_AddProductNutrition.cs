using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IndirimTakip.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductNutrition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NutritionCheckedAt",
                table: "Products",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NutritionJson",
                table: "Products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProteinPerServingGrams",
                table: "Products",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NutritionCheckedAt",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "NutritionJson",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ProteinPerServingGrams",
                table: "Products");
        }
    }
}

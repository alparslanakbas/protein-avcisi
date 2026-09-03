using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IndirimTakip.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FiyatOzetiAlanlari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LatestPrice",
                table: "Products",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LatestScrapedAt",
                table: "Products",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LatestStoreOldPrice",
                table: "Products",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LowestPrice30",
                table: "Products",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PriceSummaryUpdatedAt",
                table: "Products",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReferencePrice30",
                table: "Products",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LatestPrice",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "LatestScrapedAt",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "LatestStoreOldPrice",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "LowestPrice30",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PriceSummaryUpdatedAt",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ReferencePrice30",
                table: "Products");
        }
    }
}

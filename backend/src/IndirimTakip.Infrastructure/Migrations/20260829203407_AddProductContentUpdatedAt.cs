using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IndirimTakip.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductContentUpdatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ContentUpdatedAt",
                table: "Products",
                type: "timestamp with time zone",
                nullable: true);

            // Geçmişi doğru değerlerle dolduruyoruz: her ürün için fiyatın bir
            // ÖNCEKİ ölçümden farklı olduğu en son an. Alan boş bırakılsaydı
            // sitemap tüm katalog için yine son tarama zamanına düşer ve
            // düzeltmenin bir anlamı kalmazdı.
            migrationBuilder.Sql(@"
                UPDATE ""Products"" p
                SET ""ContentUpdatedAt"" = sub.ts
                FROM (
                    SELECT ""ProductId"", MAX(""ScrapedAt"") AS ts
                    FROM (
                        SELECT ""ProductId"", ""ScrapedAt"", ""Price"",
                               LAG(""Price"") OVER (PARTITION BY ""ProductId"" ORDER BY ""ScrapedAt"") AS prev
                        FROM ""PriceHistories""
                    ) x
                    WHERE prev IS NULL OR ""Price"" <> prev
                    GROUP BY ""ProductId""
                ) sub
                WHERE sub.""ProductId"" = p.""Id"";
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentUpdatedAt",
                table: "Products");
        }
    }
}

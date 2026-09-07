using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IndirimTakip.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UrunGorunurlugu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Products",
                type: "boolean",
                nullable: false,
                // VARSAYILAN true OLMAK ZORUNDA. EF bu kolonu defaultValue:
                // false ile ureti ve MEVCUT 4.920 SATIRIN HEPSI GIZLENDI —
                // 7 Eylul'de canlida yasandi, site bir kac dakika bombos
                // kaldi. C# tarafindaki "= true" baslatici YALNIZCA yeni
                // nesnelere uygulaniyor, migration'in var olan satirlara
                // yazdigi degere degil.
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Products");
        }
    }
}

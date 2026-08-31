using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IndirimTakip.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSellerCoupons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "BrandId",
                table: "Coupons",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "Seller",
                table: "Coupons",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Coupons_ExactlyOneTarget",
                table: "Coupons",
                sql: "(\"BrandId\" IS NULL) <> (\"Seller\" IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Eski şema satıcı hedefini temsil edemez. Geri dönüşte yalnız
            // satıcı kuponlarını kaldır; mevcut marka kuponları korunur.
            migrationBuilder.Sql("DELETE FROM \"Coupons\" WHERE \"BrandId\" IS NULL;");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Coupons_ExactlyOneTarget",
                table: "Coupons");

            migrationBuilder.DropColumn(
                name: "Seller",
                table: "Coupons");

            migrationBuilder.AlterColumn<int>(
                name: "BrandId",
                table: "Coupons",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IndirimTakip.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductInStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "InStock",
                table: "Products",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InStock",
                table: "Products");
        }
    }
}

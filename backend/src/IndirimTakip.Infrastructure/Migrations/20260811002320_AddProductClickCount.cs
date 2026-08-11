using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IndirimTakip.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductClickCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClickCount",
                table: "Products",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClickCount",
                table: "Products");
        }
    }
}

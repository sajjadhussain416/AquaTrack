using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AquaTrack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Day6WalletNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EasypaisaTitle",
                table: "Plants",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JazzCashTitle",
                table: "Plants",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EasypaisaTitle",
                table: "Plants");

            migrationBuilder.DropColumn(
                name: "JazzCashTitle",
                table: "Plants");
        }
    }
}

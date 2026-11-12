using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AquaTrack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Day6Payments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_PlantId",
                table: "Payments");

            migrationBuilder.AddColumn<string>(
                name: "BankAccountNumber",
                table: "Plants",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankAccountTitle",
                table: "Plants",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankName",
                table: "Plants",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EasypaisaNumber",
                table: "Plants",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JazzCashNumber",
                table: "Plants",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransactionRef",
                table: "Payments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Verification",
                table: "Payments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Verified");

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifiedAt",
                table: "Payments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerifiedById",
                table: "Payments",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "Deliveries",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Cash");

            migrationBuilder.AddColumn<string>(
                name: "TransactionRef",
                table: "Deliveries",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PlantId_Verification",
                table: "Payments",
                columns: new[] { "PlantId", "Verification" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_PlantId_Verification",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "BankAccountNumber",
                table: "Plants");

            migrationBuilder.DropColumn(
                name: "BankAccountTitle",
                table: "Plants");

            migrationBuilder.DropColumn(
                name: "BankName",
                table: "Plants");

            migrationBuilder.DropColumn(
                name: "EasypaisaNumber",
                table: "Plants");

            migrationBuilder.DropColumn(
                name: "JazzCashNumber",
                table: "Plants");

            migrationBuilder.DropColumn(
                name: "TransactionRef",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Verification",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "VerifiedAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "VerifiedById",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Deliveries");

            migrationBuilder.DropColumn(
                name: "TransactionRef",
                table: "Deliveries");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PlantId",
                table: "Payments",
                column: "PlantId");
        }
    }
}

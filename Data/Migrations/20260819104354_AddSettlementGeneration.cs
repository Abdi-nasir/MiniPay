using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniApy.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSettlementGeneration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SettlementId",
                table: "Transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FeePercentage",
                table: "Settlements",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_SettlementId",
                table: "Transactions",
                column: "SettlementId");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Settlements_SettlementId",
                table: "Transactions",
                column: "SettlementId",
                principalTable: "Settlements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Settlements_SettlementId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_SettlementId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "SettlementId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "FeePercentage",
                table: "Settlements");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WalletApp.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CryptoHoldings_Users_UserId",
                table: "CryptoHoldings");

            migrationBuilder.DropForeignKey(
                name: "FK_RecurringTransactions_Users_UserId",
                table: "RecurringTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Users_UserId",
                table: "Transactions");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CryptoHoldings_Users_UserId",
                table: "CryptoHoldings",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RecurringTransactions_Users_UserId",
                table: "RecurringTransactions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Users_UserId",
                table: "Transactions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CryptoHoldings_Users_UserId",
                table: "CryptoHoldings");

            migrationBuilder.DropForeignKey(
                name: "FK_RecurringTransactions_Users_UserId",
                table: "RecurringTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Users_UserId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Users");

            migrationBuilder.AddForeignKey(
                name: "FK_CryptoHoldings_Users_UserId",
                table: "CryptoHoldings",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RecurringTransactions_Users_UserId",
                table: "RecurringTransactions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Users_UserId",
                table: "Transactions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

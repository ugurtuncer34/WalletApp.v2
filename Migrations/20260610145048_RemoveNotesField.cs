using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WalletApp.Migrations
{
    /// <inheritdoc />
    public partial class RemoveNotesField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Transactions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Transactions",
                type: "TEXT",
                nullable: true);
        }
    }
}

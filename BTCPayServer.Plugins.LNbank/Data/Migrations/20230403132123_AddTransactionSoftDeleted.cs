using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.LNbank.Data.Migrations
{
    public partial class AddTransactionSoftDeleted : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSoftDeleted",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "Transactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSoftDeleted",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "Transactions");
        }
    }
}

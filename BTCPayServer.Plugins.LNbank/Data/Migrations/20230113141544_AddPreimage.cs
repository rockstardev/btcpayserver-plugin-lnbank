using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.LNbank.Data.Migrations
{
    public partial class AddPreimage : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Preimage",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "Transactions",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Preimage",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "Transactions");
        }
    }
}

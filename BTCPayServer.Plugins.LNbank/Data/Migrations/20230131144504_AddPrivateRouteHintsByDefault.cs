using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.LNbank.Data.Migrations
{
    public partial class AddPrivateRouteHintsByDefault : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PrivateRouteHintsByDefault",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "Wallets",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrivateRouteHintsByDefault",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "Wallets");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.LNbank.Data.Migrations
{
    public partial class AddLightningAddressIdentifier : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LightningAddressIdentifier",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "Wallets",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LightningAddressIdentifier",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "Wallets");
        }
    }
}

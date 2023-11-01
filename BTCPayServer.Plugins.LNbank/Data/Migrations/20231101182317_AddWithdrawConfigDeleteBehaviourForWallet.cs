using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.LNbank.Data.Migrations
{
    public partial class AddWithdrawConfigDeleteBehaviourForWallet : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WithdrawConfigs_Wallets_WalletId",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "WithdrawConfigs");

            migrationBuilder.AlterColumn<long>(
                name: "Counter",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "BoltCards",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddForeignKey(
                name: "FK_WithdrawConfigs_Wallets_WalletId",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "WithdrawConfigs",
                column: "WalletId",
                principalSchema: "BTCPayServer.Plugins.LNbank",
                principalTable: "Wallets",
                principalColumn: "WalletId",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WithdrawConfigs_Wallets_WalletId",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "WithdrawConfigs");

            migrationBuilder.AlterColumn<int>(
                name: "Counter",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "BoltCards",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddForeignKey(
                name: "FK_WithdrawConfigs_Wallets_WalletId",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "WithdrawConfigs",
                column: "WalletId",
                principalSchema: "BTCPayServer.Plugins.LNbank",
                principalTable: "Wallets",
                principalColumn: "WalletId");
        }
    }
}

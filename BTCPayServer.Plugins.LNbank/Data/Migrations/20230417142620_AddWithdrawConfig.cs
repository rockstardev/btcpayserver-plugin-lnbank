using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.LNbank.Data.Migrations
{
    public partial class AddWithdrawConfig : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WithdrawConfigId",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "Transactions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WithdrawConfigs",
                schema: "BTCPayServer.Plugins.LNbank",
                columns: table => new
                {
                    WithdrawConfigId = table.Column<string>(type: "text", nullable: false),
                    WalletId = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ReuseType = table.Column<string>(type: "text", nullable: false),
                    Limit = table.Column<long>(type: "bigint", nullable: true),
                    MaxPerUse = table.Column<long>(type: "bigint", nullable: true),
                    MaxTotal = table.Column<long>(type: "bigint", nullable: true),
                    IsSoftDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WithdrawConfigs", x => x.WithdrawConfigId);
                    table.ForeignKey(
                        name: "FK_WithdrawConfigs_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalSchema: "BTCPayServer.Plugins.LNbank",
                        principalTable: "Wallets",
                        principalColumn: "WalletId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_WithdrawConfigId",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "Transactions",
                column: "WithdrawConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawConfigs_WalletId",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "WithdrawConfigs",
                column: "WalletId");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_WithdrawConfigs_WithdrawConfigId",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "Transactions",
                column: "WithdrawConfigId",
                principalSchema: "BTCPayServer.Plugins.LNbank",
                principalTable: "WithdrawConfigs",
                principalColumn: "WithdrawConfigId",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_WithdrawConfigs_WithdrawConfigId",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "WithdrawConfigs",
                schema: "BTCPayServer.Plugins.LNbank");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_WithdrawConfigId",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "WithdrawConfigId",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "Transactions");
        }
    }
}

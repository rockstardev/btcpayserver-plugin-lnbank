using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.LNbank.Data.Migrations
{
    public partial class AddBoltCards : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BoltCards",
                schema: "BTCPayServer.Plugins.LNbank",
                columns: table => new
                {
                    BoltCardId = table.Column<string>(type: "text", nullable: false),
                    CardIdentifier = table.Column<string>(type: "text", nullable: true),
                    Index = table.Column<int>(type: "integer", nullable: true),
                    Counter = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    WithdrawConfigId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoltCards", x => x.BoltCardId);
                    table.ForeignKey(
                        name: "FK_BoltCards_WithdrawConfigs_WithdrawConfigId",
                        column: x => x.WithdrawConfigId,
                        principalSchema: "BTCPayServer.Plugins.LNbank",
                        principalTable: "WithdrawConfigs",
                        principalColumn: "WithdrawConfigId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BoltCards_WithdrawConfigId",
                schema: "BTCPayServer.Plugins.LNbank",
                table: "BoltCards",
                column: "WithdrawConfigId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BoltCards",
                schema: "BTCPayServer.Plugins.LNbank");
        }
    }
}

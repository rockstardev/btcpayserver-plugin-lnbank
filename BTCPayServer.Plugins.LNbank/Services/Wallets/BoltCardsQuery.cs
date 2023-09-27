using BTCPayServer.Plugins.LNbank.Data.Models;

namespace BTCPayServer.Plugins.LNbank.Services.Wallets;

public class BoltCardsQuery
{
    public string BoltCardId { get; set; }
    public string WithdrawConfigId { get; set; }
    public bool IncludeWithdrawConfig { get; set; }
    public bool IncludeTransactions { get; set; }
    public BoltCardStatus? Status { get; set; }
}

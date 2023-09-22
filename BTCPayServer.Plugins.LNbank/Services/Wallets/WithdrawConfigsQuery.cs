namespace BTCPayServer.Plugins.LNbank.Services.Wallets;

public class WithdrawConfigsQuery
{
    public string WalletId { get; set; }
    public string WithdrawConfigId { get; set; }
    public bool IncludeWallet { get; set; }
    public bool IncludeTransactions { get; set; }
    public bool IncludeBoltCard { get; set; }
}

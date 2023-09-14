using System.Linq;
using BTCPayServer.Plugins.LNbank.Data.Models;

namespace BTCPayServer.Plugins.LNbank.Services.Wallets;

public class WalletsQuery
{
    public string[] UserId { get; set; }
    public string[] WalletId { get; set; }
    public string[] AccessKey { get; set; }
    public string[] LightningAddressIdentifier { get; set; }
    public bool IncludeTransactions { get; set; }
    public bool IncludeAccessKeys { get; set; }
    public bool IncludeUser { get; set; }
    public bool IsServerAdmin { get; set; }

    public bool HasAdminAccess(Wallet wallet)
    {
        return IsServerAdmin || (UserId != null && UserId.Contains(wallet.UserId));
    }
}

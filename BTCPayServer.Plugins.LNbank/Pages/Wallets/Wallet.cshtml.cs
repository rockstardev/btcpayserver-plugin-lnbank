using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Data;
using BTCPayServer.Plugins.LNbank.Authentication;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using AuthenticationSchemes = BTCPayServer.Abstractions.Constants.AuthenticationSchemes;

namespace BTCPayServer.Plugins.LNbank.Pages.Wallets;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = LNbankPolicies.CanViewWallet)]
public class WalletModel : BasePageModel
{
    public IEnumerable<Transaction> Transactions { get; set; }

    public WalletModel(
        UserManager<ApplicationUser> userManager,
        WalletRepository walletRepository,
        WalletService walletService) : base(userManager, walletRepository, walletService) { }

    public IActionResult OnGetAsync(string walletId)
    {
        if (CurrentWallet == null)
            return NotFound();

        Transactions = CurrentWallet.Transactions.OrderByDescending(t => t.CreatedAt);

        return Page();
    }
}

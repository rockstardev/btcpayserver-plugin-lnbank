using System.Collections.Generic;
using System.Linq;
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
    private readonly HistogramService _histogramService;
    public IEnumerable<Transaction> Transactions { get; set; }
    public HistogramData HistogramData { get; set; }

    public WalletModel(
        UserManager<ApplicationUser> userManager,
        HistogramService histogramService,
        WalletRepository walletRepository,
        WalletService walletService) : base(userManager, walletRepository, walletService)
    {
        _histogramService = histogramService;
    }

    public IActionResult OnGetAsync(string walletId)
    {
        if (CurrentWallet == null)
            return NotFound();

        Transactions = CurrentWallet.Transactions.OrderByDescending(t => t.CreatedAt);
        HistogramData = _histogramService.GetHistogram(CurrentWallet);

        return Page();
    }
}

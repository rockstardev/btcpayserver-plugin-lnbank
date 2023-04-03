using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Data;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.Authentication;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Services;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.LNbank.Pages.Admin;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = LNbankPolicies.CanManageLNbank)]
public class IndexModel : BasePageModel
{
    private readonly BTCPayService _btcpayService;
    public Dictionary<string, IEnumerable<Wallet>> Wallets { get; set; }
    public LightMoney TotalBalance { get; set; }
    public bool IsReady { get; set; }

    public IndexModel(
        UserManager<ApplicationUser> userManager,
        WalletRepository walletRepository,
        WalletService walletService,
        BTCPayService btcpayService) : base(userManager, walletRepository, walletService)
    {
        _btcpayService = btcpayService;
    }

    public async Task<IActionResult> OnGetAsync(string walletId)
    {
        IsReady = _btcpayService.HasInternalNode;
        if (!IsReady)
        {
            TempData[WellKnownTempData.ErrorMessage] = "LNbank requires an internal Lightning node to be configured.";
        }

        var wallets = await WalletRepository.GetWallets(new WalletsQuery
        {
            IncludeTransactions = true,
            IncludeUser = true
        });

        Wallets = wallets
            .GroupBy(w => w.UserId)
            .ToDictionary(g => g.Key,
                g => g.AsEnumerable());

        TotalBalance = wallets
            .Select(w => w.Balance)
            .Aggregate((res, current) => res + current);

        return Page();
    }
}

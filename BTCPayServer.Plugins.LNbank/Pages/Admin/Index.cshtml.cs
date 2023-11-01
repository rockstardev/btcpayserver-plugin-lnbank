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
    public Dictionary<string, WalletsViewModel> WalletsByUserId { get; set; }
    public LightMoney TotalBalance { get; set; }
    public LightMoney TotalLiabilities { get; set; }
    public LightMoney TotalNodeBalance { get; set; }
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

        var walletsByUserId = (await WalletRepository.GetWallets(new WalletsQuery
            {
                IncludeTransactions = true,
                IncludeUser = true,
                IncludeSoftDeleted = true,
                IsServerAdmin = User.IsInRole(Roles.ServerAdmin)
            }))
            .GroupBy(w => w.UserId)
            .ToDictionary(g => g.Key, g => g.AsEnumerable());

        TotalBalance = LightMoney.Zero;
        WalletsByUserId = new Dictionary<string, WalletsViewModel>();
        foreach (var userId in walletsByUserId.Keys)
        {
            var user = await UserManager.FindByIdAsync(userId);
            if (user == null) continue;

            var userWallets = walletsByUserId[userId].ToList();
            var userTotal = await userWallets.Aggregate(Task.FromResult(LightMoney.Zero), async (total, wallet) => await total + await WalletService.GetBalance(wallet));
            WalletsByUserId[userId] = new WalletsViewModel
            {
                User = user,
                Wallets = userWallets,
                TotalBalance = userTotal
            };
            TotalBalance += userTotal;
        }

        // check LNbank reserves
        TotalLiabilities = await WalletService.GetLiabilitiesTotal();
        TotalNodeBalance = (await _btcpayService.GetLightningNodeBalance()).OffchainBalance.Local;

        return Page();
    }
}

public class WalletsViewModel
{
    public ApplicationUser User { get; init; }
    public IEnumerable<Wallet> Wallets { get; init; }
    public LightMoney TotalBalance { get; set; }
}

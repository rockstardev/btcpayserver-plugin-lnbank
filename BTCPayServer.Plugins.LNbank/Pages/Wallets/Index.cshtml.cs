using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Services;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.LNbank.Pages.Wallets;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class IndexModel : BasePageModel
{
    private readonly BTCPayService _btcpayService;
    public IEnumerable<Wallet> Wallets { get; set; }
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

        Wallets = (await WalletRepository.GetWallets(new WalletsQuery
        {
            UserId = new[] { UserId },
            IncludeTransactions = true
        })).ToList();

        if (!Wallets.Any())
        {
            return RedirectToPage("./Create");
        }

        TotalBalance = await Wallets
            .Select(async w => await WalletService.GetBalance(w))
            .Aggregate(async (res, current) => await res + await current);

        // check LNbank reserves
        TotalLiabilities = await WalletService.GetLiabilitiesTotal();
        TotalNodeBalance = (await _btcpayService.GetLightningNodeBalance()).OffchainBalance.Local;

        return Page();
    }
}

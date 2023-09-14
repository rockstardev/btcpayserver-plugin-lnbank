using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Data;
using BTCPayServer.Plugins.LNbank.Authentication;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using AuthenticationSchemes = BTCPayServer.Abstractions.Constants.AuthenticationSchemes;

namespace BTCPayServer.Plugins.LNbank.Pages.Wallets;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = LNbankPolicies.CanManageWallet)]
public class DetailsModel : BasePageModel
{
    public DetailsModel(
        UserManager<ApplicationUser> userManager,
        WalletRepository walletRepository,
        WalletService walletService) : base(userManager, walletRepository, walletService) { }

    public IActionResult OnGetAsync(string walletId)
    {
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string walletId)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (await TryUpdateModelAsync(CurrentWallet, "CurrentWallet",
                w => w.Name,
                w => w.LightningAddressIdentifier,
                w => w.PrivateRouteHintsByDefault))
        {
            await WalletRepository.AddOrUpdateWallet(CurrentWallet);
            TempData[WellKnownTempData.SuccessMessage] = "Wallet successfully updated.";
            return RedirectToPage("./Details", new { walletId });
        }

        return Page();
    }
}

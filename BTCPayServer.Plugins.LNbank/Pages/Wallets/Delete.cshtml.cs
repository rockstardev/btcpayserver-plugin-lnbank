using System;
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
public class DeleteModel : BasePageModel
{
    public DeleteModel(
        UserManager<ApplicationUser> userManager,
        WalletRepository walletRepository,
        WalletService walletService) : base(userManager, walletRepository, walletService) { }

    public IActionResult OnGetAsync(string walletId)
    {
        if (CurrentWallet == null)
            return NotFound();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string walletId)
    {
        if (CurrentWallet == null)
            return NotFound();

        if (WalletService.HasBalance(CurrentWallet) && !User.IsInRole(Roles.ServerAdmin))
        {
            TempData[WellKnownTempData.ErrorMessage] = "This wallet still has a balance.";
            return Page();
        }

        await WalletRepository.RemoveWallet(CurrentWallet);
        TempData[WellKnownTempData.SuccessMessage] = "Wallet removed.";
        return RedirectToPage("./Index");
    }
}

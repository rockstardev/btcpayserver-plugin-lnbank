using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Data;
using BTCPayServer.Plugins.LNbank.Authentication;
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

        var hasBalance = await WalletService.HasBalance(CurrentWallet);
        var hasBoltCard = await WalletService.HasActiveBoltCard(CurrentWallet);
        if ((hasBalance || hasBoltCard) && !IsServerAdmin)
        {
            TempData[WellKnownTempData.ErrorMessage] = hasBalance
                ? "This wallet still has a balance."
                : "This wallet still has a withdraw config and Bolt Card associated with it. Make sure to backup the wipe keys for any associated Bolt Card!";
            return Page();
        }

        await WalletRepository.RemoveWallet(CurrentWallet);
        TempData[WellKnownTempData.SuccessMessage] = "Wallet removed.";
        return RedirectToPage("./Index");
    }
}

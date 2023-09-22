using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.Authentication;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using BTCPayServer.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.LNbank.Pages.Wallets;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewProfile)]
public class CreateModel : BasePageModel
{
    private readonly IAuthorizationService _authorizationService;
    public Wallet Wallet { get; set; }
    [FromQuery]
    public string ReturnUrl { get; set; }

    [BindProperty]
    [DisplayName("Initial Balance")]
    [Range(0, 2100000000000)]
    public long InitialBalance { get; set; } = 0;

    public CreateModel(
        UserManager<ApplicationUser> userManager,
        WalletRepository walletRepository,
        WalletService walletService,
        IAuthorizationService authorizationService) : base(userManager, walletRepository, walletService)
    {
        _authorizationService = authorizationService;
    }

    public IActionResult OnGet()
    {
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        Wallet = new Wallet
        {
            UserId = UserId
        };

        if (!await TryUpdateModelAsync(Wallet, "wallet", w => w.Name))
            return Page();

        await WalletRepository.AddOrUpdateWallet(Wallet);

        if (InitialBalance > 0 && (await _authorizationService.AuthorizeAsync(User, null,
                new PolicyRequirement(LNbankPolicies.CanManageLNbank))).Succeeded)
        {
            await WalletService.TopUp(Wallet.WalletId, LightMoney.Satoshis(InitialBalance), "Initial balance");
        }

        if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
        {
            return Redirect(ReturnUrl + $"?LNbankWallet={Wallet.WalletId}");
        }

        TempData[WellKnownTempData.SuccessMessage] = "Wallet successfully created.";
        return RedirectToPage("./Wallet", new { walletId = Wallet.WalletId });
    }
}

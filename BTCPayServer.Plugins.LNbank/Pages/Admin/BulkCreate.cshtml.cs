using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Data;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.Authentication;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Services.BoltCard;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.LNbank.Pages.Admin;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = LNbankPolicies.CanManageLNbank)]
public class BulkCreateModel : BasePageModel
{
    private readonly WithdrawConfigRepository _withdrawConfigRepository;
    private readonly BoltCardService _boltCardService;

    [BindProperty]
    [DisplayName("Wallet name")]
    [Required]
    public string WalletName { get; set; }

    [BindProperty]
    [DisplayName("Initial Balance")]
    [Range(0, 2100000000000)]
    public long InitialBalance { get; set; }

    [BindProperty]
    [DisplayName("How many wallets to create")]
    [Range(1, 20000)]
    public int WalletsToCreate { get; set; }

    [BindProperty]
    [DisplayName("Issue Bolt Card for each wallet")]
    public bool InitBoltCard { get; set; }

    public BulkCreateModel(
        UserManager<ApplicationUser> userManager,
        WalletRepository walletRepository,
        WalletService walletService,
        IAuthorizationService authorizationService,
        WithdrawConfigRepository withdrawConfigRepository,
        BoltCardService boltCardService) : base(userManager, walletRepository, walletService)
    {
        _withdrawConfigRepository = withdrawConfigRepository;
        _boltCardService = boltCardService;
    }

    public IActionResult OnGet()
    {
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        for (var i = 1; i <= WalletsToCreate; i++)
        {
            var wallet = new Wallet
            {
                UserId = UserId,
                Name = WalletName.Replace("{Index}", i.ToString()),
            };

            await WalletRepository.AddOrUpdateWallet(wallet);

            if (InitialBalance > 0)
            {
                await WalletService.TopUp(wallet.WalletId, LightMoney.Satoshis(InitialBalance), "Initial balance");
            }

            if (InitBoltCard)
            {
                var wc = await _withdrawConfigRepository.AddWithdrawConfig(new WithdrawConfig
                {
                    WalletId = wallet.WalletId,
                    ReuseType = WithdrawConfigReuseType.Unlimited,
                    Name = wallet.Name,
                });
                await _boltCardService.CreateCard(wc.WithdrawConfigId);
            }
        }

        TempData[WellKnownTempData.SuccessMessage] = "Wallets successfully created.";
        return RedirectToPage("./Index");
    }
}

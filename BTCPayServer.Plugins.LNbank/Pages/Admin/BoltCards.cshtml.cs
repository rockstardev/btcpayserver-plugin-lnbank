using System.Collections.Generic;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Data;
using BTCPayServer.Plugins.LNbank.Authentication;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Services;
using BTCPayServer.Plugins.LNbank.Services.BoltCard;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.LNbank.Pages.Admin;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = LNbankPolicies.CanManageLNbank)]
public class BoltCards : BasePageModel
{
    private readonly WithdrawConfigRepository _withdrawConfigRepository;

    public IEnumerable<BoltCard> PendingCards { get; set; }

    public BoltCards(
        UserManager<ApplicationUser> userManager,
        WithdrawConfigRepository withdrawConfigRepository,
        WalletRepository walletRepository,
        WalletService walletService,
        BoltCardService boltCardService) : base(userManager, walletRepository, walletService)
    {
        _withdrawConfigRepository = withdrawConfigRepository;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        PendingCards = await _withdrawConfigRepository.GetBoltCards(new BoltCardsQuery
        {
            Status = BoltCardStatus.PendingActivation,
            IncludeWithdrawConfig = true,
            IncludeWallet = true,
            IsServerAdmin = IsServerAdmin,
            IncludeSoftDeleted = IsServerAdmin
        });

        return Page();
    }
}


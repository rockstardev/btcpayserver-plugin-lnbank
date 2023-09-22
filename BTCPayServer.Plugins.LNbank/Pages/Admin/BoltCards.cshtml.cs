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
    private readonly LNbankPluginDbContextFactory _lNbankPluginDbContextFactory;

    public BoltCards(
        UserManager<ApplicationUser> userManager,
        WalletRepository walletRepository,
        WalletService walletService,
        LNbankPluginDbContextFactory lNbankPluginDbContextFactory, BoltCardService boltCardService) : base(userManager, walletRepository, walletService)
    {
        _lNbankPluginDbContextFactory = lNbankPluginDbContextFactory;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        await using var ctx = _lNbankPluginDbContextFactory.CreateContext();
        PendingCards = await ctx.BoltCards
            .Where(card => card.Status == BoltCardStatus.PendingActivation)
            .Include(card => card.WithdrawConfig)
            .ToListAsync();

        return Page();
    }

    public List<BoltCard> PendingCards { get; set; }
}


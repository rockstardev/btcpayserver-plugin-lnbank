using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Data;
using BTCPayServer.Plugins.LNbank.Authentication;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using AuthenticationSchemes = BTCPayServer.Abstractions.Constants.AuthenticationSchemes;

namespace BTCPayServer.Plugins.LNbank.Pages.Wallets;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = LNbankPolicies.CanViewWallet)]
public class WalletModel : BasePageModel
{
    private readonly HistogramService _histogramService;
    private readonly WithdrawConfigRepository _withdrawConfigRepository;
    public IEnumerable<Transaction> Transactions { get; set; }
    public WithdrawConfig WithdrawConfig { get; set; }
    public HistogramData HistogramData { get; set; }

    public WalletModel(
        UserManager<ApplicationUser> userManager,
        HistogramService histogramService,
        WalletService walletService,
        WalletRepository walletRepository,
        WithdrawConfigRepository withdrawConfigRepository) : base(userManager, walletRepository, walletService)
    {
        _withdrawConfigRepository = withdrawConfigRepository;
        _histogramService = histogramService;
    }

    public async Task<IActionResult> OnGetAsync(string walletId)
    {
        if (Request.Query.ContainsKey("withdrawConfigId"))
        {
            WithdrawConfig = await _withdrawConfigRepository.GetWithdrawConfig(new WithdrawConfigsQuery
            {
                WithdrawConfigId = Request.Query["withdrawConfigId"].ToString(),
                WalletId = CurrentWallet.WalletId,
                IncludeTransactions = true,
                IncludeBoltCard = true
            });
            if (WithdrawConfig == null)
                return NotFound();
        }

        Transactions = WithdrawConfig == null
            ? CurrentWallet.Transactions.OrderByDescending(t => t.CreatedAt)
            : WithdrawConfig.GetTransactions().OrderByDescending(t => t.CreatedAt);
        HistogramData = _histogramService.GetHistogram(Transactions);

        return Page();
    }
}

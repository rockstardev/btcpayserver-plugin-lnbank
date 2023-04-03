using System;
using System.Linq;
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

namespace BTCPayServer.Plugins.LNbank.Pages.Transactions;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = LNbankPolicies.CanManageLNbank)]
public class DeleteModel : BasePageModel
{
    public Transaction Transaction { get; set; }

    public DeleteModel(
        UserManager<ApplicationUser> userManager,
        WalletRepository walletRepository,
        WalletService walletService) : base(userManager, walletRepository, walletService) { }

    public IActionResult OnGetAsync(string walletId, string transactionId)
    {
        Transaction = CurrentWallet.Transactions.FirstOrDefault(t => t.TransactionId == transactionId);
        if (Transaction == null)
            return NotFound();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string walletId, string transactionId)
    {
        Transaction = CurrentWallet.Transactions.FirstOrDefault(t => t.TransactionId == transactionId);
        if (Transaction == null)
            return NotFound();

        try
        {
            await WalletRepository.RemoveTransaction(Transaction);

            TempData[WellKnownTempData.SuccessMessage] = "Transaction removed.";
            return RedirectToPage("/Wallets/Wallet", new { CurrentWallet.WalletId });
        }
        catch (Exception e)
        {
            TempData[WellKnownTempData.ErrorMessage] = e.Message;
            return Page();
        }
    }
}

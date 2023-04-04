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

namespace BTCPayServer.Plugins.LNbank.Pages.Transactions;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = LNbankPolicies.CanManageWallet)]
public class EditModel : BasePageModel
{
    public Transaction Transaction { get; set; }

    public EditModel(
        UserManager<ApplicationUser> userManager,
        WalletRepository walletRepository,
        WalletService walletService) : base(userManager, walletRepository, walletService) { }

    public IActionResult OnGetAsync(string walletId, string transactionId)
    {
        if (CurrentWallet == null)
            return NotFound();

        Transaction = CurrentWallet.Transactions.FirstOrDefault(t => t.TransactionId == transactionId);
        if (Transaction == null)
            return NotFound();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string walletId, string transactionId)
    {
        if (CurrentWallet == null)
            return NotFound();

        Transaction = await WalletRepository.GetTransaction(new TransactionQuery
        {
            UserId = UserId,
            WalletId = CurrentWallet.WalletId,
            TransactionId = transactionId
        });

        Transaction = CurrentWallet.Transactions.FirstOrDefault(t => t.TransactionId == transactionId);
        if (Transaction == null)
            return NotFound();

        if (!ModelState.IsValid)
            return Page();

        if (await TryUpdateModelAsync(Transaction, "transaction", t => t.Description))
        {
            await WalletRepository.UpdateTransaction(Transaction);
            return RedirectToPage("/Wallets/Wallet", new { CurrentWallet.WalletId });
        }

        return Page();
    }
}

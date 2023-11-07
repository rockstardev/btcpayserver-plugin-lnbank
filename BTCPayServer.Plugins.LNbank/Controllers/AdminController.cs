using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Plugins.LNbank.Authentication;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.LNbank.Controllers;

[Route("~/plugins/lnbank/admin")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = LNbankPolicies.CanManageLNbank)]
public class AdminController : Controller
{
    private readonly WalletRepository _walletRepository;

    public AdminController(WalletRepository walletRepository)
    {
        _walletRepository = walletRepository;
    }

    [HttpPost("wallets/{walletId}/restore")]
    public async Task<IActionResult> RestoreWallet(string walletId)
    {
        var wallet = await _walletRepository.GetWallet(new WalletsQuery
        {
            WalletId = new[] { walletId },
            IncludeSoftDeleted = true,
            IsServerAdmin = User.IsInRole(Roles.ServerAdmin)
        });
        if (wallet == null)
            return NotFound();
        if (!wallet.IsSoftDeleted)
            return RedirectToPage("/Wallets/Details", new { wallet.WalletId });

        wallet.IsSoftDeleted = false;
        await _walletRepository.AddOrUpdateWallet(wallet);
        TempData[WellKnownTempData.SuccessMessage] = "Wallet restored.";

        return RedirectToPage("/Wallets/Details", new { wallet.WalletId });
    }

    [HttpPost("transactions/{transactionId}/restore")]
    public async Task<IActionResult> RestoreTransaction(string transactionId)
    {
        var transaction = await _walletRepository.GetTransaction(new TransactionQuery
        {
            TransactionId = transactionId,
            IncludeSoftDeleted = true,
            IsServerAdmin = User.IsInRole(Roles.ServerAdmin)
        });
        if (transaction == null)
            return NotFound();
        if (!transaction.IsSoftDeleted)
            return RedirectToPage("/Transactions/Details", new { transaction.WalletId, transaction.TransactionId });

        transaction.IsSoftDeleted = false;
        await _walletRepository.UpdateTransaction(transaction);
        TempData[WellKnownTempData.SuccessMessage] = "Transaction restored.";

        return RedirectToPage("/Transactions/Details", new { transaction.WalletId, transaction.TransactionId });
    }
}

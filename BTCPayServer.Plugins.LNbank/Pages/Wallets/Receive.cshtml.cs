using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client.Models;
using BTCPayServer.Data;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.Authentication;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using LNURL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using AuthenticationSchemes = BTCPayServer.Abstractions.Constants.AuthenticationSchemes;

namespace BTCPayServer.Plugins.LNbank.Pages.Wallets;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = LNbankPolicies.CanCreateInvoices)]
public class ReceiveModel : BasePageModel
{
    private readonly ILogger _logger;

    [BindProperty]
    public string Description { get; set; }

    [BindProperty]
    [DisplayName("Attach description to payment request")]
    public bool AttachDescription { get; set; }

    [BindProperty]
    [DisplayName("Amount")]
    [Range(0, 2100000000000)]
    public long Amount { get; set; }

    [BindProperty]
    [DisplayName("Add routing hints for private channels")]
    public bool PrivateRouteHints { get; set; }

    [BindProperty]
    [DisplayName("Custom invoice expiry")]
    public int? Expiry { get; set; }

    [BindProperty]
    [DisplayName("LNURL Withdraw")]
    public string LNURLW { get; set; }

    public LNURLWithdrawRequest LnurlWithdraw { get; set; }
    public bool IsDefinedAmount { get; set; }

    public ReceiveModel(
        ILogger<ReceiveModel> logger,
        UserManager<ApplicationUser> userManager,
        WalletRepository walletRepository,
        WalletService walletService) : base(userManager, walletRepository, walletService)
    {
        _logger = logger;
    }

    public IActionResult OnGet(string walletId)
    {
        if (CurrentWallet == null)
            return NotFound();

        PrivateRouteHints = CurrentWallet.PrivateRouteHintsByDefault;

        return Page();
    }

    public async Task<IActionResult> OnPostInvoiceAsync(string walletId)
    {
        if (CurrentWallet == null)
            return NotFound();
        if (!ModelState.IsValid)
            return Page();

        try
        {
            if (!string.IsNullOrEmpty(LNURLW))
            {
                LnurlWithdraw = await WalletService.GetWithdrawRequest(LNURLW);
                IsDefinedAmount = LnurlWithdraw.MinWithdrawable == LnurlWithdraw.MaxWithdrawable;
                if (IsDefinedAmount)
                {
                    Amount = (long)LnurlWithdraw.MaxWithdrawable.ToUnit(LightMoneyUnit.Satoshi);
                }
            }

            var amount = LightMoney.Satoshis(Amount).MilliSatoshi;
            var memo = !string.IsNullOrEmpty(Description) ? Description : null;
            var expiry = Expiry is > 0 ? TimeSpan.FromMinutes(Expiry.Value) : WalletService.ExpiryDefault;
            var req = new CreateLightningInvoiceRequest
            {
                Amount = amount,
                Expiry = expiry,
                Description = AttachDescription && !string.IsNullOrEmpty(memo) ? memo : null,
                PrivateRouteHints = PrivateRouteHints
            };
            var transaction = await WalletService.Receive(CurrentWallet, req, memo);

            if (LnurlWithdraw != null)
            {
                await WalletService.GetWithdrawal(LnurlWithdraw, transaction.PaymentRequest);

                TempData[WellKnownTempData.SuccessMessage] = "LNURL Withdraw request sent.";
            }
            return RedirectToPage("/Transactions/Details", new { walletId, transaction.TransactionId });
        }
        catch (Exception exception)
        {
            var text = LnurlWithdraw != null ? "LNURL Withdraw" : "Invoice creation";

            _logger.LogError(exception, "{Text} failed: {Message}", text, exception.Message);

            TempData[WellKnownTempData.ErrorMessage] = $"{text} failed: {exception.Message}";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostLnurlAsync(string walletId)
    {
        if (CurrentWallet == null)
            return NotFound();
        if (!ModelState.IsValid)
            return Page();

        try
        {
            LNURLW = LNURLW.Trim();
            LnurlWithdraw = await WalletService.GetWithdrawRequest(LNURLW);
            IsDefinedAmount = LnurlWithdraw.MinWithdrawable == LnurlWithdraw.MaxWithdrawable;
            Amount = (long)LnurlWithdraw.MaxWithdrawable.ToUnit(LightMoneyUnit.Satoshi);
            Description = !string.IsNullOrEmpty(LnurlWithdraw.DefaultDescription) ? LnurlWithdraw.DefaultDescription : null;

            TempData[WellKnownTempData.SuccessMessage] = "Continue with the values resolved via LNURL Withdraw request.";
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Resolving LNURL Withdraw failed: {Message}", exception.Message);

            TempData[WellKnownTempData.ErrorMessage] = $"Resolving LNURL Withdraw failed: {exception.Message}";
        }

        return Page();
    }
}


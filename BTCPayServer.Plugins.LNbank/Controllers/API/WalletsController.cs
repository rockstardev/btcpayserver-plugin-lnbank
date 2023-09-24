using System;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Client;
using BTCPayServer.Client.Models;
using BTCPayServer.Data;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.Authentication;
using BTCPayServer.Plugins.LNbank.Data.API;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Services;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WalletData = BTCPayServer.Plugins.LNbank.Data.API.WalletData;

namespace BTCPayServer.Plugins.LNbank.Controllers.API;

[ApiController]
[Route("~/api/v1/lnbank/wallets")]
public class WalletsController : ControllerBase
{
    private readonly LNURLService _lnurlService;
    private readonly WalletService _walletService;
    private readonly WalletRepository _walletRepository;
    private readonly UserManager<ApplicationUser> _userManager;

    public WalletsController(
        UserManager<ApplicationUser> userManager,
        WalletRepository walletRepository,
        WalletService walletService,
        LNURLService lnurlService)
    {
        _userManager = userManager;
        _walletRepository = walletRepository;
        _walletService = walletService;
        _lnurlService = lnurlService;
    }

    [HttpGet("")]
    [Authorize(AuthenticationSchemes = AuthenticationSchemes.Greenfield, Policy = Policies.CanModifyProfile)]
    public async Task<IActionResult> GetWallets()
    {
        var wallets = await _walletRepository.GetWallets(new WalletsQuery
        {
            UserId = new[] { GetUserId() },
            IncludeTransactions = true,
            IsServerAdmin = User.IsInRole(Roles.ServerAdmin)
        });

        return Ok(wallets.Select(FromModel));
    }

    [HttpPost("")]
    [Authorize(AuthenticationSchemes = AuthenticationSchemes.Greenfield, Policy = Policies.CanModifyProfile)]
    public async Task<IActionResult> CreateWallet(EditWalletRequest request)
    {
        var validationResult = Validate(request);
        if (validationResult != null)
        {
            return validationResult;
        }

        var wallet = new Wallet
        {
            UserId = GetUserId(),
            Name = request.Name
        };

        var entry = await _walletRepository.AddOrUpdateWallet(wallet);

        return Ok(FromModel(entry));
    }

    [HttpGet("{walletId}")]
    [Authorize(AuthenticationSchemes = LNbankAuthenticationSchemes.AccessKey, Policy = LNbankPolicies.CanViewWallet)]
    public async Task<IActionResult> GetWallet(string walletId)
    {
        var wallet = await FetchWallet(walletId);
        return wallet == null
            ? this.CreateAPIError(404, "wallet-not-found", "The wallet was not found")
            : Ok(FromModel(wallet));
    }

    [HttpPut("{walletId}")]
    [Authorize(AuthenticationSchemes = LNbankAuthenticationSchemes.AccessKey, Policy = LNbankPolicies.CanManageWallet)]
    public async Task<IActionResult> UpdateWallet(string walletId, EditWalletRequest request)
    {
        var validationResult = Validate(request);
        if (validationResult != null)
        {
            return validationResult;
        }

        var wallet = await _walletRepository.GetWallet(new WalletsQuery
        {
            UserId = new[] { GetUserId() },
            WalletId = new[] { walletId },
            IncludeTransactions = true,
            IncludeAccessKeys = true,
            IsServerAdmin = User.IsInRole(Roles.ServerAdmin)
        });

        if (wallet == null)
            return this.CreateAPIError(404, "wallet-not-found", "The wallet was not found");

        wallet.Name = request.Name;

        var entry = await _walletRepository.AddOrUpdateWallet(wallet);

        return Ok(FromModel(entry));
    }

    [HttpDelete("{walletId}")]
    [Authorize(AuthenticationSchemes = LNbankAuthenticationSchemes.AccessKey, Policy = LNbankPolicies.CanManageWallet)]
    public async Task<IActionResult> DeleteWallet(string walletId)
    {
        var wallet = await FetchWallet(walletId);
        if (wallet == null)
            return this.CreateAPIError(404, "wallet-not-found", "The wallet was not found");

        try
        {
            await _walletRepository.RemoveWallet(wallet);
            return Ok();
        }
        catch (Exception e)
        {
            return this.CreateAPIError("wallet-not-empty", e.Message);
        }
    }

    [HttpPost("{walletId}/receive")]
    [Authorize(AuthenticationSchemes = LNbankAuthenticationSchemes.AccessKey, Policy = LNbankPolicies.CanCreateInvoices)]
    public async Task<IActionResult> Receive(string walletId, ReceiveRequest receive)
    {
        var wallet = await FetchWallet(walletId);
        if (wallet == null)
            return this.CreateAPIError(404, "wallet-not-found", "The wallet was not found");

        try
        {
            var memo = !string.IsNullOrEmpty(receive.Description) ? receive.Description : null;
            var expiry = receive.Expiry is > 0 ? TimeSpan.FromMinutes(receive.Expiry.Value) : WalletService.ExpiryDefault;
            var req = new CreateLightningInvoiceRequest
            {
                Amount = receive.Amount,
                Expiry = expiry,
                Description = receive.AttachDescription && !string.IsNullOrEmpty(memo) ? memo : null,
                PrivateRouteHints = receive.PrivateRouteHints ?? wallet.PrivateRouteHintsByDefault
            };

            var transaction = await _walletService.Receive(wallet, req, memo);
            return Ok(FromModel(transaction));
        }
        catch (Exception exception)
        {
            return this.CreateAPIError("generic-error", $"Invoice creation failed: {exception.Message}");
        }
    }

    [HttpPost("{walletId}/send")]
    [Authorize(AuthenticationSchemes = LNbankAuthenticationSchemes.AccessKey, Policy = LNbankPolicies.CanSendMoney)]
    public async Task<IActionResult> Send(string walletId, SendRequest send)
    {
        var wallet = await FetchWallet(walletId);
        if (wallet == null)
            return this.CreateAPIError(404, "wallet-not-found", "The wallet was not found");

        BOLT11PaymentRequest bolt11;
        try
        {
            (bolt11, var lnurlPay) = await _walletService.GetPaymentRequests(send.Destination);

            if (bolt11 == null)
            {
                var isDefinedAmount = lnurlPay.MinSendable == lnurlPay.MaxSendable;
                var amount = isDefinedAmount ? lnurlPay.MinSendable : send.ExplicitAmount;

                if (amount == null)
                {
                    ModelState.AddModelError(nameof(send.ExplicitAmount), "Amount must be defined");
                }
                else
                {
                    bolt11 = await _walletService.GetBolt11(lnurlPay, amount, send.Comment);
                }
            }
        }
        catch (Exception exception)
        {
            return this.CreateAPIError("generic-error", $"Payment failed: {exception.Message}");
        }

        // Abort if there's still no payment request - from here on we require a BOLT11
        if (bolt11 == null)
        {
            ModelState.AddModelError(nameof(send.Destination), "Could not resolve a valid payment request from destination");
        }

        if (!ModelState.IsValid)
        {
            return this.CreateValidationError(ModelState);
        }

        try
        {
            var transaction = await _walletService.Send(wallet, bolt11!, send.Description, explicitAmount: send.ExplicitAmount);
            var data = FromModel(transaction);
            return transaction.IsPending ? Accepted(data) : Ok(data);
        }
        catch (Exception exception)
        {
            return this.CreateAPIError("generic-error", $"Payment failed: {exception.Message}");
        }
    }

    [HttpGet("{walletId}/transactions")]
    [Authorize(AuthenticationSchemes = LNbankAuthenticationSchemes.AccessKey, Policy = LNbankPolicies.CanViewWallet)]
    public async Task<IActionResult> GetTransactions(string walletId)
    {
        var wallet = await FetchWallet(walletId);
        return wallet == null
            ? this.CreateAPIError(404, "wallet-not-found", "The wallet was not found")
            : Ok(wallet.Transactions.Select(FromModel));
    }

    [HttpGet("{walletId}/transactions/{transactionId}")]
    [Authorize(AuthenticationSchemes = LNbankAuthenticationSchemes.AccessKey, Policy = LNbankPolicies.CanViewWallet)]
    public async Task<IActionResult> GetTransaction(string walletId, string transactionId)
    {
        var wallet = await FetchWallet(walletId);
        if (wallet == null)
            return this.CreateAPIError(404, "wallet-not-found", "The wallet was not found");

        var transaction = wallet.Transactions.FirstOrDefault(t => t.TransactionId == transactionId);
        return transaction == null
            ? this.CreateAPIError(404, "transaction-not-found", "The transaction was not found")
            : Ok(FromModel(transaction));
    }

    private async Task<Wallet> FetchWallet(string walletId)
    {
        return await _walletRepository.GetWallet(new WalletsQuery
        {
            UserId = new[] { GetUserId() },
            WalletId = new[] { walletId },
            IncludeTransactions = true,
            IsServerAdmin = User.IsInRole(Roles.ServerAdmin)
        });
    }

    private IActionResult Validate(EditWalletRequest request)
    {
        if (request is null)
        {
            return BadRequest();
        }

        if (string.IsNullOrEmpty(request.Name))
            ModelState.AddModelError(nameof(request.Name), "Name is missing");
        else if (request.Name.Length is < 1 or > 50)
            ModelState.AddModelError(nameof(request.Name), "Name can only be between 1 and 50 characters");

        return !ModelState.IsValid ? this.CreateValidationError(ModelState) : null;
    }

    private WalletData FromModel(Wallet model) =>
        new()
        {
            Id = model.WalletId,
            Name = model.Name,
            CreatedAt = model.CreatedAt,
            Balance = model.Balance,
            AccessKey = model.AccessKeys.FirstOrDefault(ak => ak.UserId == GetUserId())?.Key,
            LnurlPayBech32 = _lnurlService.GetLNURLPayForWallet(Request, model.WalletId, true),
            LnurlPayUri = _lnurlService.GetLNURLPayForWallet(Request, model.WalletId, false)
        };

    private TransactionData FromModel(Transaction model) =>
        new()
        {
            Id = model.TransactionId,
            WalletId = model.WalletId,
            InvoiceId = model.InvoiceId,
            WithdrawConfigId = model.WithdrawConfigId,
            Description = model.Description,
            PaymentRequest = model.PaymentRequest,
            PaymentHash = model.PaymentHash,
            Preimage = model.Preimage,
            Status = model.Status,
            Amount = model.Amount,
            AmountSettled = model.AmountSettled,
            RoutingFee = model.RoutingFee,
            CreatedAt = model.CreatedAt,
            ExpiresAt = model.ExpiresAt,
            PaidAt = model.PaidAt
        };

    private string GetUserId() => _userManager.GetUserId(User);
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Client.Models;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Services;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using LNURL;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.LNbank.Controllers.API;

[ApiController]
[Route("~/api/v1/lnbank/[controller]")]
public class LnurlController : ControllerBase
{
    private readonly WalletService _walletService;
    private readonly WalletRepository _walletRepository;
    private readonly WithdrawConfigRepository _withdrawConfigRepository;

    public LnurlController(WalletService walletService, WalletRepository walletRepository, WithdrawConfigRepository withdrawConfigRepository)
    {
        _walletService = walletService;
        _walletRepository = walletRepository;
        _withdrawConfigRepository = withdrawConfigRepository;
    }

    [HttpGet("{walletId}/pay")]
    public async Task<IActionResult> LnurlPay(string walletId)
    {
        var wallet = await _walletRepository.GetWallet(new WalletsQuery { WalletId = new[] { walletId } });
        if (wallet == null)
        {
            return BadRequest(GetError("The wallet was not found"));
        }

        var data = new List<string[]> { new[] { "text/plain", wallet.Name } };
        var meta = JsonConvert.SerializeObject(data);
        var payRequest = GetPayRequest(wallet.WalletId, meta);

        return Ok(payRequest);
    }

    [HttpGet("{walletId}/pay-callback")]
    public async Task<IActionResult> LnurlPayCallback(string walletId,
        [FromQuery] long? amount = null, string comment = null)
    {
        var wallet = await _walletRepository.GetWallet(new WalletsQuery { WalletId = new[] { walletId } });
        if (wallet == null)
        {
            return BadRequest(GetError("The wallet was not found"));
        }

        var data = new List<string[]> { new[] { "text/plain", wallet.Name } };
        var meta = JsonConvert.SerializeObject(data);
        if (amount is null)
        {
            var payRequest = GetPayRequest(wallet.WalletId, meta);

            return Ok(payRequest);
        }

        comment = comment?.Truncate(LNURLService.CommentLength);

        if (amount < LNURLService.MinSendable || amount > LNURLService.MaxSendable)
        {
            return BadRequest(GetError("Amount is out of bounds"));
        }

        try
        {
            var req = new CreateLightningInvoiceRequest
            {
                Amount = amount.Value,
                Description = meta,
                DescriptionHashOnly = true,
                Expiry = WalletService.ExpiryDefault
            };
            var transaction = await _walletService.Receive(wallet, req, comment);

            var paymentRequest = transaction.PaymentRequest;
            if (_walletService.ValidateDescriptionHash(paymentRequest, meta))
            {
                return Ok(new LNURLPayRequest.LNURLPayRequestCallbackResponse
                {
                    Disposable = true,
                    Routes = Array.Empty<string>(),
                    Pr = paymentRequest
                });
            }
            return BadRequest(GetError("LNbank could not generate invoice with a valid description hash"));
        }
        catch (Exception exception)
        {
            return BadRequest(GetError($"LNbank could not generate invoice: {exception.Message}"));
        }
    }

    [HttpGet("{walletId}/withdraw/{withdrawConfigId}")]
    public async Task<IActionResult> LnurlWithdraw(string walletId, string withdrawConfigId, string pr)
    {
        var withdrawConfig = await _withdrawConfigRepository.GetWithdrawConfig(new WithdrawConfigsQuery
        {
            WalletId = walletId,
            WithdrawConfigId = withdrawConfigId,
            IncludeTransactions = true
        });
        if (withdrawConfig == null)
        {
            return BadRequest(GetError($"The withdraw configuration was not found"));
        }

        var request = GetWithdrawRequest(withdrawConfig);
        if (string.IsNullOrEmpty(pr))
        {
            return Ok(request);
        }

        try
        {
            var transaction = await _walletService.Send(withdrawConfig, pr);

            switch (transaction.LightningPaymentStatus)
            {
                case LightningPaymentStatus.Unknown:
                case LightningPaymentStatus.Pending:
                    return Ok(new LNUrlStatusResponse { Status = "OK", Reason = $"The payment status is {transaction.Status}" });
                case LightningPaymentStatus.Complete:
                    return Ok(new LNUrlStatusResponse { Status = "OK" });
                case LightningPaymentStatus.Failed:
                    return BadRequest(GetError("Payment request could not be paid"));
            }

            return Ok(request);
        }
        catch (Exception exception)
        {
            return BadRequest(GetError($"Payment request could not be paid: {exception.Message}"));
        }
    }

    private static LNUrlStatusResponse GetError(string reason) => new()
    {
        Status = "ERROR",
        Reason = reason
    };

    private LNURLPayRequest GetPayRequest(string walletId, string metadata) => new()
    {
        Tag = LNURLService.PayRequestTag,
        MinSendable = LNURLService.MinSendable,
        MaxSendable = LNURLService.MaxSendable,
        CommentAllowed = LNURLService.CommentLength,
        Callback = new Uri($"{Request.Scheme}://{Request.Host.ToUriComponent()}{Request.PathBase.ToUriComponent()}/api/v1/lnbank/lnurl/{walletId}/pay-callback"),
        Metadata = metadata
    };

    private LNURLWithdrawRequest GetWithdrawRequest(WithdrawConfig withdrawConfig)
    {
        var remaining = withdrawConfig.GetRemainingBalance();
        var oneSat = LightMoney.Satoshis(1);
        var thisUri = new Uri(Request.GetCurrentUrl());
        var request = new LNURLWithdrawRequest
        {
            Tag = LNURLService.WithdrawRequestTag,
            K1 = withdrawConfig.WithdrawConfigId,
            DefaultDescription = withdrawConfig.Name,
            MinWithdrawable = remaining > oneSat ? oneSat : LightMoney.Zero,
            MaxWithdrawable = remaining,
            CurrentBalance = remaining,
            BalanceCheck = thisUri,
            Callback = thisUri
        };
        return request;
    }
}

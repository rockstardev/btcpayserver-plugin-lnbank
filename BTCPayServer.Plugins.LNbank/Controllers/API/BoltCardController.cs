using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Services;
using BTCPayServer.Plugins.LNbank.Services.BoltCard;
using LNURL;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.LNbank.Controllers.API;

[ApiController]
[Route("~/plugins/lnbank/api/boltcard")]
public class BoltCardController : ControllerBase
{
    private readonly BoltCardService _boltCardService;
    private readonly ILogger<BoltCardController> _logger;

    public BoltCardController(BoltCardService boltCardService, ILogger<BoltCardController> logger)
    {
        _boltCardService = boltCardService;
        _logger = logger;
    }

    [HttpGet("pay/{group?}")]
    public async Task<IActionResult> BoltCardPay(int group = 0, CancellationToken cancellationToken = default)
    {
        var url = Request.GetCurrentUrl() + Request.QueryString;
        try
        {
            var result = await _boltCardService.VerifyTap(url, group, cancellationToken);
            return Ok(GetWithdrawRequest(result.Item1.WithdrawConfig, result.authorizationCode));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,"Bolt Card tap verification failed: {Error} (URL: {Url}, Group: {Group})", exception.Message, url, group);
            return BadRequest(GetError(exception.Message));
        }
    }

    [HttpGet("pay-callback")]
    public async Task<IActionResult> BoltCardPayCallback([FromQuery] string pr, [FromQuery] string k1)
    {
        try
        {
            var transaction = await _boltCardService.HandleTapPayment(k1, pr);

            switch (transaction.LightningPaymentStatus)
            {
                case LightningPaymentStatus.Unknown:
                case LightningPaymentStatus.Pending:
                    return Ok(new LNUrlStatusResponse
                        { Status = "OK", Reason = $"The payment status is {transaction.Status}" });
                case LightningPaymentStatus.Complete:
                    return Ok(new LNUrlStatusResponse {Status = "OK"});
                case LightningPaymentStatus.Failed:
                default:
                    return BadRequest(GetError("Payment request could not be paid"));
            }
        }
        catch (Exception e)
        {
            return BadRequest(GetError(e.Message));
        }
    }

    [HttpGet("activate/{code}")]
    public async Task<IActionResult> ActivateCard(string code)
    {
        try
        {
            var card = await _boltCardService.IssueCard(code);
            var index = card.card.Index!.Value;
            return Ok(new NewCardResponse
            {
                CardName = card.card.WithdrawConfig.Name,
                K0 = BoltCardService.ToHexString(card.masterSeed, index, "k0"),
                K1 = BoltCardService.ToHexString(card.masterSeed, index, "k1"),
                K2 = BoltCardService.ToHexString(card.masterSeed, index, "k2"),
                K3 = BoltCardService.ToHexString(card.masterSeed, index, "k3"),
                K4 = BoltCardService.ToHexString(card.masterSeed, index, "k4"),
                LNURLW = Url.Action("BoltCardPay", "BoltCard", new
                {
                    group =  card.group == 0 ? (int?)null : card.group
                }, "lnurlw")
            });
        }
        catch (Exception e)
        {
            return BadRequest(e.Message);
        }
    }

    public class NewCardResponse
    {
        [JsonProperty("protocol_name")] public string ProtocolName { get; set; } = "create_bolt_card_response";
        [JsonProperty("protocol_version")] public int ProtocolVersion { get; set; } = 2;
        [JsonProperty("card_name")] public string CardName { get; set; }
        [JsonProperty("lnurlw_base")] public string LNURLW { get; set; }
        [JsonProperty("uid_privacy")] public string UIDPrivacy { get; set; } = "Y";
        [JsonProperty("k0")] public string K0 { get; set; }
        [JsonProperty("k1")] public string K1 { get; set; }
        [JsonProperty("k2")] public string K2 { get; set; }
        [JsonProperty("k3")] public string K3 { get; set; }
        [JsonProperty("k4")] public string K4 { get; set; }
    }

    private static LNUrlStatusResponse GetError(string reason) => new()
    {
        Status = "ERROR",
        Reason = reason
    };

    private LNURLWithdrawRequest GetWithdrawRequest(WithdrawConfig withdrawConfig, string authorizationCode)
    {
        var remaining = withdrawConfig.GetRemainingBalance();
        var oneSat = LightMoney.Satoshis(1);
        var request = new LNURLWithdrawRequest
        {
            Tag = LNURLService.WithdrawRequestTag,
            K1 = authorizationCode,
            DefaultDescription = withdrawConfig.Name,
            MinWithdrawable = remaining > oneSat ? oneSat : LightMoney.Zero,
            MaxWithdrawable = remaining,
            CurrentBalance = remaining,
            Callback = new Uri(Url.Action("BoltCardPayCallback", "BoltCard", null, Request.Scheme))
        };
        return request;
    }
}

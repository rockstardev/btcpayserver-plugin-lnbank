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
using NBitcoin;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.LNbank.Controllers.API;

[ApiController]
[Route("~/plugins/lnbank/api/boltcard")]
public class BoltCardController : ControllerBase
{
    private readonly BoltCardService _boltCardService;

    public BoltCardController(BoltCardService boltCardService)
    {
        _boltCardService = boltCardService;
    }

    [HttpGet("debug/{p}")]
    public async Task<IActionResult> GetUIDAndCounterAndIndex(string p, [FromServices] LNbankPluginDbContextFactory dbContextFactory)
    {
        var group = 0;
        var settings = await _boltCardService.GetSettings();
        var slipNode = settings.Slip21Node();
        var lowerBound = group * settings.GroupSize;
        var upperBound = lowerBound + settings.GroupSize - 1;
        var url = Request.GetCurrentUrl() + $"?p={p}&c={Convert.ToHexString(RandomUtils.GetBytes(8))}";

        (string uid, uint counter, byte[] rawUid, byte[] rawCtr, byte[] c)? boltCardMatch = null;
        int i;
        for (i = lowerBound; i <= upperBound; i++)
        {
            var k1 = slipNode.DeriveChild(i + "k1").Key.ToBytes().Take(16)
                .ToArray();
            boltCardMatch =
                BoltCardHelper.ExtractBoltCardFromRequest(new Uri(url), k1, out var error);
            if (error is null && boltCardMatch is not null)
                break;
        }

        BoltCard boltCard = null;
        if (boltCardMatch is not null)
        {
            await using var dbContext = dbContextFactory.CreateContext();

            boltCard = await dbContext.BoltCards.AsNoTracking()
                .Include(card => card.WithdrawConfig)
                .FirstOrDefaultAsync(c => c.Index == i);
        }

        return boltCardMatch is null
            ? NotFound("No Bolt Card matched")
            : Ok(new
            {
                index = i,
                boltCardMatch.Value.uid,
                boltCardMatch.Value.counter,
                withdrawConfigId = boltCard?.WithdrawConfigId,
                status = boltCard?.Status,
                savedCounter = boltCard?.Counter
            });
    }

    [HttpGet("pay/{group?}")]
    public async Task<IActionResult> BoltCardPay(int group = 0, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _boltCardService.VerifyTap(Request.GetCurrentUrl() + Request.QueryString, group, cancellationToken);
            return Ok(GetWithdrawRequest(result.Item1.WithdrawConfig, result.authorizationCode));
        }
        catch (Exception e)
        {
            return BadRequest(GetError(e.Message));
        }
    }

    [HttpGet("pay-callback")]
    public async Task<IActionResult> BoltCardPayCallback(string pr, string k1)
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
            Callback = new Uri(Url.Action("BoltCardPayCallback", "BoltCard", null, "lnurlw"))
        };
        return request;
    }
}

#nullable enable
using System;
using System.Net.Http;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.Controllers.API;
using BTCPayServer.Plugins.LNbank.Exceptions;
using LNURL;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using MimeKit;
using NBitcoin;

namespace BTCPayServer.Plugins.LNbank.Services;

public class LNURLService
{
    // see LightningClientFactoryService
    private const string HttpHandlerOnionNamedClient = "lightning.onion";
    private const string HttpHandlerClearnetNamedClient = "lightning.clearnet";

    internal const string PayRequestTag = "payRequest";
    internal const string WithdrawRequestTag = "withdrawRequest";
    internal static readonly LightMoney MinSendable = new(1, LightMoneyUnit.Satoshi);
    internal static readonly LightMoney MaxSendable = LightMoney.FromUnit(6.12m, LightMoneyUnit.BTC);
    internal static readonly int CommentLength = 2000;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly LinkGenerator _linkGenerator;
    private readonly bool _isDevEnv;
    private readonly Network _network;

    public LNURLService(
        IHttpClientFactory httpClientFactory,
        BTCPayNetworkProvider networkProvider,
        LinkGenerator linkGenerator,
        IHostEnvironment env)
    {
        _httpClientFactory = httpClientFactory;
        _linkGenerator = linkGenerator;

        _network = networkProvider.GetNetwork<BTCPayNetwork>(BTCPayService.CryptoCode).NBitcoinNetwork;
        // This is to allow for local testing of LNURL payments
        _isDevEnv = networkProvider.NetworkType == ChainName.Regtest && env.IsDevelopment();
    }

    public async Task<LNURLPayRequest> GetPaymentRequest(string destination)
    {
        var (lnurl, lnurlTag) = GetLNURL(destination);
        return await ResolveLNURL(lnurl, lnurlTag, destination);
    }

    public async Task<BOLT11PaymentRequest> GetBolt11(LNURLPayRequest payRequest, LightMoney? amount = null,
        string? comment = null)
    {
        if (payRequest.Tag != PayRequestTag)
            throw new PaymentRequestValidationException(
                $"Expected LNURL \"{PayRequestTag}\" type, got \"{payRequest.Tag}\".");

        var httpClient = CreateClient(payRequest.Callback);
        var payResponse = await payRequest.SendRequest(amount ?? payRequest.MinSendable, _network, httpClient, comment);
        var bolt11 = payResponse.GetPaymentRequest(_network);

        return bolt11;
    }

    public string GetLNURLPayForWallet(HttpRequest req, string walletId, bool bech32)
    {
        var endpoint = new Uri(_linkGenerator.GetUriByAction(
            action: nameof(LnurlController.LnurlPay),
            controller: "Lnurl",
            values: new { walletId }, req.Scheme, req.Host, req.PathBase) ?? string.Empty);
        return LNURL.LNURL.EncodeUri(endpoint, "payRequest", bech32).ToString().Replace("lightning:", "");
    }

    private async Task<LNURLPayRequest> ResolveLNURL(Uri lnurl, string? lnurlTag, string destination)
    {
        var type = IsLightningAddress(destination) ? "Lightning Address" : "LNURL";
        try
        {
            if (lnurlTag is null)
            {
                var httpClient = CreateClient(lnurl);
                var info = (LNURLPayRequest)await LNURL.LNURL.FetchInformation(lnurl, httpClient);
                lnurlTag = info.Tag;
            }

            if (lnurlTag.Equals(PayRequestTag, StringComparison.InvariantCultureIgnoreCase))
            {
                var httpClient = CreateClient(lnurl);
                var payRequest = (LNURLPayRequest)await LNURL.LNURL.FetchInformation(lnurl, lnurlTag, httpClient);
                return payRequest;
            }
        }
        catch (HttpRequestException ex) when (_isDevEnv &&
                                              ex.Message.StartsWith("The SSL connection could not be established"))
        {
            var lnurlBuilder = new UriBuilder(lnurl) { Scheme = Uri.UriSchemeHttp };
            return await ResolveLNURL(lnurlBuilder.Uri, lnurlTag, destination);
        }
        catch (Exception ex)
        {
            throw new ResolveLNURLException(destination, $"Resolving {type} {destination} failed: {ex.Message}");
        }

        throw new ResolveLNURLException(destination, $"Resolving {type} {destination} failed");
    }

    private (Uri lnurl, string? lnurlTag) GetLNURL(string destination)
    {
        Uri lnurl;
        string? lnurlTag = null;
        var isLnAddress = IsLightningAddress(destination);
        try
        {
            lnurl = isLnAddress
                ? LNURL.LNURL.ExtractUriFromInternetIdentifier(destination)
                : LNURL.LNURL.Parse(destination, out lnurlTag);
        }
        catch (Exception ex)
        {
            var type = isLnAddress ? "Lightning Address" : "LNURL";
            throw new ResolveLNURLException(destination, $"Parsing {type} failed: {ex.Message}");
        }

        return (lnurl, lnurlTag);
    }

    private bool IsLightningAddress(string email)
    {
        if (string.IsNullOrEmpty(email))
            return false;
        if (_isDevEnv)
            return email.Contains('@');

        var options = ParserOptions.Default.Clone();
        options.AllowAddressesWithoutDomain = false;
        return MailboxAddress.TryParse(options, email, out var mailboxAddress) && mailboxAddress is not null;
    }

    private HttpClient CreateClient(Uri uri)
    {
        return _httpClientFactory.CreateClient(uri.IsOnion()
            ? HttpHandlerOnionNamedClient
            : HttpHandlerClearnetNamedClient);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Plugins.LNbank.Controllers.API;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using LNURL;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.LNbank.Hooks;

public class ResolveLightningAddressHandler : IPluginHookFilter
{
    public string Hook { get; } = "resolve-lnurlp-request-for-lightning-address";

    private readonly WalletRepository _walletRepository;
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ResolveLightningAddressHandler(
        IHttpContextAccessor httpContextAccessor,
        WalletRepository walletRepository,
        LinkGenerator linkGenerator)
    {
        _httpContextAccessor = httpContextAccessor;
        _walletRepository = walletRepository;
        _linkGenerator = linkGenerator;
    }

    public async Task<object> Execute(object args)
    {
        var obj = (LightningAddressResolver)args;
        var username = obj.Username;
        var wallet = await _walletRepository.GetWallet(new WalletsQuery
        {
            LightningAddressIdentifier = new[] { username },
        });
        if (wallet == null) return obj;

        var request = _httpContextAccessor.HttpContext.Request;
        var metadata = new Dictionary<string, string>
        {
            ["text/identifier"] = $"{username}@{request.Host}"
        };
        obj.LNURLPayRequest = new LNURLPayRequest
        {
            Tag = "payRequest",
            CommentAllowed = 2000,
            Metadata = JsonConvert.SerializeObject(metadata.Select(kv => new[] { kv.Key, kv.Value })),
            Callback = new Uri(_linkGenerator.GetUriByAction(
                action: nameof(LnurlController.LnurlPay),
                controller: "Lnurl",
                values: new { walletId = wallet.WalletId }, request.Scheme, request.Host, request.PathBase) ?? string.Empty)
        };

        return obj;
    }
}



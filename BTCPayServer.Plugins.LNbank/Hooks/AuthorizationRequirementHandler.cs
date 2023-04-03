using System.Threading.Tasks;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Data;
using BTCPayServer.Plugins.LNbank.Authentication;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using BTCPayServer.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;

namespace BTCPayServer.Plugins.LNbank.Hooks;

public class AuthorizationRequirementHandler : IPluginHookFilter
{
    public string Hook { get; } = "handle-authorization-requirement";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly WalletRepository _walletRepository;

    public AuthorizationRequirementHandler(
        UserManager<ApplicationUser> userManager,
        WalletRepository walletRepository)
    {
        _userManager = userManager;
        _walletRepository = walletRepository;
    }

    public async Task<object> Execute(object args)
    {
        var obj = (AuthorizationFilterHandle)args;
        var httpContext = obj.HttpContext;
        var user = obj.Context.User;
        var userId = _userManager.GetUserId(user);
        var isServerAdmin = user.IsInRole(Roles.ServerAdmin);
        Wallet wallet = null;

        var routeData = httpContext.GetRouteData();
        if (routeData.Values.TryGetValue("walletId", out var vWalletId) && vWalletId is string walletId)
        {
            wallet = await _walletRepository.GetWallet(new WalletsQuery
            {
                UserId = new[] { userId },
                WalletId = new[] { walletId },
                IsServerAdmin = isServerAdmin
            });
        }

        switch (obj.Requirement.Policy)
        {
            case LNbankPolicies.CanManageLNbank:
                if (isServerAdmin)
                    obj.MarkSuccessful();
                break;
            case LNbankPolicies.CanManageWallet:
                if (isServerAdmin || wallet is { AccessLevel: AccessLevel.Admin })
                    obj.MarkSuccessful();
                break;
            case LNbankPolicies.CanSendMoney:
                if (isServerAdmin || wallet is { AccessLevel: AccessLevel.Admin } or { AccessLevel: AccessLevel.Send })
                    obj.MarkSuccessful();
                break;
            case LNbankPolicies.CanCreateInvoices:
                if (isServerAdmin || wallet is { AccessLevel: AccessLevel.Admin } or { AccessLevel: AccessLevel.Send } or { AccessLevel: AccessLevel.Invoice })
                    obj.MarkSuccessful();
                break;
            case LNbankPolicies.CanViewWallet:
                if (wallet != null)
                    obj.MarkSuccessful();
                break;
        }

        if (obj.Success)
        {
            httpContext.Items["BTCPAY.LNBANK.WALLET"] = wallet;
        }

        return obj;
    }
}



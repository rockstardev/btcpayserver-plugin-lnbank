using System.Threading.Tasks;
using BTCPayServer.Plugins.LNbank.Hooks;
using BTCPayServer.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace BTCPayServer.Plugins.LNbank.Authentication;

public class LNbankAuthorizationHandler : AuthorizationHandler<PolicyRequirement>
{
    private readonly HttpContext _httpContext;
    private readonly AuthorizationRequirementHandler _authHandler;

    public LNbankAuthorizationHandler(IHttpContextAccessor httpContextAccessor,
        AuthorizationRequirementHandler authHandler)
    {
        _httpContext = httpContextAccessor.HttpContext;
        _authHandler = authHandler;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        PolicyRequirement requirement)
    {
        if (context.User.Identity?.AuthenticationType != LNbankAuthenticationSchemes.AccessKey)
            return;

        var handle = new AuthorizationFilterHandle(context, requirement, _httpContext);
        await _authHandler.Execute(handle);
        if (handle.Success)
        {
            context.Succeed(requirement);
        }
    }
}

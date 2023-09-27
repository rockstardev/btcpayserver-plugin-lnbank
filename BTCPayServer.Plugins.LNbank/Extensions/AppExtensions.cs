using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Plugins.LNbank.Authentication;
using BTCPayServer.Plugins.LNbank.Hooks;
using BTCPayServer.Plugins.LNbank.Services;
using BTCPayServer.Plugins.LNbank.Services.BoltCard;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace BTCPayServer.Plugins.LNbank.Extensions;

public static class AppExtensions
{
    public static void AddAppServices(this IServiceCollection services)
    {
        services.AddHostedService<LNbankPluginMigrationRunner>();
        services.AddHostedService<LightningInvoiceWatcher>();
        services.AddSingleton<BTCPayService>();
        services.AddSingleton<LNURLService>();
        services.AddSingleton<WalletService>();
        services.AddSingleton<WalletRepository>();
        services.AddSingleton<HistogramService>();
        services.AddSingleton<WithdrawConfigService>();
        services.AddSingleton<WithdrawConfigRepository>();
        services.AddSingleton<ISwaggerProvider, LNbankSwaggerProvider>();
        services.AddSingleton<AuthorizationRequirementHandler>();
        services.AddSingleton<ResolveLightningAddressHandler>();
        services.AddScoped<IAuthorizationHandler, LNbankAuthorizationHandler>();
        services.AddSingleton<BoltCardService>();
        services.AddHostedService(sp => sp.GetRequiredService<BoltCardService>());
    }
}

using System.Threading.Tasks;
using BTCPayServer.Data;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.LNbank.Controllers;

[Route("~/plugins/lnbank/[controller]")]
public class WalletsController : Controller
{
    private readonly WalletRepository _walletRepository;
    private readonly HistogramService _histogramService;
    private readonly UserManager<ApplicationUser> _userManager;

    public WalletsController(UserManager<ApplicationUser> userManager, HistogramService histogramService, WalletRepository walletRepository)
    {
        _userManager = userManager;
        _histogramService = histogramService;
        _walletRepository = walletRepository;
    }

    [HttpGet("{walletId}/histogram/{type}")]
    public async Task<IActionResult> Histogram(string walletId, HistogramType type)
    {
        var wallet = await _walletRepository.GetWallet(new WalletsQuery
        {
            UserId = new [] { GetUserId() },
            WalletId = new[] { walletId },
            IncludeTransactions = true,
            IsServerAdmin = User.IsInRole(Roles.ServerAdmin)
        });
        if (wallet == null)
        {
            return NotFound();
        }

        var data = _histogramService.GetHistogram(wallet, type);
        return data == null
            ? NotFound()
            : Json(data);
    }

    private string GetUserId() => _userManager.GetUserId(User);
}

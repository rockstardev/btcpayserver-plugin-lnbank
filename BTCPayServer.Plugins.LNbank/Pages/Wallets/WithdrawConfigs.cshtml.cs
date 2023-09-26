using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Data;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.Authentication;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Services;
using BTCPayServer.Plugins.LNbank.Services.BoltCard;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using AuthenticationSchemes = BTCPayServer.Abstractions.Constants.AuthenticationSchemes;

namespace BTCPayServer.Plugins.LNbank.Pages.Wallets;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = LNbankPolicies.CanManageWallet)]
public class WithdrawConfigsModel : BasePageModel
{
    private readonly WithdrawConfigRepository _withdrawConfigRepository;
    private readonly BoltCardService _boltCardService;

    [BindProperty]
    public WithdrawConfigViewModel WithdrawConfig { get; set; }
    public IEnumerable<WithdrawConfig> WithdrawConfigs { get; set; }

    public WithdrawConfigsModel(
        UserManager<ApplicationUser> userManager,
        WithdrawConfigRepository withdrawConfigRepository,
        WalletRepository walletRepository,
        WalletService walletService,
        BoltCardService boltCardService) : base(userManager, walletRepository, walletService)
    {
        _withdrawConfigRepository = withdrawConfigRepository;
        _boltCardService = boltCardService;
    }

    public async Task<IActionResult> OnGetAsync(string walletId)
    {
        if (CurrentWallet == null)
            return NotFound();

        WithdrawConfigs = await GetWithdrawConfigs();
        WithdrawConfig = new WithdrawConfigViewModel { ReuseType = WithdrawConfigReuseType.Unlimited };

        return Page();
    }

    public async Task<IActionResult> OnPostAddAsync(string walletId)
    {
        if (CurrentWallet == null)
            return NotFound();

        WithdrawConfigs = await GetWithdrawConfigs();
        if (!ModelState.IsValid)
            return Page();

        if (!Enum.IsDefined(WithdrawConfig.ReuseType))
        {
            ModelState.AddModelError(nameof(WithdrawConfig.ReuseType), "Invalid reuse type");
            return Page();
        }

        if (WithdrawConfig.ReuseType != WithdrawConfigReuseType.Unlimited && WithdrawConfig.Limit is null or <= 0)
        {
            ModelState.AddModelError(nameof(WithdrawConfig.Limit), "Limit must be greater than 0");
            return Page();
        }

        var hasMaxTotal = WithdrawConfig.MaxTotal is > 0;
        var hasMaxPerUse = WithdrawConfig.MaxPerUse is > 0;
        if (hasMaxTotal && hasMaxPerUse && WithdrawConfig.MaxPerUse > WithdrawConfig.MaxTotal)
        {
            ModelState.AddModelError(nameof(WithdrawConfig.MaxPerUse), "Per use value must be less than or equal to max total value");
            return Page();
        }

        var withdrawConfig = new WithdrawConfig
        {
            WalletId = CurrentWallet.WalletId,
            Name = WithdrawConfig.Name,
            ReuseType = WithdrawConfig.ReuseType,
            Limit = WithdrawConfig.Limit,
            MaxTotal = hasMaxTotal ? LightMoney.Satoshis(WithdrawConfig.MaxTotal.Value) : null,
            MaxPerUse = hasMaxPerUse ? LightMoney.Satoshis(WithdrawConfig.MaxPerUse.Value) : null
        };

        await _withdrawConfigRepository.AddWithdrawConfig(withdrawConfig);
        TempData[WellKnownTempData.SuccessMessage] = "Withdraw configuration added successfully.";
        return RedirectToPage("./WithdrawConfigs", new { walletId });
    }

    public async Task<IActionResult> OnPostRemoveAsync(string walletId, string withdrawConfigId)
    {
        if (CurrentWallet == null)
            return NotFound();

        var withdrawConfig = await _withdrawConfigRepository.GetWithdrawConfig(new WithdrawConfigsQuery
        {
            WalletId = walletId,
            WithdrawConfigId = withdrawConfigId
        });
        if (withdrawConfig == null)
            return NotFound();

        try
        {
            await _withdrawConfigRepository.RemoveWithdrawConfig(withdrawConfig);

            TempData[WellKnownTempData.SuccessMessage] = "Withdraw configuration removed successfully.";
            return RedirectToPage("./WithdrawConfigs", new { walletId });
        }
        catch (Exception)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Failed to remove withdraw configuration.";
        }

        WithdrawConfigs = await GetWithdrawConfigs();
        return Page();
    }

    public async Task<IActionResult> OnGetIssueBoltAsync(string walletId, string withdrawConfigId)
    {
        if (CurrentWallet == null)
            return NotFound();

        var withdrawConfig = await _withdrawConfigRepository.GetWithdrawConfig(new WithdrawConfigsQuery
        {
            WalletId = walletId,
            WithdrawConfigId = withdrawConfigId
        });
        if (withdrawConfig == null)
            return NotFound();

        try
        {
            await _boltCardService.CreateCard(withdrawConfigId);

            TempData[WellKnownTempData.SuccessMessage] = "Card issuance started, scan the QR code for activation.";
            return RedirectToPage("./WithdrawConfigs", new { walletId, withdrawConfigId });
        }
        catch (Exception)
        {
            TempData[WellKnownTempData.ErrorMessage] = "Failed to issue bolt card.";
        }

        WithdrawConfigs = await GetWithdrawConfigs();
        return Page();
    }

    public async Task<IActionResult> OnGetReactivateBoltAsync(string walletId, string withdrawConfigId)
    {
        if (CurrentWallet == null)
            return NotFound();

        var withdrawConfig = await _withdrawConfigRepository.GetWithdrawConfig(new WithdrawConfigsQuery
        {
            WalletId = walletId,
            WithdrawConfigId = withdrawConfigId,
            IncludeBoltCard = true
        });
        if (withdrawConfig == null)
            return NotFound();

        if (await _boltCardService.MarkForReactivation(withdrawConfig.BoltCard.BoltCardId))
        {
            TempData[WellKnownTempData.SuccessMessage] = "Card reactivation started, scan the QR code for activation.";
            return RedirectToPage("./WithdrawConfigs", new { walletId, withdrawConfigId });
        }

        TempData[WellKnownTempData.ErrorMessage] = "Failed to reactivate bolt card.";
        WithdrawConfigs = await GetWithdrawConfigs();
        return Page();
    }

    private async Task<IEnumerable<WithdrawConfig>> GetWithdrawConfigs()
    {
        return await _withdrawConfigRepository.GetWithdrawConfigs(new WithdrawConfigsQuery
        {
            WalletId = CurrentWallet.WalletId,
            IncludeTransactions = true,
            IncludeBoltCard = true
        });
    }
}

public class WithdrawConfigViewModel
{
    [Required]
    public string Name { get; set; }

    [Required]
    [DisplayName("Reuse Type")]
    public WithdrawConfigReuseType ReuseType { get; set; }

    public uint? Limit { get; set; }

    [DisplayName("Spendable amount per use")]
    [Range(1, 2100000000000)]
    public long? MaxPerUse { get; set; }

    [DisplayName("Total spendable amount")]
    [Range(1, 2100000000000)]
    public long? MaxTotal { get; set; }
}

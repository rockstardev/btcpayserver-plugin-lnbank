using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
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
            WithdrawConfigId = withdrawConfigId,
            IncludeBoltCard = true
        });
        if (withdrawConfig == null)
            return NotFound();

        var hasBoltCard = await WalletService.HasActiveBoltCard(CurrentWallet);
        if (hasBoltCard && !IsServerAdmin)
        {
            TempData[WellKnownTempData.ErrorMessage] = "This withdraw config still has a Bolt Card associated with it. Make sure to backup the wipe keys for any associated Bolt Card!";
        }
        else
        {
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
            TempData[WellKnownTempData.ErrorMessage] = "Failed to issue Bolt Card.";
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

        var boltCard = withdrawConfig.BoltCard;
        if (boltCard == null)
            return NotFound();

        if (await _boltCardService.MarkForReactivation(boltCard.BoltCardId))
        {
            TempData[WellKnownTempData.SuccessMessage] = "Card reactivation started, scan the QR code for activation.";
            return RedirectToPage("./WithdrawConfigs", new { walletId, withdrawConfigId });
        }

        TempData[WellKnownTempData.ErrorMessage] = "Failed to reactivate Bolt Card.";
        WithdrawConfigs = await GetWithdrawConfigs();
        return Page();
    }

    public async Task<IActionResult> OnGetDeactivateBoltAsync(string walletId, string withdrawConfigId)
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

        var boltCard = withdrawConfig.BoltCard;
        if (boltCard == null)
            return NotFound();

        if (await _boltCardService.MarkInactive(boltCard.BoltCardId))
        {
            TempData[WellKnownTempData.SuccessMessage] = "Card marked as inactive. Wipe it or download the wipe keys and keep them safe!";
            return RedirectToPage("./WithdrawConfigs", new { walletId, withdrawConfigId });
        }

        TempData[WellKnownTempData.ErrorMessage] = "Failed to deactivate Bolt Card.";
        WithdrawConfigs = await GetWithdrawConfigs();
        return Page();
    }

    public async Task<IActionResult> OnGetDownloadWipeKeysAsync(string walletId, string withdrawConfigId)
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

        var boltCard = withdrawConfig.BoltCard;
        if (boltCard == null)
            return NotFound();
        if (boltCard.Status != BoltCardStatus.Active || !boltCard.Index.HasValue)
            return BadRequest();

        var wipeContent = await _boltCardService.GetWipeContent(boltCard.Index.Value);
        if (!string.IsNullOrEmpty(wipeContent))
        {
            var cd = new ContentDisposition
            {
                FileName = $"lnbank-bolt-card-wipe-keys-{boltCard.BoltCardId}.json",
                Inline = false
            };
            Response.Headers.Add("Content-Disposition", cd.ToString());
            Response.Headers.Add("X-Content-Type-Options", "nosniff");

            return Content(wipeContent, "application/json");
        }

        TempData[WellKnownTempData.ErrorMessage] = "Failed to get wipe keys for Bolt Card.";
        WithdrawConfigs = await GetWithdrawConfigs();
        return Page();
    }

    private async Task<IEnumerable<WithdrawConfig>> GetWithdrawConfigs()
    {
        return await _withdrawConfigRepository.GetWithdrawConfigs(new WithdrawConfigsQuery
        {
            WalletId = CurrentWallet.WalletId,
            IncludeTransactions = true,
            IncludeBoltCard = true,
            IncludeSoftDeleted = IsServerAdmin,
            IsServerAdmin = IsServerAdmin
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

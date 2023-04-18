using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using BTCPayServer.Lightning;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.LNbank.Data.Models;

public enum WithdrawConfigReuseType
{
    Unlimited,
    Total,
    [Display(Name = "Per day")]
    PerDay,
    [Display(Name = "Per week")]
    PerWeek,
    [Display(Name = "Per month")]
    PerMonth
}

public class WithdrawConfig
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [DisplayName("Config ID")]
    public string WithdrawConfigId { get; set; }

    [DisplayName("Wallet ID")]
    public string WalletId { get; set; }
    public Wallet Wallet { get; set; }

    [Required]
    public string Name { get; set; }

    [Required]
    [DisplayName("Reuse Type")]
    public WithdrawConfigReuseType ReuseType { get; set; }

    public uint? Limit { get; set; }

    [DisplayName("Spendable amount per use")]
    public LightMoney MaxPerUse { get; set; }

    [DisplayName("Total spendable amount")]
    public LightMoney MaxTotal { get; set; }

    private ICollection<Transaction> GetPaymentsInInterval()
    {
        var payments = Wallet.Transactions.Where(t =>
            t.WithdrawConfigId == WithdrawConfigId && t.AmountSettled != null);

        if (ReuseType is WithdrawConfigReuseType.Unlimited or WithdrawConfigReuseType.Total)
            return payments.ToList();

        var days = ReuseType switch
        {
            WithdrawConfigReuseType.PerDay => 1,
            WithdrawConfigReuseType.PerWeek => 7,
            WithdrawConfigReuseType.PerMonth => 30,
            _ => throw new ArgumentOutOfRangeException()
        };
        var interval = DateTime.UtcNow.AddDays(days * -1);
        return payments.Where(t => t.CreatedAt > interval).ToList();
    }

    public LightMoney GetRemainingBalance(bool total = false)
    {
        var balance = Wallet.Balance;
        var hasTotalLimit = MaxTotal != null && MaxTotal > LightMoney.Zero;
        var hasPerUseLimit = MaxPerUse != null && MaxPerUse > LightMoney.Zero;
        var upperLimit = total ? MaxTotal : MaxPerUse;
        var limit = hasTotalLimit || hasPerUseLimit ? upperLimit : null;
        var payments = GetPaymentsInInterval();
        var remaining = Limit is > 0 && payments.Count >= Limit
            ? LightMoney.Zero
            : (limit ?? balance) + payments.Sum(t => t.AmountSettled);

        return Math.Min(remaining, balance);
    }

    public uint GetRemainingUsages() {
        if (ReuseType == WithdrawConfigReuseType.Unlimited)
            return uint.MaxValue;

        var limit = Limit!.Value;
        var payments = GetPaymentsInInterval();
        return payments.Count >= limit ? 0 : limit - (uint)payments.Count;
    }

    public bool IsSoftDeleted { get; set; }

    internal static void OnModelCreating(ModelBuilder builder)
    {
        builder
            .Entity<WithdrawConfig>()
            .HasIndex(w => w.WalletId);

        builder
            .Entity<WithdrawConfig>()
            .HasQueryFilter(w => !w.IsSoftDeleted);

        builder.Entity<WithdrawConfig>()
            .Property(w => w.ReuseType)
            .HasConversion<string>();

        builder
            .Entity<WithdrawConfig>()
            .Property(e => e.MaxPerUse)
            .HasConversion(
                v => v.MilliSatoshi,
                v => new LightMoney(v));

        builder
            .Entity<WithdrawConfig>()
            .Property(e => e.MaxTotal)
            .HasConversion(
                v => v.MilliSatoshi,
                v => new LightMoney(v));
    }
}

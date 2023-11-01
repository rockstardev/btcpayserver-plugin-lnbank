using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.Services;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
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

    public ICollection<Transaction> GetTransactions()
    {
        return Wallet.Transactions.Where(t =>
            t.WithdrawConfigId == WithdrawConfigId && t.AmountSettled != null).ToList();
    }

    public bool IsSoftDeleted { get; set; }
    public BoltCard BoltCard { get; set; }

    internal static void OnModelCreating(ModelBuilder builder)
    {
        builder
            .Entity<WithdrawConfig>()
            .HasIndex(w => w.WalletId);

        builder
            .Entity<WithdrawConfig>()
            .HasQueryFilter(w => !w.IsSoftDeleted);

        builder
            .Entity<WithdrawConfig>()
            .HasOne(w => w.BoltCard)
            .WithOne(w => w.WithdrawConfig)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .Entity<WithdrawConfig>()
            .HasOne(w => w.Wallet)
            .WithMany(w => w.WithdrawConfigs)
            .OnDelete(DeleteBehavior.Cascade);

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

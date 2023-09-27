using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using BTCPayServer.Lightning;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.LNbank.Data.Models;

public class Wallet
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [DisplayName("Wallet ID")]
    public string WalletId { get; set; }

    [DisplayName("User ID")]
    public string UserId { get; set; }

    [Required]
    public string Name { get; set; }

    [DisplayName("Creation date")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    public LightMoney GetBalance() => GetBalance(Transactions);

    public static LightMoney GetBalance(IEnumerable<Transaction> transactions)
    {
        return transactions
            .Where(t => t.AmountSettled != null)
            .Sum(t => t.AmountSettled - (t.HasRoutingFee ? t.RoutingFee : LightMoney.Zero));
    }

    [NotMapped]
    public bool HasBalance => GetBalance() >= LightMoney.Satoshis(1);

    public ICollection<AccessKey> AccessKeys { get; set; } = new List<AccessKey>();

    [NotMapped]
    public AccessLevel? AccessLevel { get; set; }

    public ICollection<WithdrawConfig> WithdrawConfigs { get; set; } = new List<WithdrawConfig>();

    public bool IsSoftDeleted { get; set; }

    [DisplayName("Add routing hints for private channels by default")]
    public bool PrivateRouteHintsByDefault { get; set; }

    [DisplayName("Lightning Address identifier")]
    public string LightningAddressIdentifier { get; set; }

    internal static void OnModelCreating(ModelBuilder builder)
    {
        builder
            .Entity<Wallet>()
            .HasIndex(o => o.UserId);

        builder
            .Entity<Wallet>()
            .HasQueryFilter(w => !w.IsSoftDeleted);
    }
}

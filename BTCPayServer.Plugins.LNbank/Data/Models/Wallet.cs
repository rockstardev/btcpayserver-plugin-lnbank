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

    public LightMoney Balance
    {
        get => Transactions
                .Where(t => t.AmountSettled != null)
                .Aggregate(LightMoney.Zero, (total, t) => total + t.AmountSettled);
    }

    [NotMapped]
    public bool HasBalance
    {
        get => Balance >= LightMoney.Satoshis(1);
    }

    public ICollection<AccessKey> AccessKeys { get; set; } = new List<AccessKey>();

    [NotMapped]
    public AccessLevel AccessLevel { get; set; }

    public bool IsSoftDeleted { get; set; }

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

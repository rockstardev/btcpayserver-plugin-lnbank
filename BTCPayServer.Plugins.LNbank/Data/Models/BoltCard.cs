using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.LNbank.Data.Models;

public enum BoltCardStatus
{
    PendingActivation,
    Active,
    Inactive
}

public class BoltCard
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string BoltCardId { get; set; }
    public string CardIdentifier { get; set; }
    public int? Index { get; set; }
    public BoltCardStatus Status { get; set; }
    public string WithdrawConfigId { get; set; }
    public WithdrawConfig WithdrawConfig { get; set; }
    public long Counter { get; set; }

    public static void OnModelCreating(ModelBuilder builder)
    {
        builder
            .Entity<BoltCard>()
            .HasIndex(o => o.WithdrawConfigId);

        builder
            .Entity<BoltCard>()
            .HasOne(o => o.WithdrawConfig)
            .WithOne(w => w.BoltCard)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

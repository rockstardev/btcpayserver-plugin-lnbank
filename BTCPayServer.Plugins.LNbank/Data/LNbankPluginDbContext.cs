using BTCPayServer.Plugins.LNbank.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.LNbank.Data;

public class LNbankPluginDbContext : DbContext
{
    private readonly bool _designTime;

    public LNbankPluginDbContext(DbContextOptions<LNbankPluginDbContext> options, bool designTime = false)
        : base(options)
    {
        _designTime = designTime;
    }

    public DbSet<Wallet> Wallets { get; set; }
    public DbSet<AccessKey> AccessKeys { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<WithdrawConfig> WithdrawConfigs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("BTCPayServer.Plugins.LNbank");

        AccessKey.OnModelCreating(modelBuilder);
        Transaction.OnModelCreating(modelBuilder);
        Wallet.OnModelCreating(modelBuilder);
        WithdrawConfig.OnModelCreating(modelBuilder);
    }
}

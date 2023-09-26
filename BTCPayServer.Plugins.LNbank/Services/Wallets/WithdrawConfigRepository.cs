using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Plugins.LNbank.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.LNbank.Services.Wallets;

public class WithdrawConfigRepository
{
    private readonly LNbankPluginDbContextFactory _dbContextFactory;

    public WithdrawConfigRepository(LNbankPluginDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<IEnumerable<WithdrawConfig>> GetWithdrawConfigs(WithdrawConfigsQuery query)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        var queryable = FilterWithdrawConfigs(dbContext.WithdrawConfigs.AsQueryable(), query);
        return await queryable.ToListAsync();
    }

    public async Task<WithdrawConfig> GetWithdrawConfig(WithdrawConfigsQuery query)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        return await FilterWithdrawConfigs(dbContext.WithdrawConfigs.AsQueryable(), query).FirstOrDefaultAsync();
    }

    private IQueryable<WithdrawConfig> FilterWithdrawConfigs(IQueryable<WithdrawConfig> queryable, WithdrawConfigsQuery query)
    {
        if (query.WithdrawConfigId != null)
            queryable = queryable.Where(t => t.WithdrawConfigId == query.WithdrawConfigId);

        if (query.WalletId != null)
            queryable = queryable.Where(t => t.WalletId == query.WalletId);

        if (query.IncludeWallet || query.IncludeTransactions)
            queryable = queryable.Include(t => t.Wallet).AsNoTracking();

        if (query.IncludeTransactions)
            queryable = queryable.Include(t => t.Wallet.Transactions).AsNoTracking();

        if (query.IncludeBoltCard)
            queryable = queryable.Include(t => t.BoltCard).AsNoTracking();

        return queryable;
    }

    public async Task<WithdrawConfig> AddWithdrawConfig(WithdrawConfig withdrawConfig)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        await dbContext.WithdrawConfigs.AddAsync(withdrawConfig);
        await dbContext.SaveChangesAsync();

        return withdrawConfig;
    }

    public async Task RemoveWithdrawConfig(WithdrawConfig withdrawConfig)
    {
        withdrawConfig.IsSoftDeleted = true;

        await using var dbContext = _dbContextFactory.CreateContext();
        dbContext.Update(withdrawConfig);
        await dbContext.SaveChangesAsync();
    }

    public async Task<Data.Models.BoltCard> CreateBoltCardForWithdrawConfig(WithdrawConfig withdrawConfig)
    {
        var boltCard = new Data.Models.BoltCard
        {
            WithdrawConfigId = withdrawConfig.WithdrawConfigId,
            Status = BoltCardStatus.PendingActivation,
            Counter = -1
        };
        await using var dbContext = _dbContextFactory.CreateContext();
        await dbContext.BoltCards.AddAsync(boltCard);
        await dbContext.SaveChangesAsync();

        return boltCard;
    }

    public async Task<IEnumerable<Data.Models.BoltCard>> GetBoltCards(BoltCardsQuery query)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        var queryable = FilterBoltCards(dbContext.BoltCards.AsQueryable(), query);
        return await queryable.ToListAsync();
    }

    public async Task<Data.Models.BoltCard> GetBoltCard(BoltCardsQuery query)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        return await FilterBoltCards(dbContext.BoltCards.AsQueryable(), query).FirstOrDefaultAsync();
    }

    private IQueryable<Data.Models.BoltCard> FilterBoltCards(IQueryable<Data.Models.BoltCard> queryable, BoltCardsQuery query)
    {
        if (query.BoltCardId != null)
            queryable = queryable.Where(b => b.BoltCardId == query.BoltCardId);

        if (query.WithdrawConfigId != null)
            queryable = queryable.Where(b => b.WithdrawConfigId == query.WithdrawConfigId);

        if (query.IncludeWithdrawConfig || query.IncludeTransactions)
            queryable = queryable.Include(b => b.WithdrawConfig).AsNoTracking();

        if (query.IncludeTransactions)
            queryable = queryable.Include(b => b.WithdrawConfig.Wallet).AsNoTracking();

        return queryable;
    }

    public async Task<bool> MarkBoltCardForReactivation(string boltCardId)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        var boltCard = await dbContext.BoltCards.FindAsync(boltCardId);
        if (boltCard == null) return false;

        boltCard.Status = BoltCardStatus.PendingActivation;
        boltCard.CardIdentifier = null;
        boltCard.Counter = -1;
        return await dbContext.SaveChangesAsync() > 0;
    }
}

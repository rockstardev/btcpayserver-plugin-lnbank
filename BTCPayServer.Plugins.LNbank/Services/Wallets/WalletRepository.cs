using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Caching.Memory;

namespace BTCPayServer.Plugins.LNbank.Services.Wallets;

public class WalletRepository
{
    private const string BalancesCacheKey = "LNbankWalletBalances";

    private readonly LNbankPluginDbContextFactory _dbContextFactory;
    private readonly IMemoryCache _memoryCache;

    public WalletRepository(
        LNbankPluginDbContextFactory dbContextFactory,
        IMemoryCache memoryCache)
    {
        _dbContextFactory = dbContextFactory;
        _memoryCache = memoryCache;
    }

    public async Task<IEnumerable<Wallet>> GetWallets(WalletsQuery query)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        var wallets = await FilterWallets(dbContext.Wallets.AsQueryable(), query).ToListAsync();
        return wallets.Select(wallet =>
        {
            if (query.HasAdminAccess(wallet))
            {
                wallet.AccessLevel = AccessLevel.Admin;
            }
            else
            {
                var key = wallet.AccessKeys.FirstOrDefault(ak => query.UserId.Contains(ak.UserId));
                if (key != null)
                    wallet.AccessLevel = key.Level;
            }
            return wallet;
        });
    }

    private IQueryable<Wallet> FilterWallets(IQueryable<Wallet> queryable, WalletsQuery query)
    {
        if (query.UserId != null)
            queryable = queryable
                .Include(w => w.AccessKeys).AsNoTracking()
                .Where(w =>
                    // Admin
                    query.IsServerAdmin ||
                    // Owner
                    query.UserId.Contains(w.UserId) ||
                    // Access key holder
                    w.AccessKeys.Any(ak => query.UserId.Contains(ak.UserId)));

        if (query.AccessKey != null)
            queryable = queryable
                .Include(w => w.AccessKeys).AsNoTracking()
                .Where(w => w.AccessKeys.Any(key => query.AccessKey.Contains(key.Key)));

        if (query.WalletId != null)
            queryable = queryable.Where(wallet => query.WalletId.Contains(wallet.WalletId));

        if (query.LightningAddressIdentifier != null)
            queryable = queryable.Where(wallet => query.LightningAddressIdentifier.Contains(wallet.LightningAddressIdentifier));

        if (query.IncludeTransactions)
            queryable = queryable.Include(w => w.Transactions).AsNoTracking();

        if (query.IncludeAccessKeys)
            queryable = queryable.Include(w => w.AccessKeys).AsNoTracking();

        if (query.IncludeSoftDeleted && query.IsServerAdmin)
            queryable = queryable.IgnoreQueryFilters();

        return queryable;
    }

    public async Task<Wallet> GetWallet(WalletsQuery query)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        var wallet = await FilterWallets(dbContext.Wallets.AsQueryable(), query).FirstOrDefaultAsync();
        if (wallet == null)
            return null;

        AccessKey key = null;
        if (query.HasAdminAccess(wallet))
        {
            wallet.AccessLevel = AccessLevel.Admin;
        }
        else if (query.UserId != null)
        {
            key = wallet.AccessKeys.FirstOrDefault(ak => query.UserId.Contains(ak.UserId));
        }
        else if (query.AccessKey != null)
        {
            key = wallet.AccessKeys.FirstOrDefault(ak => query.AccessKey.Contains(ak.Key));
        }

        if (key != null)
            wallet.AccessLevel = key.Level;

        return wallet;
    }

    public async Task<Wallet> AddOrUpdateWallet(Wallet wallet)
    {
        await using var dbContext = _dbContextFactory.CreateContext();

        EntityEntry entry;
        if (string.IsNullOrEmpty(wallet.WalletId))
        {
            wallet.AccessKeys ??= new List<AccessKey>();
            wallet.AccessKeys.Add(new AccessKey { UserId = wallet.UserId, Level = AccessLevel.Admin });
            entry = await dbContext.Wallets.AddAsync(wallet);
            _memoryCache.Remove(BalancesCacheKey);
        }
        else
        {
            entry = dbContext.Update(wallet);
        }

        await dbContext.SaveChangesAsync();

        return (Wallet)entry.Entity;
    }

    public async Task<AccessKey> AddOrUpdateAccessKey(string walletId, string userId, AccessLevel level)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        var accessKey = await dbContext.AccessKeys.FirstOrDefaultAsync(a => a.WalletId == walletId && a.UserId == userId);

        if (accessKey == null)
        {
            accessKey = new AccessKey { UserId = userId, WalletId = walletId, Level = level };
            await dbContext.AccessKeys.AddAsync(accessKey);
        }
        else if (accessKey.Level != level)
        {
            accessKey.Level = level;
            dbContext.Update(accessKey);
        }

        await dbContext.SaveChangesAsync();

        return accessKey;
    }

    public async Task DeleteAccessKey(string walletId, string key)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        var accessKey = await dbContext.AccessKeys.FirstAsync(a => a.WalletId == walletId && a.Key == key);

        dbContext.AccessKeys.Remove(accessKey);
        await dbContext.SaveChangesAsync();
    }

    public async Task RemoveWallet(Wallet wallet, bool forceDelete = false)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        if (forceDelete)
        {
            dbContext.Wallets.Remove(wallet);
        }
        else
        {
            wallet.IsSoftDeleted = true;
            dbContext.Update(wallet);
        }
        await dbContext.SaveChangesAsync();
        _memoryCache.Remove(BalancesCacheKey);
    }

    public async Task<IEnumerable<Transaction>> GetPendingTransactions()
    {
        return await GetTransactions(new TransactionsQuery
        {
            IncludingPending = true,
            IncludingRevalidating = true,
            IncludingExpired = false,
            IncludingInvalid = false,
            IncludingCancelled = false,
            IncludingPaid = false
        });
    }

    public async Task<Transaction> GetTransaction(TransactionQuery query)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        var queryable = dbContext.Transactions.AsQueryable();

        if (query.WalletId != null)
        {
            var walletQuery = new WalletsQuery
            {
                WalletId = new[] { query.WalletId },
                IncludeTransactions = true
            };
            if (query.UserId != null)
                walletQuery.UserId = new[] { query.UserId };

            var wallet = await GetWallet(walletQuery);
            if (wallet == null)
                return null;

            queryable = wallet.Transactions.AsQueryable();
        }

        if (query.InvoiceId != null) // due to legacy reasons we need to fallback to check the payment hash too
            queryable = queryable.Where(t => t.InvoiceId == query.InvoiceId || t.PaymentHash == query.InvoiceId);
        else if (query.HasInvoiceId)
            queryable = queryable.Where(t => t.InvoiceId != null);

        if (query.TransactionId != null)
            queryable = queryable.Where(t => t.TransactionId == query.TransactionId);

        if (query.PaymentRequest != null)
            queryable = queryable.Where(t => t.PaymentRequest == query.PaymentRequest);

        if (query.PaymentHash != null)
            queryable = queryable.Where(t => t.PaymentHash == query.PaymentHash);

        if (query.IncludeSoftDeleted && query.IsServerAdmin)
            queryable = queryable.IgnoreQueryFilters();

        return queryable.FirstOrDefault();
    }

    public async Task<Transaction> AddTransaction(Transaction transaction, CancellationToken cancellationToken = default)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        var entry = await dbContext.Transactions.AddAsync(transaction, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        InvalidateBalanceCache(transaction.WalletId);

        return entry.Entity;
    }

    public async Task<Transaction> UpdateTransaction(Transaction transaction)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        var entry = dbContext.Entry(transaction);
        entry.State = EntityState.Modified;

        await dbContext.SaveChangesAsync();

        InvalidateBalanceCache(transaction.WalletId);

        return entry.Entity;
    }

    public async Task<bool> SettleTransactionsAtomically(Transaction sendingTransaction, Transaction receivingTransaction, CancellationToken cancellationToken = default)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        var executionStrategy = dbContext.Database.CreateExecutionStrategy();
        var result = false;
        await executionStrategy.ExecuteAsync(async () =>
        {
            await using var dbTransaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var now = DateTimeOffset.UtcNow;
                var receiveEntry = dbContext.Entry(receivingTransaction);
                var sendingEntry = await dbContext.Transactions.AddAsync(sendingTransaction, cancellationToken);

                sendingEntry.Entity.SetSettled(sendingTransaction.Amount, sendingTransaction.AmountSettled, null, now, null);
                receiveEntry.Entity.SetSettled(sendingTransaction.Amount, sendingTransaction.Amount, null, now, null);
                receiveEntry.State = EntityState.Modified;
                await dbContext.SaveChangesAsync(cancellationToken);
                await dbTransaction.CommitAsync(cancellationToken);

                InvalidateBalanceCache(sendingTransaction.WalletId);
                InvalidateBalanceCache(receivingTransaction.WalletId);

                result = true;
            }
            catch (Exception)
            {
                await dbTransaction.RollbackAsync(cancellationToken);
            }
        });
        return result;
    }

    public async Task RemoveTransaction(Transaction transaction, bool forceDelete = false)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        if (forceDelete)
        {
            dbContext.Transactions.Remove(transaction);
        }
        else
        {
            transaction.IsSoftDeleted = true;
            dbContext.Update(transaction);
        }
        await dbContext.SaveChangesAsync();

        InvalidateBalanceCache(transaction.WalletId);
    }

    public async Task<IEnumerable<Transaction>> GetTransactions(TransactionsQuery query)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        var queryable = dbContext.Transactions.AsQueryable();

        if (query.UserId != null)
            query.IncludeWallet = true;

        if (query.WalletId != null)
            queryable = queryable.Where(t => t.WalletId == query.WalletId);

        if (query.IncludeWallet)
            queryable = queryable.Include(t => t.Wallet).AsNoTracking();

        if (query.UserId != null)
            queryable = queryable.Where(t => t.Wallet.UserId == query.UserId);

        if (!query.IncludingPaid)
            queryable = queryable.Where(t => t.PaidAt == null);

        if (!query.IncludingPending)
            queryable = queryable.Where(t => t.PaidAt != null);

        if (!query.IncludingRevalidating)
            queryable = queryable.Where(t => t.ExplicitStatus != Transaction.StatusRevalidating);

        if (!query.IncludingCancelled)
            queryable = queryable.Where(t => t.ExplicitStatus != Transaction.StatusCancelled);

        if (!query.IncludingInvalid)
            queryable = queryable.Where(t => t.ExplicitStatus != Transaction.StatusInvalid);

        if (!query.IncludingExpired)
            queryable = queryable.Where(t => t.ExplicitStatus != Transaction.StatusExpired);

        if (query.IncludeSoftDeleted && query.IsServerAdmin)
            queryable = queryable.IgnoreQueryFilters();

        queryable = query.Type switch
        {
            TransactionType.Invoice => queryable.Where(t => t.InvoiceId != null),
            TransactionType.Payment => queryable.Where(t => t.InvoiceId == null),
            _ => queryable
        };

        return await queryable.ToListAsync();
    }

    public async Task<LightMoney> GetLiabilitiesTotal()
    {
        if (!_memoryCache.TryGetValue<long>(BalancesCacheKey, out var total))
        {
            await using var dbContext = _dbContextFactory.CreateContext();
            total = await dbContext.Transactions.Include(t => t.Wallet).AsNoTracking().AsQueryable()
                .Where(t => t.AmountSettled != null && t.IsSoftDeleted == false && t.Wallet.IsSoftDeleted == false)
                .SumAsync(t => t.AmountSettled);
            _memoryCache.Set(BalancesCacheKey, total, TimeSpan.FromMinutes(5));
        }
        return total;
    }

    public Task<LightMoney> GetBalance(Wallet wallet, CancellationToken cancellationToken = default)
    {
        var id = wallet.WalletId;
        if (!_memoryCache.TryGetValue<LightMoney>(GetBalanceCacheKey(id), out var balance))
        {
            balance = GetBalance(wallet.Transactions);
            _memoryCache.Set(GetBalanceCacheKey(id), balance, TimeSpan.FromMinutes(5));
        }
        return Task.FromResult(balance);
    }

    public LightMoney GetBalance(IEnumerable<Transaction> transactions)
    {
        return transactions
            .Where(t => t.AmountSettled != null && t.IsSoftDeleted == false)
            .Sum(t => t.AmountSettled - (t.HasRoutingFee ? t.RoutingFee : LightMoney.Zero));
    }

    private void InvalidateBalanceCache(string walletId)
    {
        _memoryCache.Remove(GetBalanceCacheKey(walletId));
        _memoryCache.Remove(BalancesCacheKey);
    }

    private static string GetBalanceCacheKey(string walletId)
    {
        return $"LNbankWalletBalance_{walletId}";
    }
}

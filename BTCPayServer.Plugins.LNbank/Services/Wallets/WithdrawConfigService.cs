using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.Data.Models;

namespace BTCPayServer.Plugins.LNbank.Services.Wallets;

public class WithdrawConfigService
{
    private readonly WalletRepository _walletRepository;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _balanceSemaphores = new();

    public WithdrawConfigService(
        WalletRepository walletRepository)
    {
        _walletRepository = walletRepository;
    }

    private ICollection<Transaction> GetPaymentsInInterval(WithdrawConfig withdrawConfig)
    {
        if (withdrawConfig.ReuseType is WithdrawConfigReuseType.Unlimited or WithdrawConfigReuseType.Total)
            return withdrawConfig.GetTransactions().ToList();

        var days = withdrawConfig.ReuseType switch
        {
            WithdrawConfigReuseType.PerDay => 1,
            WithdrawConfigReuseType.PerWeek => 7,
            WithdrawConfigReuseType.PerMonth => 30,
            _ => throw new ArgumentOutOfRangeException()
        };
        var interval = DateTime.UtcNow.AddDays(days * -1);
        return withdrawConfig.GetTransactions().Where(t => t.CreatedAt > interval).ToList();
    }

    private static LightMoney GetSpentAmount(WithdrawConfig withdrawConfig)
    {
        return GetSpentAmount(withdrawConfig.GetTransactions());
    }

    private static LightMoney GetSpentAmount(IEnumerable<Transaction> transactions)
    {
        return Math.Abs(transactions
            .Where(t => t.AmountSettled != null)
            .Sum(t => t.AmountSettled - (t.HasRoutingFee ? t.RoutingFee : LightMoney.Zero)));
    }

    public LightMoney GetSpentTotal(WithdrawConfig withdrawConfig) => GetSpentAmount(withdrawConfig);

    public async Task<LightMoney> GetRemainingBalance(WithdrawConfig withdrawConfig, bool total = false, CancellationToken cancellationToken = default)
    {
        var walletBalance = await _walletRepository.GetBalance(withdrawConfig.Wallet, cancellationToken);

        var semaphore = _balanceSemaphores.GetOrAdd(withdrawConfig.WithdrawConfigId, new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);

        var hasTotalLimit = withdrawConfig.MaxTotal != null && withdrawConfig.MaxTotal > LightMoney.Zero;
        var hasPerUseLimit = withdrawConfig.MaxPerUse != null && withdrawConfig.MaxPerUse > LightMoney.Zero;

        // usage limit
        if (GetRemainingUsages(withdrawConfig) == 0) return LightMoney.Zero;

        // amount limit
        var limit = walletBalance;
        if (hasPerUseLimit && !total)
        {
            limit = limit > withdrawConfig.MaxPerUse ? withdrawConfig.MaxPerUse : limit;
        }
        if (hasTotalLimit)
        {
            var limitTotal = withdrawConfig.MaxTotal - GetSpentTotal(withdrawConfig);
            limit = limitTotal < limit ? limitTotal : limit;
        }

        // Account for fees
        var remainingInSats = limit.ToUnit(LightMoneyUnit.Satoshi);
        var maxFeeAmount = LightMoney.Satoshis(remainingInSats * (decimal)WalletService.MaxFeePercentDefault / 100);
        var remainingMinusFee = limit - maxFeeAmount;
        // allow sweeping transaction if the amount is below threshold and empties the wallet
        if (remainingInSats == walletBalance.ToUnit(LightMoneyUnit.Satoshi) && remainingInSats < 10000)
        {
            remainingMinusFee = walletBalance;
        }

        semaphore.Release();

        return Math.Min(remainingMinusFee, walletBalance);
    }

    public uint GetRemainingUsages(WithdrawConfig withdrawConfig) {
        if (withdrawConfig.ReuseType == WithdrawConfigReuseType.Unlimited)
            return uint.MaxValue;

        var limit = withdrawConfig.Limit!.Value;
        var payments = GetPaymentsInInterval(withdrawConfig);

        return payments.Count >= limit ? 0 : limit - (uint)payments.Count;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.Data.Models;

namespace BTCPayServer.Plugins.LNbank.Services.Wallets;

public class WithdrawConfigService
{
    private readonly WalletRepository _walletRepository;

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

    public LightMoney GetRemainingBalance(WithdrawConfig withdrawConfig, bool total = false)
    {
        var walletBalance = _walletRepository.GetBalance(withdrawConfig.Wallet);
        var hasTotalLimit = withdrawConfig.MaxTotal != null && withdrawConfig.MaxTotal > LightMoney.Zero;
        var hasPerUseLimit = withdrawConfig.MaxPerUse != null && withdrawConfig.MaxPerUse > LightMoney.Zero;
        var upperLimit = total ? withdrawConfig.MaxTotal : withdrawConfig.MaxPerUse;
        var limit = hasTotalLimit || hasPerUseLimit ? upperLimit : null;
        var payments = GetPaymentsInInterval(withdrawConfig);
        if (withdrawConfig.Limit is > 0 && payments.Count >= withdrawConfig.Limit) return LightMoney.Zero;

        // Account for fees
        var remaining = (limit ?? walletBalance) + GetSpentAmount(payments);
        var remainingInSats = remaining.ToUnit(LightMoneyUnit.Satoshi);
        var maxFeeAmount = LightMoney.Satoshis(remainingInSats * (decimal)WalletService.MaxFeePercentDefault / 100);
        var remainingMinusFee = remaining - maxFeeAmount;
        // allow sweeping transaction if the amount is below threshold and empties the wallet
        if (remainingInSats == walletBalance.ToUnit(LightMoneyUnit.Satoshi) && remainingInSats < 10000)
        {
            remainingMinusFee = walletBalance;
        }

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

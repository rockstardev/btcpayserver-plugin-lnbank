using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.Data.Models;

namespace BTCPayServer.Plugins.LNbank.Services.Wallets;

public enum HistogramType
{
    Week,
    Month,
    Year
}

public class HistogramService
{
    public HistogramData GetHistogram(Wallet wallet, HistogramType type = HistogramType.Week)
    {
        var (days, pointCount) = type switch
        {
            HistogramType.Week => (7, 30),
            HistogramType.Month => (30, 30),
            HistogramType.Year => (365, 30),
            _ => throw new ArgumentException($"HistogramType {type} does not exist.")
        };

        const int labelCount = 6;
        var labelEvery = pointCount / labelCount;
        var to = DateTimeOffset.UtcNow;
        var from = to - TimeSpan.FromDays(days);
        var ticks = (to - from).Ticks;
        var interval = TimeSpan.FromTicks(ticks / pointCount);
        var all = wallet.Transactions
            .Where(t => t.AmountSettled != null)
            .OrderBy(t => t.PaidAt);
        if (!all.Any()) return null;

        var beforeAndRange = all
            .GroupBy(t => t.PaidAt >= from)
            .ToList();
        var range = beforeAndRange.FirstOrDefault(g => g.Key)?.ToList() ?? new List<Transaction>();
        var before = beforeAndRange.FirstOrDefault(g => g.Key == false)?.ToList();
        var balance = before == null ? LightMoney.Zero : Wallet.GetBalance(before);
        var series = new List<decimal>(pointCount);
        var labels = new List<string>(pointCount);

        for (var i = 0; i < pointCount; i++)
        {
            var transactions = range.Where(t =>
                t.CreatedAt.Ticks >= from.Ticks + interval.Ticks * i &&
                t.CreatedAt.Ticks < from.Ticks + interval.Ticks * (i + 1));
            balance += Wallet.GetBalance(transactions);
            series.Add(balance.ToUnit(LightMoneyUnit.Satoshi));
            labels.Add(i % labelEvery == 0
                ? (from + interval * i).ToString("MMM dd", CultureInfo.InvariantCulture)
                : null);
        }

        return new HistogramData
        {
            Labels = labels,
            Series = series,
            Type = type
        };
    }
}

public class HistogramData
{
    public HistogramType Type { get; set; }
    public List<decimal> Series { get; set; }
    public List<string> Labels { get; set; }
}


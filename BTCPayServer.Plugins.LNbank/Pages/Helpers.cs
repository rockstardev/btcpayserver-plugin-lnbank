using System;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.Data.Models;

namespace BTCPayServer.Plugins.LNbank.Pages;

public static class Helpers
{
    public static string Sats(LightMoney amount, bool includeUnit = true)
    {
        return amount >= LightMoney.Satoshis(1) || amount <= LightMoney.Satoshis(-1)
            ? $"{Math.Floor(amount.ToUnit(LightMoneyUnit.Satoshi))}{(includeUnit ? " sats" : string.Empty)}"
            : Millisats(amount, includeUnit);
    }

    public static string Millisats(LightMoney amount, bool includeUnit = true)
    {
        return $"{amount.ToUnit(LightMoneyUnit.MilliSatoshi)}{(includeUnit ? " millisats" : string.Empty)}";
    }

    public static string TransactionStateClass(Transaction transaction)
    {
        if (transaction.IsPaid || transaction.IsSettled)
            return transaction.AmountSettled > 0 ? "success" : "danger";

        return transaction.IsExpired || transaction.IsCancelled || transaction.IsInvalid ? "info" : "warning";
    }
}

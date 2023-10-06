using System;
using LNURL;

namespace BTCPayServer.Plugins.LNbank.Exceptions;

public class LNURLWithdrawException : Exception
{
    public LNURLWithdrawRequest WithdrawRequest { get; set; }

    public LNURLWithdrawException(LNURLWithdrawRequest withdrawRequest, string message) : base(message)
    {
        WithdrawRequest = withdrawRequest;
    }
}

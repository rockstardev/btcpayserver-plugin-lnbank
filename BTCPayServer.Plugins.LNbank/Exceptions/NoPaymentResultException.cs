using System;

namespace BTCPayServer.Plugins.LNbank.Exceptions;

public class NoPaymentResultException : Exception
{
    public NoPaymentResultException(string message) : base(message)
    {
    }
}

using System;
using BTCPayServer.Client.JsonConverters;
using BTCPayServer.Lightning;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.LNbank.Data.API;

public class TransactionData
{
    public string Id { get; set; }
    public string WalletId { get; set; }
    public string InvoiceId { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string WithdrawConfigId { get; set; }

    public string Description { get; set; }
    public string PaymentRequest { get; set; }
    public string PaymentHash { get; set; }
    public string Preimage { get; set; }
    public string Status { get; set; }

    [JsonConverter(typeof(LightMoneyJsonConverter))]
    public LightMoney Amount { get; set; }

    [JsonConverter(typeof(LightMoneyJsonConverter))]
    public LightMoney AmountSettled { get; set; }

    [JsonConverter(typeof(LightMoneyJsonConverter))]
    public LightMoney RoutingFee { get; set; }

    [JsonConverter(typeof(NBitcoin.JsonConverters.DateTimeToUnixTimeConverter))]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonConverter(typeof(NBitcoin.JsonConverters.DateTimeToUnixTimeConverter))]
    public DateTimeOffset ExpiresAt { get; set; }

    [JsonConverter(typeof(NBitcoin.JsonConverters.DateTimeToUnixTimeConverter))]
    public DateTimeOffset? PaidAt { get; set; }
}

using BTCPayServer.Client.JsonConverters;
using BTCPayServer.Lightning;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.LNbank.Data.API;

public class ReceiveRequest
{
    public string Description { get; set; }

    [JsonConverter(typeof(LightMoneyJsonConverter))]
    public LightMoney Amount { get; set; }

    public bool AttachDescription { get; set; }

    public bool? PrivateRouteHints { get; set; }

    public int? Expiry { get; set; }
}

using BTCPayServer.Client.JsonConverters;
using BTCPayServer.Lightning;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.LNbank.Data.API;

public class SendRequest
{
    public string Destination { get; set; }
    public string Description { get; set; }
    public string Comment { get; set; }

    [JsonConverter(typeof(LightMoneyJsonConverter))]
    public LightMoney ExplicitAmount { get; set; }
}

namespace BTCPayServer.Plugins.LNbank.Data.API;

public class EditWalletRequest
{
    public string Name { get; set; }
    public string LightningAddressIdentifier { get; set; }
}

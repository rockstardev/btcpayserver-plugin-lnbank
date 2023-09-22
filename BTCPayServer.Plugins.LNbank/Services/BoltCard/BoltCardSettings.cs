using NBitcoin.Altcoins.Elements;

namespace BTCPayServer.Plugins.LNbank.Services.BoltCard;

public class BoltCardSettings
{
    // a master seed used for SLIP21, to derive symmetric keys deterministically, must be 64bytes
    public string MasterSeed { get; set; }
    public int LastIndexUsed { get; set; } = -1;
    public int GroupSize { get; set; } = 1000;

    public Slip21Node Slip21Node()
    {
        return MasterSeed is not null ? NBitcoin.Altcoins.Elements.Slip21Node.FromSeed(MasterSeed) : null;
    }
}

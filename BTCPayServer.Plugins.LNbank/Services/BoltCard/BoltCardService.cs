using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.HostedServices;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using LNURL;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Altcoins.Elements;
using Newtonsoft.Json.Linq;
using Transaction = BTCPayServer.Plugins.LNbank.Data.Models.Transaction;

namespace BTCPayServer.Plugins.LNbank.Services.BoltCard;

public class BoltCardService : EventHostedServiceBase
{
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _verificationSemaphores = new();
    private readonly ILogger<BoltCardService> _logger;
    private readonly ISettingsRepository _settingsRepository;
    private readonly LNbankPluginDbContextFactory _dbContextFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly WalletService _walletService;

    public BoltCardService(
        ISettingsRepository settingsRepository,
        EventAggregator eventAggregator,
        ILogger<BoltCardService> logger,
        LNbankPluginDbContextFactory dbContextFactory,
        IMemoryCache memoryCache,
        WalletService walletService) : base(eventAggregator, logger)
    {
        _logger = logger;
        _settingsRepository = settingsRepository;
        _dbContextFactory = dbContextFactory;
        _memoryCache = memoryCache;
        _walletService = walletService;
    }

    private readonly SemaphoreSlim _settingsSemaphore = new(1, 1);

    public async Task<BoltCardSettings> GetSettings()
    {
        await _settingsSemaphore.WaitAsync();
        var settings = await _settingsRepository.GetSettingAsync<BoltCardSettings>(nameof(BoltCardSettings));
        settings ??= new BoltCardSettings();
        if (settings.MasterSeed is null)
        {
            settings.MasterSeed = Convert.ToHexString(RandomUtils.GetBytes(64));
            settings.LastIndexUsed = 0;
            settings.GroupSize = await ComputeGroupSize();

            await _settingsRepository.UpdateSetting(settings, nameof(BoltCardSettings));
        }
        _settingsSemaphore.Release();

        return settings;
    }

    private async Task<int> ComputeGroupSize()
    {
        int OneSecondOfCompute()
        {
            var sw = new Stopwatch();
            sw.Start();
            var attempts = 0;
            while (sw.Elapsed.Seconds < 1)
            {
                try
                {
                    var key1 = RandomUtils.GetBytes(16);
                    BoltCardHelper.ExtractBoltCardFromRequest(
                        new Uri("https://test.com?p=4E2E289D945A66BB13377A728884E867&c=E19CCB1FED8892CE"),
                        key1, out _);
                }
                catch (Exception)
                {
                    // ignored
                }

                attempts++;
            }

            return attempts;
        }

        return (int) (await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => Task.Run(OneSecondOfCompute)))).Average();
    }

    public async Task<string> GetWipeContent(int index)
    {
        var settings = await GetSettings();
        var slip21Node = settings.Slip21Node();
        return JObject.FromObject(new
        {
            version = 1,
            action = "wipe",
            k0 = ToHexString(slip21Node, index, "k0"),
            k1 = ToHexString(slip21Node, index, "k1"),
            k2 = ToHexString(slip21Node, index, "k2"),
            k3 = ToHexString(slip21Node, index, "k3"),
            k4 = ToHexString(slip21Node, index, "k4"),
        }).ToString();
    }

    public static string ToHexString(Slip21Node slip21Node, int index, string field)
    {
        return Convert.ToHexString(slip21Node.DeriveChild(index + field).Key.ToBytes().Take(16).ToArray());
    }

    private record IncrementDerivationIndexEvt(TaskCompletionSource<int> tcs);

    protected override async Task ProcessEvent(object evt, CancellationToken cancellationToken)
    {
        // we use sequential processing of these to avoid race conditions such as two cards being issued with the same index or a counter check failing
        if (evt is IncrementDerivationIndexEvt incrementDerivationIndexEvt)
        {
            var settings = await GetSettings();

            settings.LastIndexUsed++;
            await _settingsRepository.UpdateSetting(settings, nameof(BoltCardSettings));
            incrementDerivationIndexEvt.tcs.SetResult(settings.LastIndexUsed);
        }
        await  base.ProcessEvent(evt, cancellationToken);
    }

    private async Task<int> IncrementDerivationIndex()
    {
        var tcs = new TaskCompletionSource<int>();
        PushEvent(new IncrementDerivationIndexEvt(tcs));
        return await tcs.Task;
    }

    public async Task MarkForReactivation(string code)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        var card = await dbContext.BoltCards.FindAsync( code);
        if (card is null)
        {
            throw new Exception("Card not found");
        }
        card.Status = BoltCardStatus.PendingActivation;
        card.CardIdentifier = null;
        card.Counter = -1;
        await dbContext.SaveChangesAsync();
    }

    public async Task<string> CreateCard(string withdrawConfigId)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        var withdrawConfig = await dbContext.WithdrawConfigs.FindAsync(withdrawConfigId);
        if (withdrawConfig is null)
        {
            throw new Exception("Withdraw config not found");
        }

        var boltCard = new Data.Models.BoltCard
        {
            Counter = -1,
            WithdrawConfigId = withdrawConfigId,
            Status = BoltCardStatus.PendingActivation
        };
        await dbContext.BoltCards.AddAsync(boltCard);
        await dbContext.SaveChangesAsync();
        return boltCard.BoltCardId;
    }

    public async Task<(Data.Models.BoltCard card, Slip21Node masterSeed, int group)> IssueCard(string activationCode)
    {
        await using var dbContext = _dbContextFactory.CreateContext();

        var card = await dbContext.BoltCards
            .Include(boltCard => boltCard.WithdrawConfig)
            .SingleOrDefaultAsync(boltCard => boltCard.BoltCardId == activationCode &&
                                              boltCard.Status == BoltCardStatus.PendingActivation);
        if (card is null)
            throw new Exception("Card not found or already activated");

        var settings = await GetSettings();
        card.Index ??= await IncrementDerivationIndex();
        card.Status = BoltCardStatus.Active;

        await dbContext.SaveChangesAsync();
        var index = (int)card.Index;
        var groupSize = settings.GroupSize;
        var groupNumber = index / groupSize;
        return (card, settings.Slip21Node(), groupNumber);
    }

    public async Task<(Data.Models.BoltCard, string authorizationCode)> VerifyTap(string url, int group, CancellationToken cancellationToken)
    {
        var settings = await GetSettings();
        var slipNode = settings.Slip21Node();
        var lowerBound = group * settings.GroupSize;
        var upperBound = lowerBound + settings.GroupSize - 1;

        (string uid, uint counter, byte[] rawUid, byte[] rawCtr, byte[] c)? boltCardMatch = null;
        int i;
        for (i = lowerBound; i <= upperBound; i++)
        {
            var k1 = slipNode.DeriveChild(i + "k1").Key.ToBytes().Take(16)
                .ToArray();
            boltCardMatch =
                BoltCardHelper.ExtractBoltCardFromRequest(new Uri(url), k1, out var error);
            if (error is null && boltCardMatch is not null)
                break;
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (boltCardMatch is null)
            throw new Exception("No matching card found");

        var semaphore = _verificationSemaphores.GetOrAdd(i, new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);  // Wait for the semaphore if it's locked by another task
        Data.Models.BoltCard matchedCard;
        try
        {
            await using var dbContext = _dbContextFactory.CreateContext();

            matchedCard = await dbContext.BoltCards.AsNoTracking()
                .Include(card => card.WithdrawConfig)
                .FirstOrDefaultAsync(card => card.Index == i, cancellationToken);

            if (matchedCard is null)
                throw new Exception("No matching card exists", null);

            if (matchedCard.Status != BoltCardStatus.Active)
                throw new Exception("Card is not active", null);

            if (matchedCard.Counter >= boltCardMatch.Value.counter)
                throw new Exception("Counter is too low", null);

            matchedCard.CardIdentifier ??= boltCardMatch.Value.uid;
            if (matchedCard.CardIdentifier != boltCardMatch.Value.uid)
                throw new Exception("Card mismatch", null);

            var k2 =  slipNode.DeriveChild(i + "k2").Key.ToBytes().Take(16)
                .ToArray();

            if (!BoltCardHelper.CheckCmac(boltCardMatch.Value.rawUid, boltCardMatch.Value.rawCtr, k2,
                    boltCardMatch.Value.c, out var error2))
            {
                throw new Exception($"C invalid: {error2}", null);
            }
            matchedCard.Counter = (int)boltCardMatch.Value.counter;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogInformation("Bolt Card tap verification failed: {Error} (URL: {Url}, Group: {Group})", e.Message, url, group);
            throw;
        }
        finally
        {
            semaphore.Release();
        }

        var authorizationCode = Guid.NewGuid().ToString();
        _memoryCache.CreateEntry("BoltCardAuthorizationCode_" + authorizationCode).SetValue(matchedCard).AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
        return (matchedCard, authorizationCode);
    }

    public async Task<Transaction> HandleTapPayment(string authorizationCode, string paymentRequest)
    {
        var card = _memoryCache.Get<Data.Models.BoltCard>("BoltCardAuthorizationCode_" + authorizationCode);
        if (card is null)
            throw new Exception("Invalid authorization code");
        return await _walletService.Send(card.WithdrawConfig, paymentRequest);
    }
}

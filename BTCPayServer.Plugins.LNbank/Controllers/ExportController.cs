using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Plugins.LNbank.Authentication;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.LNbank.Controllers;

[Route("~/plugins/lnbank/[controller]")]
public class ExportController : Controller
{
    private readonly WalletRepository _walletRepository;
    private readonly UserManager<ApplicationUser> _userManager;

    public ExportController(UserManager<ApplicationUser> userManager, WalletRepository walletRepository)
    {
        _userManager = userManager;
        _walletRepository = walletRepository;
    }

    [HttpGet("{walletId}/{format}")]
    [Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = LNbankPolicies.CanViewWallet)]
    public async Task<IActionResult> Export(string walletId, string format)
    {
        var wallet = await _walletRepository.GetWallet(new WalletsQuery
        {
            UserId = new [] { GetUserId() },
            WalletId = new[] { walletId },
            IncludeTransactions = true,
            IsServerAdmin = User.IsInRole(Roles.ServerAdmin)
        });
        if (wallet == null)
        {
            return NotFound();
        }

        var transactions = wallet.Transactions.OrderByDescending(t => t.CreatedAt).Select(ToExportModel);
        var data = format switch
        {
            "json" => ToJson(transactions),
            "csv" => await ToCsv(transactions),
            _ => throw new NotSupportedException("Unsupported format.")
        };

        var cd = new ContentDisposition
        {
            FileName = $"lnbank-export-{DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}-{wallet.WalletId}.{format}",
            Inline = true
        };
        Response.Headers.Add("Content-Disposition", cd.ToString());
        Response.Headers.Add("X-Content-Type-Options", "nosniff");

        return Content(data, $"application/{format}");
    }

    private static string ToJson(IEnumerable<ExportModel> transactions)
    {
        var settings = new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };
        return JsonConvert.SerializeObject(transactions, Formatting.Indented, settings);
    }

    private static async Task<string> ToCsv(IEnumerable<ExportModel> transactions)
    {
        await using StringWriter writer = new();
        await using var csvWriter = new CsvHelper.CsvWriter(writer, CultureInfo.InvariantCulture, true);
        csvWriter.WriteHeader<ExportModel>();
        await csvWriter.NextRecordAsync();
        await csvWriter.WriteRecordsAsync(transactions);
        await csvWriter.FlushAsync();
        return writer.ToString();
    }

    private static ExportModel ToExportModel(Transaction t) => new()
    {
        TransactionId = t.TransactionId,
        InvoiceId = t.InvoiceId,
        Description = t.Description,
        PaymentRequest = t.PaymentRequest,
        PaymentHash = t.PaymentHash,
        Preimage = t.Preimage,
        Status = t.Status,
        Amount = t.Amount?.MilliSatoshi,
        AmountSettled = t.AmountSettled?.MilliSatoshi,
        RoutingFee = t.RoutingFee?.MilliSatoshi,
        CreatedAt = t.CreatedAt,
        PaidAt = t.PaidAt
    };

    private class ExportModel
    {
        public string TransactionId { get; set; }
        public string InvoiceId { get; set; }
        public string Description { get; set; }
        public string PaymentRequest { get; set; }
        public string PaymentHash { get; set; }
        public string Preimage { get; set; }
        public string Status { get; set; }
        public long? Amount { get; set; }
        public long? AmountSettled { get; set; }
        public long? RoutingFee { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? PaidAt { get; set; }
    }

    private string GetUserId() => _userManager.GetUserId(User);
}

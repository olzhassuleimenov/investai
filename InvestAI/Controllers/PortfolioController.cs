using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Security.Claims;

[Authorize]
public class PortfolioController : Controller
{
    private readonly IMongoCollection<Portfolio> _portfolios;
    private readonly IMongoCollection<Asset> _assets;
    private readonly IMongoCollection<Chat> _chats;
    private readonly QuotesService _quotes;

    public PortfolioController(IMongoDatabase db, QuotesService quotes)
    {
        _portfolios = db.GetCollection<Portfolio>("Portfolios");
        _assets = db.GetCollection<Asset>("Assets");
        _chats = db.GetCollection<Chat>("Chats");
        _quotes = quotes;
    }

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // GET /Portfolio/Dashboard
    public async Task<IActionResult> Dashboard()
    {
        var userId = GetUserId();
        var portfolio = await _portfolios
            .Find(p => p.UserId == userId)
            .FirstOrDefaultAsync();

        if (portfolio == null)
            return RedirectToAction("Login", "Auth");

        var assets = await _assets
            .Find(a => a.PortfolioId == portfolio.Id)
            .ToListAsync();

        var tickers = assets.Select(a => a.Ticker).Distinct();
        var prices = await _quotes.GetPricesAsync(tickers);

        var assetVms = assets.Select(a => {
            prices.TryGetValue(a.Ticker, out var price);
            var pnl = ((double)price - a.AvgBuyPrice) * a.Quantity;
            var pnlPercent = a.AvgBuyPrice > 0
                ? ((double)price - a.AvgBuyPrice) / a.AvgBuyPrice * 100
                : 0;
            return new AssetViewModel
            {
                Id = a.Id,
                PortfolioId = a.PortfolioId,
                UserId = a.UserId,
                Type = a.Type,
                Ticker = a.Ticker,
                Name = a.Name,
                Quantity = a.Quantity,
                AvgBuyPrice = a.AvgBuyPrice,
                TotalInvested = a.TotalInvested,
                Currency = a.Currency,
                BuyDate = a.BuyDate,
                Sector = a.Sector,
                DividendYield = a.DividendYield,
                DividendFreq = a.DividendFreq,
                NextDividendDate = a.NextDividendDate,
                CouponRate = a.CouponRate,
                CouponFreqPerYear = a.CouponFreqPerYear,
                MaturityDate = a.MaturityDate,
                FaceValue = a.FaceValue,
                CurrentPrice = price,
                Pnl = (decimal)pnl,
                PnlPercent = (decimal)pnlPercent,
                TotalValue = price * (decimal)a.Quantity
            };
        }).ToList();

        var totalValue = assetVms.Sum(a => a.TotalValue);
        var totalInvested = (decimal)assetVms.Sum(a => a.TotalInvested);
        var totalPnl = totalValue - totalInvested;
        var totalPnlPct = totalInvested > 0 ? totalPnl / totalInvested * 100 : 0;
        var annualDiv = (decimal)assetVms
            .Where(a => a.Type == "stock" && a.DividendYield.HasValue)
            .Sum(a => a.TotalInvested * a.DividendYield!.Value / 100);

        var chats = await _chats
            .Find(c => c.UserId == userId)
            .SortByDescending(c => c.UpdatedAt)
            .ToListAsync();

        var vm = new DashboardViewModel
        {
            Portfolio = portfolio,
            Assets = assetVms,
            TotalValue = totalValue,
            TotalInvested = totalInvested,
            TotalPnl = totalPnl,
            TotalPnlPercent = totalPnlPct,
            AnnualDividends = annualDiv,
            Chats = chats
        };

        return View(vm);
    }

    // POST /Portfolio/Create
    [HttpPost]
    public async Task<IActionResult> Create(string name, string currency)
    {
        var userId = GetUserId();
        await _portfolios.InsertOneAsync(new Portfolio
        {
            UserId = userId,
            Name = name,
            Currency = currency,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        return RedirectToAction("Dashboard");
    }

    // POST /Portfolio/Delete/{id}
    [HttpPost]
    public async Task<IActionResult> Delete(string id)
    {
        var userId = GetUserId();
        await _portfolios.DeleteOneAsync(p => p.Id == id && p.UserId == userId);
        await _assets.DeleteManyAsync(a => a.PortfolioId == id);
        return RedirectToAction("Dashboard");
    }
}
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Security.Claims;

[Authorize]
public class AssetsController : Controller
{
    private readonly IMongoCollection<Asset> _assets;
    private readonly IMongoCollection<Portfolio> _portfolios;

    public AssetsController(IMongoDatabase db)
    {
        _assets = db.GetCollection<Asset>("Assets");
        _portfolios = db.GetCollection<Portfolio>("Portfolios");
    }

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // GET /Assets/Create
    public IActionResult Create() => View();

    // POST /Assets/Create
    [HttpPost]
    public async Task<IActionResult> Create(AssetViewModel vm)
    {
        var userId = GetUserId();
        var portfolio = await _portfolios
            .Find(p => p.UserId == userId)
            .FirstOrDefaultAsync();

        var asset = new Asset
        {
            PortfolioId = portfolio.Id,
            UserId = userId,
            Type = vm.Type,
            Ticker = vm.Ticker.ToUpper(),
            Name = vm.Name,
            Quantity = vm.Quantity,
            AvgBuyPrice = vm.AvgBuyPrice,
            TotalInvested = Math.Round(vm.AvgBuyPrice * vm.Quantity, 2),
            Currency = vm.Currency,
            BuyDate = vm.BuyDate,
            Sector = vm.Sector,
            Exchange = vm.Exchange,
            DividendYield = vm.DividendYield,
            DividendFreq = vm.DividendFreq,
            NextDividendDate = vm.NextDividendDate,
            CouponRate = vm.CouponRate,
            CouponFreqPerYear = vm.CouponFreqPerYear,
            MaturityDate = vm.MaturityDate,
            FaceValue = vm.FaceValue,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _assets.InsertOneAsync(asset);
        return RedirectToAction("Dashboard", "Portfolio");
    }

    // GET /Assets/Edit/{id}
    public async Task<IActionResult> Edit(string id)
    {
        var userId = GetUserId();
        var asset = await _assets
            .Find(a => a.Id == id && a.UserId == userId)
            .FirstOrDefaultAsync();

        if (asset == null) return NotFound();

        var vm = new AssetViewModel
        {
            Id = asset.Id,
            Type = asset.Type,
            Ticker = asset.Ticker,
            Name = asset.Name,
            Quantity = asset.Quantity,
            AvgBuyPrice = asset.AvgBuyPrice,
            Currency = asset.Currency,
            BuyDate = asset.BuyDate,
            Sector = asset.Sector,
            DividendYield = asset.DividendYield,
            CouponRate = asset.CouponRate,
            CouponFreqPerYear = asset.CouponFreqPerYear,
            MaturityDate = asset.MaturityDate,
            FaceValue = asset.FaceValue
        };
        return View(vm);
    }

    // POST /Assets/Edit/{id}
    [HttpPost]
    public async Task<IActionResult> Edit(string id, AssetViewModel vm)
    {
        var userId = GetUserId();
        var update = Builders<Asset>.Update
            .Set(a => a.Ticker, vm.Ticker.ToUpper())
            .Set(a => a.Name, vm.Name)
            .Set(a => a.Quantity, vm.Quantity)
            .Set(a => a.AvgBuyPrice, vm.AvgBuyPrice)
            .Set(a => a.TotalInvested, Math.Round(vm.AvgBuyPrice * vm.Quantity, 2))
            .Set(a => a.Currency, vm.Currency)
            .Set(a => a.Sector, vm.Sector)
            .Set(a => a.DividendYield, vm.DividendYield)
            .Set(a => a.CouponRate, vm.CouponRate)
            .Set(a => a.MaturityDate, vm.MaturityDate)
            .Set(a => a.FaceValue, vm.FaceValue)
            .Set(a => a.UpdatedAt, DateTime.UtcNow);

        await _assets.UpdateOneAsync(
            a => a.Id == id && a.UserId == userId, update);

        return RedirectToAction("Dashboard", "Portfolio");
    }

    // POST /Assets/Delete/{id}
    [HttpPost]
    public async Task<IActionResult> Delete(string id)
    {
        var userId = GetUserId();
        await _assets.DeleteOneAsync(a => a.Id == id && a.UserId == userId);
        return RedirectToAction("Dashboard", "Portfolio");
    }
}
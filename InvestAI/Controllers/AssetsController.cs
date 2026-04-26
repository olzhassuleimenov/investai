using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Security.Claims;

[ApiController]
[Route("api/v1/portfolios/{portfolioId}/assets")]
[Authorize]
public class AssetsController : ControllerBase
{
    private readonly IMongoCollection<Asset> _assets;
    private readonly IMongoCollection<Portfolio> _portfolios;
    private readonly QuotesService _quotes;

    public AssetsController(IMongoDatabase db, QuotesService quotes)
    {
        _assets = db.GetCollection<Asset>("Assets");
        _portfolios = db.GetCollection<Portfolio>("Portfolios");
        _quotes = quotes;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // GET /api/v1/portfolios/{portfolioId}/assets
    [HttpGet]
    public async Task<IActionResult> GetAll(string portfolioId)
    {
        var portfolio = await _portfolios
            .Find(p => p.Id == portfolioId && p.UserId == UserId)
            .FirstOrDefaultAsync();

        if (portfolio == null)
            return NotFound(new { error = "Портфель не найден" });

        var assets = await _assets
            .Find(a => a.PortfolioId == portfolioId)
            .ToListAsync();

        var tickers = assets.Select(a => a.Ticker).Distinct();
        var prices = await _quotes.GetPricesAsync(tickers);

        var result = assets.Select(a =>
        {
            prices.TryGetValue(a.Ticker, out var currentPrice);
            var price = (double)currentPrice;
            var pnl = (price - a.AvgBuyPrice) * a.Quantity;
            var pnlPercent = a.AvgBuyPrice > 0
                ? (price - a.AvgBuyPrice) / a.AvgBuyPrice * 100
                : 0;

            return new
            {
                id = a.Id,
                type = a.Type,
                ticker = a.Ticker,
                name = a.Name,
                quantity = a.Quantity,
                avgBuyPrice = a.AvgBuyPrice,
                totalInvested = a.TotalInvested,
                currency = a.Currency,
                buyDate = a.BuyDate,
                sector = a.Sector,
                dividendYield = a.DividendYield,
                currentPrice = Math.Round(price, 2),
                pnl = Math.Round(pnl, 2),
                pnlPercent = Math.Round(pnlPercent, 2),
                totalValue = Math.Round(price * a.Quantity, 2)
            };
        });

        return Ok(result);
    }

    // POST /api/v1/portfolios/{portfolioId}/assets
    [HttpPost]
    public async Task<IActionResult> Create(string portfolioId, [FromBody] CreateAssetDto dto)
    {
        var portfolio = await _portfolios
            .Find(p => p.Id == portfolioId && p.UserId == UserId)
            .FirstOrDefaultAsync();

        if (portfolio == null)
            return NotFound(new { error = "Портфель не найден" });

        var asset = new Asset
        {
            PortfolioId = portfolioId,
            UserId = UserId,
            Type = dto.Type,
            Ticker = dto.Ticker.ToUpper(),
            Name = dto.Name,
            Quantity = dto.Quantity,
            AvgBuyPrice = dto.AvgBuyPrice,
            TotalInvested = Math.Round(dto.AvgBuyPrice * dto.Quantity, 2),
            Currency = dto.Currency,
            BuyDate = dto.BuyDate,
            Sector = dto.Sector,
            Exchange = dto.Exchange,
            Notes = dto.Notes,
            // Акции
            DividendYield = dto.DividendYield,
            DividendFreq = dto.DividendFreq,
            NextDividendDate = dto.NextDividendDate,
            // Облигации
            CouponRate = dto.CouponRate,
            CouponFreqPerYear = dto.CouponFreqPerYear,
            MaturityDate = dto.MaturityDate,
            FaceValue = dto.FaceValue,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _assets.InsertOneAsync(asset);
        return StatusCode(201, asset);
    }

    // GET /api/v1/portfolios/{portfolioId}/assets/{assetId}
    [HttpGet("{assetId}")]
    public async Task<IActionResult> GetById(string portfolioId, string assetId)
    {
        var asset = await _assets
            .Find(a => a.Id == assetId && a.PortfolioId == portfolioId && a.UserId == UserId)
            .FirstOrDefaultAsync();

        if (asset == null)
            return NotFound(new { error = "Актив не найден" });

        var price = await _quotes.GetPriceAsync(asset.Ticker);
        var currentPrice = (double)(price ?? 0);
        var pnl = (currentPrice - asset.AvgBuyPrice) * asset.Quantity;
        var pnlPercent = asset.AvgBuyPrice > 0
            ? (currentPrice - asset.AvgBuyPrice) / asset.AvgBuyPrice * 100
            : 0;

        return Ok(new
        {
            id = asset.Id,
            type = asset.Type,
            ticker = asset.Ticker,
            name = asset.Name,
            quantity = asset.Quantity,
            avgBuyPrice = asset.AvgBuyPrice,
            totalInvested = asset.TotalInvested,
            currency = asset.Currency,
            sector = asset.Sector,
            dividendYield = asset.DividendYield,
            couponRate = asset.CouponRate,
            maturityDate = asset.MaturityDate,
            currentPrice = Math.Round(currentPrice, 2),
            pnl = Math.Round(pnl, 2),
            pnlPercent = Math.Round(pnlPercent, 2),
            totalValue = Math.Round(currentPrice * asset.Quantity, 2)
        });
    }

    // PUT /api/v1/portfolios/{portfolioId}/assets/{assetId}
    [HttpPut("{assetId}")]
    public async Task<IActionResult> Update(string portfolioId, string assetId, [FromBody] CreateAssetDto dto)
    {
        var update = Builders<Asset>.Update
            .Set(a => a.Ticker, dto.Ticker.ToUpper())
            .Set(a => a.Name, dto.Name)
            .Set(a => a.Quantity, dto.Quantity)
            .Set(a => a.AvgBuyPrice, dto.AvgBuyPrice)
            .Set(a => a.TotalInvested, Math.Round(dto.AvgBuyPrice * dto.Quantity, 2))
            .Set(a => a.Currency, dto.Currency)
            .Set(a => a.Sector, dto.Sector)
            .Set(a => a.Notes, dto.Notes)
            .Set(a => a.DividendYield, dto.DividendYield)
            .Set(a => a.DividendFreq, dto.DividendFreq)
            .Set(a => a.CouponRate, dto.CouponRate)
            .Set(a => a.CouponFreqPerYear, dto.CouponFreqPerYear)
            .Set(a => a.MaturityDate, dto.MaturityDate)
            .Set(a => a.FaceValue, dto.FaceValue)
            .Set(a => a.UpdatedAt, DateTime.UtcNow);

        var result = await _assets.UpdateOneAsync(
            a => a.Id == assetId && a.PortfolioId == portfolioId && a.UserId == UserId,
            update);

        if (result.MatchedCount == 0)
            return NotFound(new { error = "Актив не найден" });

        return Ok(new { message = "Актив обновлён" });
    }

    // DELETE /api/v1/portfolios/{portfolioId}/assets/{assetId}
    [HttpDelete("{assetId}")]
    public async Task<IActionResult> Delete(string portfolioId, string assetId)
    {
        var result = await _assets.DeleteOneAsync(
            a => a.Id == assetId && a.PortfolioId == portfolioId && a.UserId == UserId);

        if (result.DeletedCount == 0)
            return NotFound(new { error = "Актив не найден" });

        return Ok(new { message = "Актив удалён" });
    }
}

public class CreateAssetDto
{
    public string Ticker { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public double Quantity { get; set; }
    public double AvgBuyPrice { get; set; }
    public string Currency { get; set; }
    public DateTime BuyDate { get; set; }
    public string Sector { get; set; }
    public string? Exchange { get; set; }
    public string? Notes { get; set; }
    // Акции
    public double? DividendYield { get; set; }
    public string? DividendFreq { get; set; }
    public DateTime? NextDividendDate { get; set; }
    // Облигации
    public double? CouponRate { get; set; }
    public int? CouponFreqPerYear { get; set; }
    public DateTime? MaturityDate { get; set; }
    public double? FaceValue { get; set; }
}
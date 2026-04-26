using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Security.Claims;

[ApiController]
[Route("api/v1/portfolios")]
[Authorize]
public class PortfolioController : ControllerBase
{
    private readonly IMongoCollection<Portfolio> _portfolios;
    private readonly IMongoCollection<Asset> _assets;
    private readonly QuotesService _quotes;

    public PortfolioController(IMongoDatabase db, QuotesService quotes)
    {
        _portfolios = db.GetCollection<Portfolio>("Portfolios");
        _assets = db.GetCollection<Asset>("Assets");
        _quotes = quotes;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // GET /api/v1/portfolios
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var portfolios = await _portfolios
            .Find(p => p.UserId == UserId)
            .ToListAsync();

        return Ok(portfolios);
    }

    // POST /api/v1/portfolios
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePortfolioDto dto)
    {
        var portfolio = new Portfolio
        {
            UserId = UserId,
            Name = dto.Name,
            Currency = dto.Currency,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _portfolios.InsertOneAsync(portfolio);
        return StatusCode(201, portfolio);
    }

    // GET /api/v1/portfolios/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var portfolio = await _portfolios
            .Find(p => p.Id == id && p.UserId == UserId)
            .FirstOrDefaultAsync();

        if (portfolio == null)
            return NotFound(new { error = "Портфель не найден" });

        var assets = await _assets
            .Find(a => a.PortfolioId == id)
            .ToListAsync();

        var tickers = assets.Select(a => a.Ticker).Distinct();
        var prices = await _quotes.GetPricesAsync(tickers);

        double totalValue = 0;
        double totalInvested = 0;
        double annualDividends = 0;

        foreach (var asset in assets)
        {
            totalInvested += asset.TotalInvested;
            if (prices.TryGetValue(asset.Ticker, out var price))
                totalValue += (double)price * asset.Quantity;
            else
                totalValue += asset.TotalInvested;

            if (asset.Type == "stock" && asset.DividendYield.HasValue)
                annualDividends += asset.TotalInvested * asset.DividendYield.Value / 100;
        }

        var totalPnl = totalValue - totalInvested;
        var totalPnlPercent = totalInvested > 0 ? (totalPnl / totalInvested) * 100 : 0;

        return Ok(new
        {
            id = portfolio.Id,
            name = portfolio.Name,
            currency = portfolio.Currency,
            createdAt = portfolio.CreatedAt,
            summary = new
            {
                totalValue = Math.Round(totalValue, 2),
                totalInvested = Math.Round(totalInvested, 2),
                totalPnl = Math.Round(totalPnl, 2),
                totalPnlPercent = Math.Round(totalPnlPercent, 2),
                annualDividends = Math.Round(annualDividends, 2),
                assetCount = assets.Count
            }
        });
    }

    // PUT /api/v1/portfolios/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] CreatePortfolioDto dto)
    {
        var update = Builders<Portfolio>.Update
            .Set(p => p.Name, dto.Name)
            .Set(p => p.Currency, dto.Currency)
            .Set(p => p.UpdatedAt, DateTime.UtcNow);

        var result = await _portfolios.UpdateOneAsync(
            p => p.Id == id && p.UserId == UserId, update);

        if (result.MatchedCount == 0)
            return NotFound(new { error = "Портфель не найден" });

        return Ok(new { message = "Портфель обновлён" });
    }

    // DELETE /api/v1/portfolios/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await _portfolios.DeleteOneAsync(
            p => p.Id == id && p.UserId == UserId);

        if (result.DeletedCount == 0)
            return NotFound(new { error = "Портфель не найден" });

        // Удаляем все активы портфеля
        await _assets.DeleteManyAsync(a => a.PortfolioId == id);

        return Ok(new { message = "Портфель удалён" });
    }
}

public class CreatePortfolioDto
{
    public string Name { get; set; }
    public string Currency { get; set; }
}
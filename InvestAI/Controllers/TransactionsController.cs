using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Security.Claims;

[ApiController]
[Route("api/v1/assets/{assetId}/transactions")]
[Authorize]
public class TransactionsController : ControllerBase
{
    private readonly IMongoCollection<Transaction> _transactions;
    private readonly IMongoCollection<Asset> _assets;

    public TransactionsController(IMongoDatabase db)
    {
        _transactions = db.GetCollection<Transaction>("Transactions");
        _assets = db.GetCollection<Asset>("Assets");
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // GET /api/v1/assets/{assetId}/transactions
    [HttpGet]
    public async Task<IActionResult> GetAll(string assetId)
    {
        var asset = await _assets
            .Find(a => a.Id == assetId && a.UserId == UserId)
            .FirstOrDefaultAsync();

        if (asset == null)
            return NotFound(new { error = "Актив не найден" });

        var transactions = await _transactions
            .Find(t => t.AssetId == assetId)
            .SortByDescending(t => t.Date)
            .ToListAsync();

        return Ok(transactions);
    }

    // POST /api/v1/assets/{assetId}/transactions
    [HttpPost]
    public async Task<IActionResult> Create(string assetId, [FromBody] CreateTransactionDto dto)
    {
        var asset = await _assets
            .Find(a => a.Id == assetId && a.UserId == UserId)
            .FirstOrDefaultAsync();

        if (asset == null)
            return NotFound(new { error = "Актив не найден" });

        var transaction = new Transaction
        {
            AssetId = assetId,
            PortfolioId = asset.PortfolioId,
            UserId = UserId,
            Type = dto.Type,
            Quantity = dto.Quantity,
            Price = dto.Price,
            TotalAmount = Math.Round(dto.Price * dto.Quantity, 2),
            Currency = asset.Currency,
            Date = dto.Date,
            Notes = dto.Notes,
            CreatedAt = DateTime.UtcNow
        };

        await _transactions.InsertOneAsync(transaction);

        // Пересчитываем среднюю цену покупки если это buy
        if (dto.Type == "buy")
            await RecalculateAvgPrice(asset, dto.Quantity, dto.Price);

        return StatusCode(201, transaction);
    }

    // DELETE /api/v1/assets/{assetId}/transactions/{txId}
    [HttpDelete("{txId}")]
    public async Task<IActionResult> Delete(string assetId, string txId)
    {
        var result = await _transactions.DeleteOneAsync(
            t => t.Id == txId && t.AssetId == assetId && t.UserId == UserId);

        if (result.DeletedCount == 0)
            return NotFound(new { error = "Транзакция не найдена" });

        return Ok(new { message = "Транзакция удалена" });
    }

    private async Task RecalculateAvgPrice(Asset asset, double newQty, double newPrice)
    {
        var newTotalQty = asset.Quantity + newQty;
        var newAvgPrice = ((asset.AvgBuyPrice * asset.Quantity) + (newPrice * newQty)) / newTotalQty;

        var update = Builders<Asset>.Update
            .Set(a => a.Quantity, newTotalQty)
            .Set(a => a.AvgBuyPrice, Math.Round(newAvgPrice, 4))
            .Set(a => a.TotalInvested, Math.Round(newAvgPrice * newTotalQty, 2))
            .Set(a => a.UpdatedAt, DateTime.UtcNow);

        await _assets.UpdateOneAsync(a => a.Id == asset.Id, update);
    }
}

public class CreateTransactionDto
{
    public string Type { get; set; }      // "buy" | "sell" | "dividend" | "coupon"
    public double Quantity { get; set; }
    public double Price { get; set; }
    public DateTime Date { get; set; }
    public string? Notes { get; set; }
}
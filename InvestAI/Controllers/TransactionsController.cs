using InvestAI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Security.Claims;

[Authorize]
public class TransactionsController : Controller
{
    private readonly IMongoCollection<Transaction> _transactions;
    private readonly IMongoCollection<Asset> _assets;

    public TransactionsController(IMongoDatabase db)
    {
        _transactions = db.GetCollection<Transaction>("Transactions");
        _assets = db.GetCollection<Asset>("Assets");
    }

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // POST /Transactions/Create
    [HttpPost]
    public async Task<IActionResult> Create(
        string assetId, string type,
        double quantity, double price, DateTime date)
    {
        var userId = GetUserId();
        var asset = await _assets
            .Find(a => a.Id == assetId && a.UserId == userId)
            .FirstOrDefaultAsync();

        if (asset == null) return NotFound();

        var tx = new Transaction
        {
            AssetId = assetId,
            PortfolioId = asset.PortfolioId,
            UserId = userId,
            Type = type,
            Quantity = quantity,
            Price = price,
            TotalAmount = price * quantity,
            Currency = asset.Currency,
            Date = date,
            CreatedAt = DateTime.UtcNow
        };
        await _transactions.InsertOneAsync(tx);

        // Пересчитать среднюю цену если buy или sell
        if (type == "buy" || type == "sell")
        {
            var qty = type == "buy" ? quantity : -quantity;
            var newTotal = asset.TotalInvested + (price * qty);
            var newQty = asset.Quantity + qty;

            await _assets.UpdateOneAsync(
                a => a.Id == assetId,
                Builders<Asset>.Update
                    .Set(a => a.AvgBuyPrice, newQty > 0 ? newTotal / newQty : 0)
                    .Set(a => a.TotalInvested, newTotal)
                    .Set(a => a.Quantity, newQty)
                    .Set(a => a.UpdatedAt, DateTime.UtcNow));
        }

        return RedirectToAction("Dashboard", "Portfolio");
    }

    // GET /Transactions/History/{assetId}
    public async Task<IActionResult> History(string assetId)
    {
        var userId = GetUserId();
        var txs = await _transactions
            .Find(t => t.AssetId == assetId && t.UserId == userId)
            .SortByDescending(t => t.Date)
            .ToListAsync();

        return View(txs);
    }

    // POST /Transactions/Delete/{id}
    [HttpPost]
    public async Task<IActionResult> Delete(string id)
    {
        var userId = GetUserId();
        await _transactions.DeleteOneAsync(
            t => t.Id == id && t.UserId == userId);

        return RedirectToAction("Dashboard", "Portfolio");
    }
}
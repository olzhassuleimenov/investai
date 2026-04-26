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

    // POST /Transactions/Create
    [HttpPost]
    public async Task<IActionResult> Create(
        string assetId, string type,
        double quantity, double price,
        DateTime date, string? notes)
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
            TotalAmount = Math.Round(price * quantity, 2),
            Currency = asset.Currency,
            Date = date,
            Notes = notes,
            CreatedAt = DateTime.UtcNow
        };
        await _transactions.InsertOneAsync(tx);

        if (type == "buy" || type == "sell")
        {
            var qty = type == "buy" ? quantity : -quantity;
            var newQty = asset.Quantity + qty;
            var newTotal = asset.TotalInvested + (price * qty);

            await _assets.UpdateOneAsync(
                a => a.Id == assetId,
                Builders<Asset>.Update
                    .Set(a => a.Quantity, newQty)
                    .Set(a => a.AvgBuyPrice, newQty > 0 ? Math.Round(newTotal / newQty, 4) : 0)
                    .Set(a => a.TotalInvested, Math.Round(newTotal, 2))
                    .Set(a => a.UpdatedAt, DateTime.UtcNow));
        }

        return RedirectToAction("Dashboard", "Portfolio");
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
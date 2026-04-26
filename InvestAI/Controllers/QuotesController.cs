using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize]
public class QuotesController : Controller
{
    private readonly QuotesService _quotes;

    public QuotesController(QuotesService quotes)
    {
        _quotes = quotes;
    }

    // GET /Quotes/Price?ticker=AAPL
    public async Task<IActionResult> Price(string ticker)
    {
        var price = await _quotes.GetPriceAsync(ticker);
        return Json(new { ticker, price });
    }
}
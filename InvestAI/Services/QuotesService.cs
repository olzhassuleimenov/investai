using System.Text.Json;

public class QuotesService
{
    private readonly HttpClient _http;
    private readonly Dictionary<string, (decimal Price, DateTime CachedAt)> _cache = new();
    private const int CacheMinutes = 15;

    public QuotesService(HttpClient http)
    {
        _http = http;
    }

    public async Task<decimal?> GetPriceAsync(string ticker)
    {
        if (_cache.TryGetValue(ticker, out var cached))
            if ((DateTime.UtcNow - cached.CachedAt).TotalMinutes < CacheMinutes)
                return cached.Price;

        var price = IsRussianTicker(ticker)
            ? await FetchMoexAsync(ticker)
            : await FetchYahooAsync(ticker);

        if (price.HasValue)
            _cache[ticker] = (price.Value, DateTime.UtcNow);

        return price;
    }

    public async Task<Dictionary<string, decimal>> GetPricesAsync(IEnumerable<string> tickers)
    {
        var result = new Dictionary<string, decimal>();
        foreach (var ticker in tickers)
        {
            var price = await GetPriceAsync(ticker);
            if (price.HasValue)
                result[ticker] = price.Value;
        }
        return result;
    }

    private bool IsRussianTicker(string ticker)
    {
        var moex = new HashSet<string> {
            "SBER","GAZP","LKOH","YNDX","MGNT",
            "ROSN","NVTK","TATN","MTSS","GMKN"
        };
        return ticker.Any(c => c >= 'А' && c <= 'я') || moex.Contains(ticker.ToUpper());
    }

    private async Task<decimal?> FetchMoexAsync(string ticker)
    {
        try
        {
            var url = $"https://iss.moex.com/iss/engines/stock/markets/shares/" +
                      $"boards/TQBR/securities/{ticker}.json?iss.meta=off&iss.only=marketdata";
            var res = await _http.GetAsync(url);
            if (!res.IsSuccessStatusCode) return null;

            var json = await res.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var data = doc.RootElement
                .GetProperty("marketdata")
                .GetProperty("data");

            if (data.GetArrayLength() == 0) return null;
            var row = data[0];
            if (row[12].ValueKind == JsonValueKind.Null) return null;
            return row[12].GetDecimal();
        }
        catch { return null; }
    }

    private async Task<decimal?> FetchYahooAsync(string ticker)
    {
        try
        {
            var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{ticker}?interval=1d&range=1d";
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");
            var res = await _http.GetAsync(url);
            if (!res.IsSuccessStatusCode) return null;

            var json = await res.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            return doc.RootElement
                .GetProperty("chart")
                .GetProperty("result")[0]
                .GetProperty("meta")
                .GetProperty("regularMarketPrice")
                .GetDecimal();
        }
        catch { return null; }
    }
}
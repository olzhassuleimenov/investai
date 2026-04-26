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
        // Проверяем кэш
        if (_cache.TryGetValue(ticker, out var cached))
            if ((DateTime.UtcNow - cached.CachedAt).TotalMinutes < CacheMinutes)
                return cached.Price;

        // Российские тикеры → MOEX, остальные → Yahoo
        var price = IsRussianTicker(ticker)
            ? await FetchMoexPriceAsync(ticker)
            : await FetchYahooPriceAsync(ticker);

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
        // Российские тикеры: кириллица или известные тикеры MOEX
        var moexTickers = new HashSet<string> { "SBER", "GAZP", "LKOH", "YNDX", "MGNT", "ROSN", "NVTK", "TATN", "MTSS", "GMKN" };
        return ticker.Any(c => c >= 'А' && c <= 'я') || moexTickers.Contains(ticker.ToUpper());
    }

    // MOEX API
    private async Task<decimal?> FetchMoexPriceAsync(string ticker)
    {
        try
        {
            var url = $"https://iss.moex.com/iss/engines/stock/markets/shares/boards/TQBR/securities/{ticker}.json?iss.meta=off&iss.only=marketdata";
            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            var data = doc.RootElement
                .GetProperty("marketdata")
                .GetProperty("data");

            if (data.GetArrayLength() == 0) return null;

            var row = data[0];
            // Поле LAST — индекс 12
            if (row[12].ValueKind == JsonValueKind.Null) return null;

            return row[12].GetDecimal();
        }
        catch
        {
            return null;
        }
    }

    // Yahoo Finance API
    private async Task<decimal?> FetchYahooPriceAsync(string ticker)
    {
        try
        {
            var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{ticker}?interval=1d&range=1d";
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");

            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            var price = doc.RootElement
                .GetProperty("chart")
                .GetProperty("result")[0]
                .GetProperty("meta")
                .GetProperty("regularMarketPrice")
                .GetDecimal();

            return price;
        }
        catch
        {
            return null;
        }
    }

    // История цен
    public async Task<List<PricePoint>?> GetHistoryAsync(string ticker, string period)
    {
        try
        {
            // period: 1m | 3m | 1y → Yahoo interval
            var range = period switch
            {
                "1m" => "1mo",
                "3m" => "3mo",
                "1y" => "1y",
                _ => "1mo"
            };

            var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{ticker}?interval=1d&range={range}";
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");

            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            var result = doc.RootElement
                .GetProperty("chart")
                .GetProperty("result")[0];

            var timestamps = result.GetProperty("timestamp").EnumerateArray().ToList();
            var closes = result
                .GetProperty("indicators")
                .GetProperty("quote")[0]
                .GetProperty("close")
                .EnumerateArray()
                .ToList();

            var points = new List<PricePoint>();
            for (int i = 0; i < timestamps.Count; i++)
            {
                if (closes[i].ValueKind == JsonValueKind.Null) continue;
                points.Add(new PricePoint
                {
                    Date = DateTimeOffset.FromUnixTimeSeconds(timestamps[i].GetInt64()).DateTime,
                    Price = closes[i].GetDecimal()
                });
            }

            return points;
        }
        catch
        {
            return null;
        }
    }
}

public class PricePoint
{
    public DateTime Date { get; set; }
    public decimal Price { get; set; }
}
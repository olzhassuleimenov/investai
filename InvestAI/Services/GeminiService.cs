using System.Text.Json;

public class GeminiService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private const string Url =
        "https://generativelanguage.googleapis.com/v1beta/models/" +
        "gemini-1.5-flash:generateContent?key={0}";

    public GeminiService(IConfiguration config, HttpClient http)
    {
        _http = http;
        _apiKey = config["Gemini:ApiKey"];
    }

    public async Task<string> AskAsync(string prompt)
    {
        var url = string.Format(Url, _apiKey);
        var body = new
        {
            contents = new[]
            {
                new
                {
                    role  = "user",
                    parts = new[] { new { text = prompt } }
                }
            },
            generationConfig = new
            {
                temperature = 0.7,
                maxOutputTokens = 2048
            }
        };

        var res = await _http.PostAsJsonAsync(url, body);

        if ((int)res.StatusCode == 429)
            throw new Exception("Превышен лимит Gemini API. Попробуйте позже.");

        res.EnsureSuccessStatusCode();

        var data = await res.Content.ReadFromJsonAsync<JsonElement>();
        return data
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString()!;
    }
}
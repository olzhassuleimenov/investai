using InvestAI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Security.Claims;
using System.Text.Json;

[Authorize]
public class ChatController : Controller
{
    private readonly IMongoCollection<Chat> _chats;
    private readonly IMongoCollection<Message> _messages;
    private readonly IMongoCollection<Asset> _assets;
    private readonly IMongoCollection<Portfolio> _portfolios;
    private readonly GeminiService _gemini;

    public ChatController(IMongoDatabase db, GeminiService gemini)
    {
        _chats = db.GetCollection<Chat>("Chats");
        _messages = db.GetCollection<Message>("Messages");
        _assets = db.GetCollection<Asset>("Assets");
        _portfolios = db.GetCollection<Portfolio>("Portfolios");
        _gemini = gemini;
    }

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // POST /Chat/Create
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateChatRequest req)
    {
        var userId = GetUserId();
        var portfolio = await _portfolios
            .Find(p => p.UserId == userId)
            .FirstOrDefaultAsync();

        var chat = new Chat
        {
            UserId = userId,
            PortfolioId = portfolio.Id,
            Title = string.IsNullOrEmpty(req.Title) ? "Новый чат" : req.Title,
            MessageCount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _chats.InsertOneAsync(chat);

        return Json(new { id = chat.Id, title = chat.Title });
    }

    // GET /Chat/Messages/{chatId}
    public async Task<IActionResult> Messages(string chatId)
    {
        var msgs = await _messages
            .Find(m => m.ChatId == chatId)
            .SortBy(m => m.CreatedAt)
            .ToListAsync();

        return Json(msgs);
    }

    // POST /Chat/SendMessage
    [HttpPost]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest req)
    {
        var userId = GetUserId();

        // 1. Сохранить сообщение пользователя
        var userMsg = new Message
        {
            ChatId = req.ChatId,
            UserId = userId,
            Role = "user",
            Text = req.Text,
            HasViz = false,
            CreatedAt = DateTime.UtcNow
        };
        await _messages.InsertOneAsync(userMsg);

        // 2. Загрузить активы портфеля
        var assets = await _assets
            .Find(a => a.UserId == userId)
            .ToListAsync();

        // 3. Собрать промпт
        var portfolioJson = JsonSerializer.Serialize(assets);
        var prompt = $"""
            Ты — финансовый ИИ-ассистент для частных инвесторов. Отвечай только на русском.
            Анализируй исключительно данные переданного портфеля.
            Если вопрос не про инвестиции — вежливо откажи.

            Портфель пользователя (JSON):
            {portfolioJson}

            Вопрос пользователя:
            {req.Text}
            """;

        // 4. Вызвать Gemini
        string aiText;
        try
        {
            aiText = await _gemini.AskAsync(prompt);
        }
        catch (Exception ex)
        {
            aiText = ex.Message;
        }

        // 5. Сохранить ответ ИИ
        var aiMsg = new Message
        {
            ChatId = req.ChatId,
            UserId = userId,
            Role = "assistant",
            Text = aiText,
            HasViz = aiText.Contains("%") || aiText.Contains("$") || aiText.Contains("₽"),
            CreatedAt = DateTime.UtcNow
        };
        await _messages.InsertOneAsync(aiMsg);

        // 6. Обновить счётчик чата
        await _chats.UpdateOneAsync(
            c => c.Id == req.ChatId,
            Builders<Chat>.Update
                .Inc(c => c.MessageCount, 2)
                .Set(c => c.UpdatedAt, DateTime.UtcNow));

        return Json(new { userMessage = userMsg, aiMessage = aiMsg });
    }

    // POST /Chat/Delete/{chatId}
    [HttpPost]
    public async Task<IActionResult> Delete(string chatId)
    {
        await _chats.DeleteOneAsync(c => c.Id == chatId);
        await _messages.DeleteManyAsync(m => m.ChatId == chatId);
        return Json(new { success = true });
    }
}

public class CreateChatRequest
{
    public string Title { get; set; }
}

public class SendMessageRequest
{
    public string ChatId { get; set; }
    public string Text { get; set; }
}
public class ChatViewModel
{
    public string ChatId { get; set; }
    public string Title { get; set; }
    public List<Message> Messages { get; set; } = new();
}
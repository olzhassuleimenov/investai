public class DashboardViewModel
{
    public Portfolio Portfolio { get; set; }
    public List<AssetViewModel> Assets { get; set; } = new();
    public decimal TotalValue { get; set; }
    public decimal TotalInvested { get; set; }
    public decimal TotalPnl { get; set; }
    public decimal TotalPnlPercent { get; set; }
    public decimal AnnualDividends { get; set; }
    public List<Chat> Chats { get; set; } = new();
    public string? ActiveChatId { get; set; }
}
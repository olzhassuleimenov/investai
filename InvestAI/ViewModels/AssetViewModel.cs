public class AssetViewModel
{
    public string Id { get; set; }
    public string PortfolioId { get; set; }
    public string UserId { get; set; }
    public string Type { get; set; }
    public string Ticker { get; set; }
    public string Name { get; set; }
    public double Quantity { get; set; }
    public double AvgBuyPrice { get; set; }
    public double TotalInvested { get; set; }
    public string Currency { get; set; }
    public DateTime BuyDate { get; set; }
    public string? Sector { get; set; }
    public string? Exchange { get; set; }
    public double? DividendYield { get; set; }
    public string? DividendFreq { get; set; }
    public DateTime? NextDividendDate { get; set; }
    public double? CouponRate { get; set; }
    public int? CouponFreqPerYear { get; set; }
    public DateTime? MaturityDate { get; set; }
    public double? FaceValue { get; set; }
    public decimal? CurrentPrice { get; set; }
    public decimal? Pnl { get; set; }
    public decimal? PnlPercent { get; set; }
    public decimal TotalValue { get; set; }
}
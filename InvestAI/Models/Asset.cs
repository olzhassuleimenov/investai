using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

public class Asset
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string PortfolioId { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; }

    public string Type { get; set; }       // "stock" | "bond"
    public string Ticker { get; set; }
    public string Name { get; set; }
    public double Quantity { get; set; }
    public double AvgBuyPrice { get; set; }
    public double TotalInvested { get; set; }
    public string Currency { get; set; }
    public DateTime BuyDate { get; set; }
    public string Sector { get; set; }
    public string Exchange { get; set; }
    public string? Notes { get; set; }

    // Только для акций
    public double? DividendYield { get; set; }
    public string? DividendFreq { get; set; }
    public DateTime? NextDividendDate { get; set; }

    // Только для облигаций
    public double? CouponRate { get; set; }
    public int? CouponFreqPerYear { get; set; }
    public DateTime? MaturityDate { get; set; }
    public double? FaceValue { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
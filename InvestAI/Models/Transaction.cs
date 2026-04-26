using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

public class Transaction
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string AssetId { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string PortfolioId { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; }

    public string Type { get; set; }       // "buy" | "sell" | "dividend" | "coupon"
    public double Quantity { get; set; }
    public double Price { get; set; }
    public double TotalAmount { get; set; }
    public string Currency { get; set; }
    public DateTime Date { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
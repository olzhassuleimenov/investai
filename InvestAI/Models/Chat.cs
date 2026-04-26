using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

public class Chat
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string PortfolioId { get; set; }

    public string Title { get; set; }
    public int MessageCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
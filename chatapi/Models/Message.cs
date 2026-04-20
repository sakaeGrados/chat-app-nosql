using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace chatapi.Models
{
    public class Message
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        [Required]
        [BsonElement("senderId")]
        public string SenderId { get; set; } = null!;

        [BsonElement("receiverId")]
        public string? ReceiverId { get; set; } // null for global chat

        [Required]
        [BsonElement("content")]
        public string Content { get; set; } = null!;

        [Required]
        [BsonElement("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}

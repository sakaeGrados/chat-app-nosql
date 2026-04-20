using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace chatapi.Models
{
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        [Required]
        [BsonElement("username")]
        public string Username { get; set; } = null!;

        [Required]
        [BsonElement("phoneNumber")]
        public string PhoneNumber { get; set; } = null!;

        [Required]
        [BsonElement("passwordHash")]
        public string PasswordHash { get; set; } = null!;

        [BsonElement("status")]
        public string Status { get; set; } = "online"; // online, away, dnd, invisible

        [BsonElement("isConnected")]
        public bool IsConnected { get; set; } = false; // Track if user is online (for Discord-like behavior)

        [BsonElement("profilePhoto")]
        public string? ProfilePhoto { get; set; } // Base64 encoded image or URL
    }
}
using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace chatapi.Models
{
    public class Group
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = null!;

        [Required]
        [BsonElement("CreatorId")]
        [BsonIgnoreIfNull]
        public string? CreatorId { get; set; }
    }
}
using chatapi.Config;
using chatapi.DTO;
using chatapi.Models;
using MongoDB.Driver;
using StackExchange.Redis;
using MongoDB.Bson;

namespace chatapi.Services;

public class ChatService
{
    private readonly MongoContext _context;
    private readonly IDatabase _redis;

    public ChatService(MongoContext context, IConnectionMultiplexer redis)
    {
        _context = context;
        _redis = redis.GetDatabase();
    }

    public async Task SendGlobalMessageAsync(string userId, SendMessageDto dto)
    {
        var message = new Message
        {
            SenderId = userId,
            Content = dto.Content,
            Timestamp = DateTime.UtcNow
        };
        await _context.Messages.InsertOneAsync(message);
    }

    public async Task<List<MessageDto>> GetGlobalMessagesAsync(int count = 50)
{
    var messages = await _context.Messages.Find(m => m.ReceiverId == null)
        .SortByDescending(m => m.Timestamp)
        .Limit(count)
        .ToListAsync();

    var userIds = messages.Select(m => m.SenderId).Distinct().ToList();

    // 👇 validación sin ObjectId
    var validUserIds = userIds
        .Where(id => System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-fA-F0-9]{24}$"))
        .ToList();

    var users = await _context.Users
        .Find(u => validUserIds.Contains(u.Id))
        .ToListAsync();

    var userDict = users.ToDictionary(u => u.Id, u => u.Username);

    return messages.Select(m => new MessageDto
    {
        Id = m.Id,
        UserId = m.SenderId,
        Username = userDict.GetValueOrDefault(m.SenderId, "Unknown"),
        Content = m.Content,
        Timestamp = m.Timestamp
    }).ToList();
}

    public async Task SendPrivateMessageAsync(string senderId, SendPrivateMessageDto dto)
    {
        var message = new Message
        {
            SenderId = senderId,
            ReceiverId = dto.ReceiverId,
            Content = dto.Content,
            Timestamp = DateTime.UtcNow
        };
        await _context.Messages.InsertOneAsync(message);
    }

    public async Task<List<MessageDto>> GetPrivateConversationAsync(string userId1, string userId2)
    {
        var filter = Builders<Message>.Filter.Or(
            Builders<Message>.Filter.And(
                Builders<Message>.Filter.Eq(m => m.SenderId, userId1),
                Builders<Message>.Filter.Eq(m => m.ReceiverId, userId2)
            ),
            Builders<Message>.Filter.And(
                Builders<Message>.Filter.Eq(m => m.SenderId, userId2),
                Builders<Message>.Filter.Eq(m => m.ReceiverId, userId1)
            )
        );

        var messages = await _context.Messages.Find(filter)
            .SortBy(m => m.Timestamp)
            .ToListAsync();

        var userIds = new List<string> { userId1, userId2 };
        var users = await _context.Users.Find(u => userIds.Contains(u.Id)).ToListAsync();
        var userDict = users.ToDictionary(u => u.Id, u => u.Username);

        return messages.Select(m => new MessageDto
        {
            Id = m.Id,
            UserId = m.SenderId,
            Username = userDict.GetValueOrDefault(m.SenderId, "Unknown"),
            Content = m.Content,
            Timestamp = m.Timestamp
        }).ToList();
    }
}
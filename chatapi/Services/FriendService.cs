using MongoDB.Driver;
using chatapi.Config;
using chatapi.Models;
using StackExchange.Redis;

namespace chatapi.Services;

public class FriendService
{
    private readonly MongoContext _context;
    private readonly IConnectionMultiplexer _redis;
    private const string FriendsCacheKeyPrefix = "friends:";
    private const int CacheTTL = 3600; // 1 hour

    public FriendService(MongoContext context, IConnectionMultiplexer redis)
    {
        _context = context;
        _redis = redis;
    }

    /// <summary>
    /// Send a friend request (creates pending request)
    /// </summary>
    public async Task<bool> AddFriendAsync(string userId, string friendId)
    {
        // Validar que no sea el mismo usuario
        if (userId == friendId)
            throw new InvalidOperationException("Cannot add yourself as a friend");

        // Validar que el usuario amigo existe
        var friendExists = await _context.Users
            .Find(u => u.Id == friendId)
            .FirstOrDefaultAsync();

        if (friendExists == null)
            throw new InvalidOperationException("Friend user does not exist");

        // Verificar si ya existe relación (en cualquier estado)
        var existingFriend = await _context.Friends
            .Find(f => (f.UserId == userId && f.FriendId == friendId) ||
                       (f.UserId == friendId && f.FriendId == userId))
            .FirstOrDefaultAsync();

        if (existingFriend != null)
            throw new InvalidOperationException("Already have a request with this user");

        // Crear solicitud pendiente (solo en una dirección)
        var friendRequest = new Friend
        {
            UserId = userId,
            FriendId = friendId,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        await _context.Friends.InsertOneAsync(friendRequest);

        // Invalidate cache for both users
        InvalidateFriendsCache(userId);
        InvalidateFriendsCache(friendId);

        return true;
    }

    /// <summary>
    /// Accept a friend request
    /// </summary>
    public async Task<bool> AcceptFriendRequestAsync(string userId, string friendId)
    {
        // Find the pending request (friendId sent request to userId)
        var request = await _context.Friends
            .Find(f => f.UserId == friendId && f.FriendId == userId && f.Status == "pending")
            .FirstOrDefaultAsync();

        if (request == null)
            throw new InvalidOperationException("Friend request not found");

        // Update the original request to accepted
        await _context.Friends.UpdateOneAsync(
            f => f.Id == request.Id,
            Builders<Friend>.Update.Set(f => f.Status, "accepted")
        );

        // Create reciprocal friendship (userId -> friendId as accepted)
        var reciprocal = new Friend
        {
            UserId = userId,
            FriendId = friendId,
            Status = "accepted",
            CreatedAt = DateTime.UtcNow
        };

        await _context.Friends.InsertOneAsync(reciprocal);

        // Invalidate cache for both users
        InvalidateFriendsCache(userId);
        InvalidateFriendsCache(friendId);

        return true;
    }

    /// <summary>
    /// Reject a friend request
    /// </summary>
    public async Task<bool> RejectFriendRequestAsync(string userId, string friendId)
    {
        // Delete the pending request (friendId sent request to userId)
        var result = await _context.Friends.DeleteOneAsync(
            f => f.UserId == friendId && f.FriendId == userId && f.Status == "pending"
        );

        if (result.DeletedCount == 0)
            throw new InvalidOperationException("Friend request not found");

        // Invalidate cache for both users
        InvalidateFriendsCache(userId);
        InvalidateFriendsCache(friendId);

        return true;
    }

    /// <summary>
    /// Cancel a sent friend request
    /// </summary>
    public async Task<bool> CancelFriendRequestAsync(string userId, string friendId)
    {
        // Delete the pending request (userId sent request to friendId)
        var result = await _context.Friends.DeleteOneAsync(
            f => f.UserId == userId && f.FriendId == friendId && f.Status == "pending"
        );

        if (result.DeletedCount == 0)
            throw new InvalidOperationException("Friend request not found");

        // Invalidate cache for both users
        InvalidateFriendsCache(userId);
        InvalidateFriendsCache(friendId);

        return true;
    }

    /// <summary>
    /// Get pending friend requests received by user
    /// </summary>
    public async Task<List<dynamic>> GetFriendRequestsAsync(string userId)
    {
        var requests = await _context.Friends
            .Find(f => f.FriendId == userId && f.Status == "pending")
            .ToListAsync();

        var senderIds = requests.Select(f => f.UserId).ToList();

        if (senderIds.Count == 0)
            return new();

        var users = await _context.Users
            .Find(u => senderIds.Contains(u.Id))
            .ToListAsync();

        return users.Select(u => new
        {
            id = u.Id,
            username = u.Username,
            phoneNumber = u.PhoneNumber,
            status = u.Status ?? "online"
        }).Cast<dynamic>().ToList();
    }

    /// <summary>
    /// Get pending friend requests sent by user
    /// </summary>
    public async Task<List<dynamic>> GetSentFriendRequestsAsync(string userId)
    {
        var requests = await _context.Friends
            .Find(f => f.UserId == userId && f.Status == "pending")
            .ToListAsync();

        var receiverIds = requests.Select(f => f.FriendId).ToList();

        if (receiverIds.Count == 0)
            return new();

        var users = await _context.Users
            .Find(u => receiverIds.Contains(u.Id))
            .ToListAsync();

        return users.Select(u => new
        {
            id = u.Id,
            username = u.Username,
            phoneNumber = u.PhoneNumber,
            status = u.Status ?? "online",
            profilePhoto = u.ProfilePhoto
        }).Cast<dynamic>().ToList();
    }

    /// <summary>
    /// Remove a friend
    /// </summary>
    public async Task<bool> RemoveFriendAsync(string userId, string friendId)
    {
        // Delete both directions of friendship
        var result1 = await _context.Friends.DeleteOneAsync(
            f => f.UserId == userId && f.FriendId == friendId
        );

        var result2 = await _context.Friends.DeleteOneAsync(
            f => f.UserId == friendId && f.FriendId == userId
        );

        if (result1.DeletedCount > 0 || result2.DeletedCount > 0)
        {
            // Invalidate cache for both users
            InvalidateFriendsCache(userId);
            InvalidateFriendsCache(friendId);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Get all friends of a user (bidirectional - works in both directions)
    /// </summary>
    public async Task<List<Friend>> GetFriendsAsync(string userId)
    {
        // Try to get from cache
        var db = _redis.GetDatabase();
        var cacheKey = $"{FriendsCacheKeyPrefix}{userId}";
        var cached = await db.StringGetAsync(cacheKey);

        if (cached.HasValue)
        {
            var json = cached.ToString();
            return System.Text.Json.JsonSerializer.Deserialize<List<Friend>>(json) ?? new();
        }

        // Get from database - search in both directions
        var friends = await _context.Friends
            .Find(f => (f.UserId == userId || f.FriendId == userId) && f.Status == "accepted")
            .ToListAsync();

        // Normalize results - always return FriendId as the other user
        var normalizedFriends = friends.Select(f => new Friend
        {
            Id = f.Id,
            UserId = userId,
            FriendId = f.UserId == userId ? f.FriendId : f.UserId,
            Status = f.Status,
            CreatedAt = f.CreatedAt
        }).ToList();

        // Cache the result
        var serialized = System.Text.Json.JsonSerializer.Serialize(normalizedFriends);
        await db.StringSetAsync(cacheKey, serialized, TimeSpan.FromSeconds(CacheTTL));

        return normalizedFriends;
    }

    /// <summary>
    /// Check if two users are friends
    /// </summary>
    public async Task<bool> AreFriendsAsync(string userId, string friendId)
    {
        var friend = await _context.Friends
            .Find(f => (f.UserId == userId && f.FriendId == friendId) ||
                       (f.UserId == friendId && f.FriendId == userId))
            .FirstOrDefaultAsync();

        return friend != null && friend.Status == "accepted";
    }

    /// <summary>
    /// Get mutual friends between two users
    /// </summary>
    public async Task<List<User>> GetMutualFriendsAsync(string userId, string otherId)
    {
        var userFriends = await GetFriendsAsync(userId);
        var otherFriends = await GetFriendsAsync(otherId);

        var mutualFriendIds = userFriends
            .Select(f => f.FriendId)
            .Intersect(otherFriends.Select(f => f.FriendId))
            .ToList();

        if (mutualFriendIds.Count == 0)
            return new();

        var mutualFriends = await _context.Users
            .Find(u => mutualFriendIds.Contains(u.Id))
            .ToListAsync();

        return mutualFriends;
    }

    /// <summary>
    /// Get friend details including user info
    /// </summary>
    public async Task<List<dynamic>> GetFriendsWithDetailsAsync(string userId)
    {
        var friends = await GetFriendsAsync(userId);
        var friendIds = friends.Select(f => f.FriendId).ToList();

        if (friendIds.Count == 0)
            return new();

        var users = await _context.Users
            .Find(u => friendIds.Contains(u.Id))
            .ToListAsync();

        return users.Select(u => new
        {
            id = u.Id,
            username = u.Username,
            phoneNumber = u.PhoneNumber,
            friendId = u.Id,
            profilePhoto = u.ProfilePhoto,
            status = u.Status ?? "online"
        }).Cast<dynamic>().ToList();
    }

    private void InvalidateFriendsCache(string userId)
    {
        var db = _redis.GetDatabase();
        var cacheKey = $"{FriendsCacheKeyPrefix}{userId}";
        db.KeyDelete(cacheKey);
    }
}

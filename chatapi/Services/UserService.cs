using chatapi.Config;
using chatapi.Models;
using MongoDB.Driver;
using StackExchange.Redis;
using System.Text.Json;

namespace chatapi.Services;

public class UserService
{
    private readonly MongoContext _context;
    private readonly IDatabase _redis;

    public UserService(MongoContext context, IConnectionMultiplexer redis)
    {
        _context = context;
        _redis = redis.GetDatabase();
    }

    /// <summary>
    /// Search users by username or phone number
    /// </summary>
    public async Task<List<User>> SearchUsersAsync(string query)
    {
        var filter = Builders<User>.Filter.Or(
            Builders<User>.Filter.Regex(u => u.Username, new MongoDB.Bson.BsonRegularExpression(query, "i")),
            Builders<User>.Filter.Regex(u => u.PhoneNumber, new MongoDB.Bson.BsonRegularExpression(query, "i"))
        );
        return await _context.Users.Find(filter).ToListAsync();
    }

    /// <summary>
    /// Get user by ID with caching
    /// </summary>
    public async Task<User?> GetUserByIdAsync(string id)
    {
        // Check Redis cache first
        var cachedUser = await _redis.StringGetAsync($"user:{id}");
        if (!cachedUser.IsNull)
        {
            return JsonSerializer.Deserialize<User>((string)cachedUser);
        }

        var user = await _context.Users.Find(u => u.Id == id).FirstOrDefaultAsync();
        if (user != null)
        {
            await _redis.StringSetAsync($"user:{id}", JsonSerializer.Serialize(user), TimeSpan.FromHours(1));
        }
        return user;
    }

    /// <summary>
    /// Get all users with pagination
    /// </summary>
    public async Task<List<User>> GetAllUsersAsync(int limit = 100, int skip = 0)
    {
        return await _context.Users.Find(_ => true)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync();
    }

    /// <summary>
    /// Update user information
    /// </summary>
    public async Task<bool> UpdateUserAsync(string userId, string username, string phoneNumber)
    {
        var filter = Builders<User>.Filter.Eq(u => u.Id, userId);
        var update = Builders<User>.Update
            .Set(u => u.Username, username)
            .Set(u => u.PhoneNumber, phoneNumber);

        var result = await _context.Users.UpdateOneAsync(filter, update);
        
        if (result.ModifiedCount > 0)
        {
            // Invalidate cache
            await _redis.KeyDeleteAsync($"user:{userId}");
        }

        return result.ModifiedCount > 0;
    }

    /// <summary>
    /// Delete user account
    /// </summary>
    public async Task<bool> DeleteUserAsync(string userId)
    {
        var filter = Builders<User>.Filter.Eq(u => u.Id, userId);
        var result = await _context.Users.DeleteOneAsync(filter);

        if (result.DeletedCount > 0)
        {
            // Invalidate cache
            await _redis.KeyDeleteAsync($"user:{userId}");
        }

        return result.DeletedCount > 0;
    }

    /// <summary>
    /// Validate if user exists
    /// </summary>
    public async Task<bool> ValidateUserAsync(string userId)
    {
        var user = await _context.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
        return user != null;
    }

    /// <summary>
    /// Change user password
    /// </summary>
    public async Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword)
    {
        var user = await GetUserByIdAsync(userId);
        if (user == null)
            return false;

        // Verify current password
        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            return false;

        // Hash new password
        var newPasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);

        var filter = Builders<User>.Filter.Eq(u => u.Id, userId);
        var update = Builders<User>.Update.Set(u => u.PasswordHash, newPasswordHash);

        var result = await _context.Users.UpdateOneAsync(filter, update);

        if (result.ModifiedCount > 0)
        {
            // Invalidate cache
            await _redis.KeyDeleteAsync($"user:{userId}");
        }

        return result.ModifiedCount > 0;
    }

    /// <summary>
    /// Update user status (online, away, dnd, invisible)
    /// </summary>
    public async Task<bool> UpdateUserStatusAsync(string userId, string status)
    {
        var filter = Builders<User>.Filter.Eq(u => u.Id, userId);
        var update = Builders<User>.Update.Set(u => u.Status, status);

        var result = await _context.Users.UpdateOneAsync(filter, update);

        if (result.ModifiedCount > 0)
        {
            // Invalidate cache
            await _redis.KeyDeleteAsync($"user:{userId}");
        }

        // MatchedCount > 0 means the user exists; ModifiedCount can be 0 when status is unchanged.
        return result.MatchedCount > 0;
    }

    /// <summary>
    /// Set user connected status (Discord-like behavior)
    /// When user connects: IsConnected = true, Status remains as configured (or becomes online)
    /// When user disconnects: IsConnected = false, Status is preserved for next reconnect
    /// </summary>
    public async Task<bool> SetConnectedAsync(string userId, bool isConnected)
    {
        var filter = Builders<User>.Filter.Eq(u => u.Id, userId);
        
        var updateBuilder = Builders<User>.Update.Set(u => u.IsConnected, isConnected);
        
        // If connecting, also set status to online (unless they had a different status)
        if (isConnected)
        {
            // Get current user to check if status needs reset
            var user = await GetUserByIdAsync(userId);
            if (user != null && (user.Status == "invisible" || !user.IsConnected))
            {
                // If reconnecting from invisible/offline, set to online
                updateBuilder = updateBuilder.Set(u => u.Status, "online");
            }
        }

        var result = await _context.Users.UpdateOneAsync(filter, updateBuilder);

        if (result.ModifiedCount > 0)
        {
            // Invalidate cache
            await _redis.KeyDeleteAsync($"user:{userId}");
        }

        return result.MatchedCount > 0;
    }

    /// <summary>
    /// Update username
    /// </summary>
    public async Task<bool> UpdateUsernameAsync(string userId, string newUsername)
    {
        // Check if username is already taken
        var existingUser = await _context.Users
            .Find(u => u.Username == newUsername && u.Id != userId)
            .FirstOrDefaultAsync();

        if (existingUser != null)
            return false;

        var filter = Builders<User>.Filter.Eq(u => u.Id, userId);
        var update = Builders<User>.Update.Set(u => u.Username, newUsername);

        var result = await _context.Users.UpdateOneAsync(filter, update);

        if (result.ModifiedCount > 0)
        {
            // Invalidate cache
            await _redis.KeyDeleteAsync($"user:{userId}");
        }

        return result.ModifiedCount > 0;
    }

    /// <summary>
    /// Update or delete user profile photo
    /// </summary>
    public async Task<bool> UpdateProfilePhotoAsync(string userId, string? photoData)
    {
        var filter = Builders<User>.Filter.Eq(u => u.Id, userId);
        var update = Builders<User>.Update.Set(u => u.ProfilePhoto, photoData);

        var result = await _context.Users.UpdateOneAsync(filter, update);

        if (result.ModifiedCount > 0)
        {
            // Invalidate cache
            await _redis.KeyDeleteAsync($"user:{userId}");
        }

        return result.ModifiedCount > 0;
    }
}
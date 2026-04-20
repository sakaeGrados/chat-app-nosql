using chatapi.Config;
using chatapi.DTO;
using chatapi.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.AspNetCore.SignalR;
using chatapi.Hubs;

namespace chatapi.Services;

public class GroupService
{
    private readonly MongoContext _context;
    private readonly IHubContext<ChatHub> _hubContext;

    public GroupService(MongoContext context, IHubContext<ChatHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    public async Task<Group> CreateGroupAsync(string creatorId, CreateGroupDto dto)
    {
        var group = new Group
        {
            Name = dto.Name,
            CreatorId = creatorId
        };
        await _context.Groups.InsertOneAsync(group);

        // Add creator as member
        var member = new GroupMember
        {
            GroupId = group.Id,
            UserId = creatorId
        };
        await _context.GroupMembers.InsertOneAsync(member);
        return group;
    }

    public async Task<bool> JoinGroupAsync(string userId, JoinGroupDto dto)
    {
        // Validate group exists
        var group = await _context.Groups.Find(g => g.Id == dto.GroupId).FirstOrDefaultAsync();
        if (group == null) return false;

        // Check if already member
        var existingMember = await _context.GroupMembers.Find(gm => gm.GroupId == dto.GroupId && gm.UserId == userId).FirstOrDefaultAsync();
        if (existingMember != null) return false;

        var member = new GroupMember
        {
            GroupId = dto.GroupId,
            UserId = userId
        };
        await _context.GroupMembers.InsertOneAsync(member);
        return true;
    }

    public async Task SendGroupMessageAsync(string userId, SendGroupMessageDto dto)
    {
        // Validate group exists
        var group = await _context.Groups.Find(g => g.Id == dto.GroupId).FirstOrDefaultAsync();
        if (group == null) throw new Exception("Group not found");

        // Check if user is member
        var member = await _context.GroupMembers.Find(gm => gm.GroupId == dto.GroupId && gm.UserId == userId).FirstOrDefaultAsync();
        if (member == null) throw new Exception("Not a member of this group");

        // Get user information
        var user = await _context.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
        var username = user?.Username ?? "Unknown";

        var message = new GroupMessage
        {
            GroupId = dto.GroupId,
            UserId = userId,
            Content = dto.Content,
            Timestamp = DateTime.UtcNow
        };
        await _context.GroupMessages.InsertOneAsync(message);

        // Notify all clients in the group via SignalR
        await _hubContext.Clients.Group($"group_{dto.GroupId}").SendAsync("ReceiveGroupMessage", new
        {
            Id = message.Id,
            GroupId = dto.GroupId,
            UserId = userId,
            Username = username,
            Content = dto.Content,
            Timestamp = message.Timestamp
        });
    }

    public async Task<List<GroupMessageDto>> GetGroupMessagesAsync(string groupId, int count = 50)
    {
        var messages = await _context.GroupMessages.Find(gm => gm.GroupId == groupId)
            .SortByDescending(gm => gm.Timestamp)
            .Limit(count)
            .ToListAsync();

        var userIds = messages.Select(m => m.UserId).Distinct().ToList();
        var users = await _context.Users.Find(u => userIds.Contains(u.Id)).ToListAsync();
        var userDict = users.ToDictionary(u => u.Id, u => u.Username);

        return messages.Select(gm => new GroupMessageDto
        {
            Id = gm.Id,
            GroupId = gm.GroupId,
            UserId = gm.UserId,
            Username = userDict.GetValueOrDefault(gm.UserId, "Unknown"),
            Content = gm.Content,
            Timestamp = gm.Timestamp
        }).ToList();
    }

    public async Task<List<GroupWithLastMessageDto>> GetUserGroupsAsync(string userId)
    {
        // Get all group memberships for the user
        var memberships = await _context.GroupMembers.Find(gm => gm.UserId == userId).ToListAsync();
        var groupIds = memberships.Select(gm => gm.GroupId).ToList();

        if (groupIds.Count == 0)
            return new List<GroupWithLastMessageDto>();

        // Get the groups
        var groups = await _context.Groups.Find(g => groupIds.Contains(g.Id)).ToListAsync();

        // Get last message for each group
        var result = new List<GroupWithLastMessageDto>();
        
        foreach (var group in groups)
        {
            var lastMessage = await _context.GroupMessages
                .Find(gm => gm.GroupId == group.Id)
                .SortByDescending(gm => gm.Timestamp)
                .FirstOrDefaultAsync();

            var lastMessageUser = lastMessage != null 
                ? await _context.Users.Find(u => u.Id == lastMessage.UserId).FirstOrDefaultAsync()
                : null;

            result.Add(new GroupWithLastMessageDto
            {
                Id = group.Id,
                Name = group.Name,
                LastMessage = lastMessage?.Content,
                LastMessageUsername = lastMessageUser?.Username ?? "Unknown",
                LastMessageTime = lastMessage?.Timestamp
            });
        }

        return result;
    }

    /// <summary>
    /// Get all members of a group
    /// </summary>
    public async Task<List<UserDto>> GetGroupMembersAsync(string groupId)
    {
        // Validate group exists
        var group = await _context.Groups.Find(g => g.Id == groupId).FirstOrDefaultAsync();
        if (group == null) return new List<UserDto>();

        // Get all members
        var memberIds = await _context.GroupMembers.Find(gm => gm.GroupId == groupId).ToListAsync();
        var userIds = memberIds.Select(m => m.UserId).ToList();

        if (userIds.Count == 0)
            return new List<UserDto>();

        var users = await _context.Users.Find(u => userIds.Contains(u.Id)).ToListAsync();
        
        return users.Select(u => new UserDto 
        { 
            Id = u.Id, 
            Username = u.Username, 
            PhoneNumber = u.PhoneNumber 
        }).ToList();
    }

    /// <summary>
    /// Remove a member from a group (only group creator can remove)
    /// </summary>
    public async Task<bool> RemoveGroupMemberAsync(string groupId, string memberId, string currentUserId)
    {
        // Verify current user is group creator
        var group = await _context.Groups.Find(g => g.Id == groupId).FirstOrDefaultAsync();
        if (group == null || group.CreatorId != currentUserId)
            return false;

        // Prevent removing creator themselves
        if (memberId == group.CreatorId)
            return false;

        var filter = Builders<GroupMember>.Filter.And(
            Builders<GroupMember>.Filter.Eq(gm => gm.GroupId, groupId),
            Builders<GroupMember>.Filter.Eq(gm => gm.UserId, memberId)
        );

        var result = await _context.GroupMembers.DeleteOneAsync(filter);
        return result.DeletedCount > 0;
    }

    /// <summary>
    /// Get a group by ID
    /// </summary>
    public async Task<Group?> GetGroupByIdAsync(string groupId)
    {
        return await _context.Groups.Find(g => g.Id == groupId).FirstOrDefaultAsync();
    }

    /// <summary>
    /// Get all groups with search capability
    /// </summary>
    public async Task<List<GroupWithLastMessageDto>> GetAllGroupsAsync(string searchTerm = "")
    {
        List<Group> allGroups = new List<Group>();
        
        try
        {
            // Use projection to get only Id and Name, avoiding CreatorId deserialization issues
            var projection = Builders<Group>.Projection
                .Include(g => g.Id)
                .Include(g => g.Name);

            var groupDocs = await _context.Groups.Find(_ => true)
                .Project<BsonDocument>(projection)
                .ToListAsync();

            foreach (var doc in groupDocs)
            {
                allGroups.Add(new Group
                {
                    Id = doc["_id"].AsObjectId.ToString(),
                    Name = doc["Name"].AsString
                });
            }
        }
        catch (Exception ex)
        {
            // If deserialization fails, log and return empty
            Console.WriteLine($"Error loading groups: {ex.Message}");
            return new List<GroupWithLastMessageDto>();
        }

        // Filter by search term if provided
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var searchLower = searchTerm.ToLower();
            allGroups = allGroups.Where(g => g.Name.ToLower().Contains(searchLower)).ToList();
        }

        // Get last message for each group
        var result = new List<GroupWithLastMessageDto>();
        
        foreach (var group in allGroups)
        {
            var lastMessage = await _context.GroupMessages
                .Find(gm => gm.GroupId == group.Id)
                .SortByDescending(gm => gm.Timestamp)
                .FirstOrDefaultAsync();

            var lastMessageUser = lastMessage != null 
                ? await _context.Users.Find(u => u.Id == lastMessage.UserId).FirstOrDefaultAsync()
                : null;

            // Get member count
            var memberCount = await _context.GroupMembers.CountDocumentsAsync(gm => gm.GroupId == group.Id);

            result.Add(new GroupWithLastMessageDto
            {
                Id = group.Id,
                Name = group.Name,
                LastMessage = lastMessage?.Content,
                LastMessageUsername = lastMessageUser?.Username ?? "Unknown",
                LastMessageTime = lastMessage?.Timestamp,
                MemberCount = (int)memberCount
            });
        }

        return result;
    }
}
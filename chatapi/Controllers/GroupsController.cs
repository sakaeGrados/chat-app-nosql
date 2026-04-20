using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using chatapi.Services;
using chatapi.DTO;
using chatapi.Hubs;
using System.Security.Claims;

namespace chatapi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GroupsController : ControllerBase
{
    private readonly GroupService _groupService;
    private readonly IHubContext<ChatHub> _hubContext;

    public GroupsController(GroupService groupService, IHubContext<ChatHub> hubContext)
    {
        _groupService = groupService;
        _hubContext = hubContext;
    }

    private string GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
               throw new UnauthorizedAccessException("User not found in token");
    }

    /// <summary>
    /// Create a new group (requires authentication)
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest("Group name is required");

        var creatorId = GetCurrentUserId();
        var group = await _groupService.CreateGroupAsync(creatorId, dto);
        
        // Notificar que se creó un nuevo grupo
        await _hubContext.Clients.All.SendAsync("GroupCreated", new
        {
            GroupId = group.Id,
            GroupName = group.Name,
            CreatorId = group.CreatorId
        });

        return Ok(group);
    }

    /// <summary>
    /// Join a group (requires authentication)
    /// </summary>
    [HttpPost("join")]
    public async Task<IActionResult> JoinGroup([FromBody] JoinGroupDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.GroupId))
            return BadRequest("Group ID is required");

        var userId = GetCurrentUserId();
        var success = await _groupService.JoinGroupAsync(userId, dto);
        if (!success)
            return BadRequest("Already a member or group not found");

        // Notificar al grupo que el usuario se unió
        await _hubContext.Clients.Group($"group_{dto.GroupId}").SendAsync("UserJoinedGroup", new
        {
            GroupId = dto.GroupId,
            UserId = userId
        });

        return Ok(new { success = true, message = "Joined group successfully" });
    }

    /// <summary>
    /// Send a message to a group (requires authentication)
    /// </summary>
    [HttpPost("message")]
    public async Task<IActionResult> SendGroupMessage([FromBody] SendGroupMessageDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.GroupId))
            return BadRequest("Group ID is required");

        if (string.IsNullOrWhiteSpace(dto.Content))
            return BadRequest("Message content cannot be empty");

        var userId = GetCurrentUserId();
        try
        {
            // GroupService.SendGroupMessageAsync already sends the notification via SignalR
            await _groupService.SendGroupMessageAsync(userId, dto);
            return Ok(new { success = true, message = "Message sent" });
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Get messages from a group
    /// </summary>
    [HttpGet("{groupId}/messages")]
    public async Task<IActionResult> GetGroupMessages(string groupId, [FromQuery] int count = 50, [FromQuery] int skip = 0)
    {
        if (string.IsNullOrWhiteSpace(groupId))
            return BadRequest("Group ID is required");

        var messages = await _groupService.GetGroupMessagesAsync(groupId, count);
        return Ok(messages);
    }

    /// <summary>
    /// Get current user's groups (requires authentication)
    /// </summary>
    [HttpGet("user/my-groups")]
    public async Task<IActionResult> GetMyGroups()
    {
        var userId = GetCurrentUserId();
        var groups = await _groupService.GetUserGroupsAsync(userId);
        return Ok(groups);
    }

    /// <summary>
    /// Get all available groups (requires authentication)
    /// </summary>
    [HttpGet("all")]
    public async Task<IActionResult> GetAllGroups([FromQuery] string search = "")
    {
        var groups = await _groupService.GetAllGroupsAsync(search);
        return Ok(groups);
    }

    /// <summary>
    /// Get a specific group by ID
    /// </summary>
    [HttpGet("{groupId}")]
    public async Task<IActionResult> GetGroup(string groupId)
    {
        if (string.IsNullOrWhiteSpace(groupId))
            return BadRequest("Group ID is required");

        var group = await _groupService.GetGroupByIdAsync(groupId);
        if (group == null)
            return NotFound("Group not found");

        return Ok(group);
    }

    /// <summary>
    /// Get all members of a group
    /// </summary>
    [HttpGet("{groupId}/members")]
    public async Task<IActionResult> GetGroupMembers(string groupId)
    {
        if (string.IsNullOrWhiteSpace(groupId))
            return BadRequest("Group ID is required");

        var members = await _groupService.GetGroupMembersAsync(groupId);
        return Ok(members);
    }

    /// <summary>
    /// Remove a user from a group (requires authentication)
    /// </summary>
    [HttpDelete("{groupId}/members/{memberId}")]
    public async Task<IActionResult> RemoveGroupMember(string groupId, string memberId)
    {
        if (string.IsNullOrWhiteSpace(groupId) || string.IsNullOrWhiteSpace(memberId))
            return BadRequest("Group ID and Member ID are required");

        var currentUserId = GetCurrentUserId();
        var success = await _groupService.RemoveGroupMemberAsync(groupId, memberId, currentUserId);

        if (!success)
            return BadRequest("Cannot remove member. You must be the group creator.");

        // Notificar que el miembro fue removido
        await _hubContext.Clients.Group($"group_{groupId}").SendAsync("UserLeftGroup", new
        {
            GroupId = groupId,
            UserId = memberId
        });

        return Ok(new { success = true, message = "Member removed successfully" });
    }

    /// <summary>
    /// Notificar que el usuario está escribiendo en el grupo
    /// </summary>
    [HttpPost("{groupId}/typing")]
    public async Task<IActionResult> NotifyTypingGroup(string groupId)
    {
        if (string.IsNullOrWhiteSpace(groupId))
            return BadRequest("Group ID is required");

        var userId = GetCurrentUserId();

        // Notificar al grupo
        await _hubContext.Clients.Group($"group_{groupId}").SendAsync("UserTypingGroup", new
        {
            GroupId = groupId,
            UserId = userId
        });

        return Ok(new { success = true });
    }
}
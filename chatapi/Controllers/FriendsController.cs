using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using chatapi.Services;
using System.Security.Claims;

namespace chatapi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FriendsController : ControllerBase
{
    private readonly FriendService _friendService;
    private readonly UserService _userService;

    public FriendsController(FriendService friendService, UserService userService)
    {
        _friendService = friendService;
        _userService = userService;
    }

    private string GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
               throw new UnauthorizedAccessException("User not found in token");
    }

    /// <summary>
    /// Send a friend request
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> AddFriend([FromBody] AddFriendRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FriendId))
            return BadRequest(new { success = false, message = "Friend ID is required" });

        var userId = GetCurrentUserId();

        try
        {
            var added = await _friendService.AddFriendAsync(userId, request.FriendId);
            return Ok(new { success = true, message = "Friend request sent successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "Error sending friend request: " + ex.Message });
        }
    }

    /// <summary>
    /// Accept a friend request
    /// </summary>
    [HttpPost("{friendId}/accept")]
    public async Task<IActionResult> AcceptFriendRequest(string friendId)
    {
        if (string.IsNullOrWhiteSpace(friendId))
            return BadRequest(new { success = false, message = "Friend ID is required" });

        var userId = GetCurrentUserId();

        try
        {
            await _friendService.AcceptFriendRequestAsync(userId, friendId);
            return Ok(new { success = true, message = "Friend request accepted" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "Error accepting friend request: " + ex.Message });
        }
    }

    /// <summary>
    /// Reject a friend request
    /// </summary>
    [HttpPost("{friendId}/reject")]
    public async Task<IActionResult> RejectFriendRequest(string friendId)
    {
        if (string.IsNullOrWhiteSpace(friendId))
            return BadRequest(new { success = false, message = "Friend ID is required" });

        var userId = GetCurrentUserId();

        try
        {
            await _friendService.RejectFriendRequestAsync(userId, friendId);
            return Ok(new { success = true, message = "Friend request rejected" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "Error rejecting friend request: " + ex.Message });
        }
    }

    /// <summary>
    /// Get pending friend requests
    /// </summary>
    [HttpGet("requests")]
    public async Task<IActionResult> GetFriendRequests()
    {
        var userId = GetCurrentUserId();
        var requests = await _friendService.GetFriendRequestsAsync(userId);
        return Ok(requests);
    }

    /// <summary>
    /// Get sent friend requests
    /// </summary>
    [HttpGet("requests/sent")]
    public async Task<IActionResult> GetSentFriendRequests()
    {
        var userId = GetCurrentUserId();
        var requests = await _friendService.GetSentFriendRequestsAsync(userId);
        return Ok(requests);
    }

    /// <summary>
    /// Cancel a sent friend request
    /// </summary>
    [HttpPost("{friendId}/cancel")]
    public async Task<IActionResult> CancelFriendRequest(string friendId)
    {
        if (string.IsNullOrWhiteSpace(friendId))
            return BadRequest(new { success = false, message = "Friend ID is required" });

        var userId = GetCurrentUserId();

        try
        {
            await _friendService.CancelFriendRequestAsync(userId, friendId);
            return Ok(new { success = true, message = "Friend request cancelled" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "Error cancelling friend request: " + ex.Message });
        }
    }

    /// <summary>
    /// Remove a friend
    /// </summary>
    [HttpDelete("{friendId}")]
    public async Task<IActionResult> RemoveFriend(string friendId)
    {
        if (string.IsNullOrWhiteSpace(friendId))
            return BadRequest("Friend ID is required");

        var userId = GetCurrentUserId();
        var removed = await _friendService.RemoveFriendAsync(userId, friendId);

        if (!removed)
            return BadRequest(new { success = false, message = "Friend not found" });

        return Ok(new { success = true, message = "Friend removed successfully" });
    }

    /// <summary>
    /// Get all friends of current user
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetFriends()
    {
        var userId = GetCurrentUserId();
        var friends = await _friendService.GetFriendsWithDetailsAsync(userId);
        return Ok(friends);
    }

    /// <summary>
    /// Check if two users are friends
    /// </summary>
    [HttpGet("check/{userId}")]
    public async Task<IActionResult> CheckFriendship(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("User ID is required");

        var currentUserId = GetCurrentUserId();
        var areFriends = await _friendService.AreFriendsAsync(currentUserId, userId);

        return Ok(new { areFriends });
    }

    /// <summary>
    /// Get mutual friends with another user
    /// </summary>
    [HttpGet("mutual/{userId}")]
    public async Task<IActionResult> GetMutualFriends(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("User ID is required");

        var currentUserId = GetCurrentUserId();
        var mutualFriends = await _friendService.GetMutualFriendsAsync(currentUserId, userId);

        return Ok(mutualFriends);
    }
}

public class AddFriendRequest
{
    public string FriendId { get; set; } = null!;
}

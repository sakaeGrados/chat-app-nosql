using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using chatapi.Services;
using chatapi.DTO;
using chatapi.Models;
using chatapi.Hubs;
using System.Security.Claims;

namespace chatapi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly UserService _service;
    private readonly FriendService _friendService;
    private readonly IHubContext<ChatHub> _hubContext;

    public UsersController(UserService service, FriendService friendService, IHubContext<ChatHub> hubContext)
    {
        _service = service;
        _friendService = friendService;
        _hubContext = hubContext;
    }

    private string GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? null;
    }

    /// <summary>
    /// Build UserDto with status privacy enforcement and connection status
    /// Status is only shown if:
    /// - User is connected (IsConnected = true), AND
    /// - (Viewing own profile OR users are accepted friends)
    /// Otherwise status is null for privacy/offline
    /// </summary>
    private async Task<UserDto> BuildUserDtoAsync(User user, string? currentUserId, bool isPublicContext = false)
    {
        var dto = new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            PhoneNumber = user.PhoneNumber,
            ProfilePhoto = user.ProfilePhoto,
            Status = null // Default to null for privacy/offline
        };

        // Show status only if user is connected
        if (!user.IsConnected)
        {
            return dto; // Return null status if disconnected
        }

        // Show status if viewing own profile
        if (currentUserId != null && user.Id == currentUserId)
        {
            dto.Status = user.Status;
        }
        // Show status if users are confirmed friends
        else if (currentUserId != null)
        {
            var areFriends = await _friendService.AreFriendsAsync(currentUserId, user.Id);
            if (areFriends)
            {
                dto.Status = user.Status;
            }
        }

        return dto;
    }

    /// <summary>
    /// Get user by ID (public endpoint)
    /// Status is hidden unless viewing own profile or users are friends
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetUserById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return BadRequest("User ID is required");

        var user = await _service.GetUserByIdAsync(id);
        if (user == null)
            return NotFound("User not found");

        var currentUserId = GetCurrentUserId();
        var dto = await BuildUserDtoAsync(user, currentUserId, isPublicContext: true);
        return Ok(dto);
    }

    /// <summary>
    /// Get current user profile (requires authentication)
    /// </summary>
    [HttpGet("profile/me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUserProfile()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized("User not authenticated");

        var user = await _service.GetUserByIdAsync(userId);
        if (user == null)
            return NotFound("User profile not found");

        var dto = await BuildUserDtoAsync(user, userId);
        return Ok(dto);
    }

    /// <summary>
    /// Update user profile (requires authentication)
    /// </summary>
    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UserDto dto)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId != id)
            return Forbid("You can only update your own profile");

        if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.PhoneNumber))
            return BadRequest("Username and PhoneNumber are required");

        var success = await _service.UpdateUserAsync(id, dto.Username, dto.PhoneNumber);
        if (!success)
            return BadRequest("Failed to update user");

        return Ok("User updated successfully");
    }

    /// <summary>
    /// Delete user account (requires authentication)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId != id)
            return Forbid("You can only delete your own account");

        var success = await _service.DeleteUserAsync(id);
        if (!success)
            return BadRequest("Failed to delete user");

        return Ok("User deleted successfully");
    }

    /// <summary>
    /// Get all users (requires authentication)
    /// Shows ALL users regardless of connection status
    /// This allows users to see and add friends even when they're offline
    /// Status is hidden unless viewing own profile or users are friends
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAllUsers([FromQuery] int limit = 100, [FromQuery] int skip = 0)
    {
        var users = await _service.GetAllUsersAsync(limit, skip);
        var currentUserId = GetCurrentUserId();
        
        var dtos = new List<UserDto>();
        foreach (var user in users)
        {
            // Show ALL users regardless of connection status
            // This allows users to see friends even when they're offline
            var dto = await BuildUserDtoAsync(user, currentUserId);
            dtos.Add(dto);
        }
        
        return Ok(dtos);
    }

    /// <summary>
    /// Search users by username or phone number
    /// Returns ALL users (including offline) to allow searching for friends who aren't currently connected
    /// Status is hidden unless viewing own profile or users are friends
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q)
    {
        if (string.IsNullOrEmpty(q))
            return BadRequest("Query required");

        var users = await _service.SearchUsersAsync(q);
        var currentUserId = GetCurrentUserId();
        
        var dtos = new List<UserDto>();
        foreach (var user in users)
        {
            // For search, show ALL users regardless of connection status
            // This allows finding friends even when they're offline
            var dto = await BuildUserDtoAsync(user, currentUserId, isPublicContext: true);
            dtos.Add(dto);
        }
        
        return Ok(dtos);
    }

    /// <summary>
    /// Change user password (requires authentication)
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized("User not authenticated");

        if (string.IsNullOrWhiteSpace(dto.CurrentPassword) || string.IsNullOrWhiteSpace(dto.NewPassword))
            return BadRequest("Current and new passwords are required");

        var success = await _service.ChangePasswordAsync(userId, dto.CurrentPassword, dto.NewPassword);
        if (!success)
            return BadRequest("Current password is incorrect or failed to change password");

        return Ok("Password changed successfully");
    }

    /// <summary>
    /// Update user status (requires authentication)
    /// </summary>
    [HttpPut("status")]
    [Authorize]
    public async Task<IActionResult> UpdateStatus([FromBody] UpdateUserStatusDto dto)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized("User not authenticated");

        if (string.IsNullOrWhiteSpace(dto.Status))
            return BadRequest("Status is required");

        var success = await _service.UpdateUserStatusAsync(userId, dto.Status);
        if (!success)
            return BadRequest("Failed to update user status");

        // Notify all connected clients about the status change via SignalR
        await _hubContext.Clients.All.SendAsync("UserStatusChanged", new
        {
            UserId = userId,
            Status = dto.Status
        });

        return Ok("Status updated successfully");
    }

    /// <summary>
    /// Update username (requires authentication)
    /// </summary>
    [HttpPut("username")]
    [Authorize]
    public async Task<IActionResult> UpdateUsername([FromBody] UpdateUsernameDto dto)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized("User not authenticated");

        if (string.IsNullOrWhiteSpace(dto.NewUsername))
            return BadRequest("New username is required");

        var success = await _service.UpdateUsernameAsync(userId, dto.NewUsername);
        if (!success)
            return BadRequest("Username already taken or failed to update");

        return Ok("Username updated successfully");
    }

    /// <summary>
    /// Upload or update user profile photo (requires authentication)
    /// Accepts image file and converts to Base64
    /// </summary>
    [HttpPost("profile-photo")]
    [Authorize]
    public async Task<IActionResult> UploadProfilePhoto(IFormFile file)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized("User not authenticated");

        if (file == null || file.Length == 0)
            return BadRequest("File is required");

        // Validate file type - handle null ContentType
        var contentType = file.ContentType?.ToLower() ?? string.Empty;
        if (string.IsNullOrEmpty(contentType))
            return BadRequest("File must have a valid content type. Please ensure the file is a valid image.");
        
        var allowedMimeTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/gif" };
        if (!allowedMimeTypes.Contains(contentType))
            return BadRequest($"Only image files (JPEG, PNG, WebP, GIF) are allowed. Received: {contentType}");

        // Validate file size (max 5MB)
        if (file.Length > 5 * 1024 * 1024)
            return BadRequest("File size must not exceed 5MB");

        try
        {
            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);
                var fileBytes = stream.ToArray();
                var base64String = Convert.ToBase64String(fileBytes);
                var dataUrl = $"data:{contentType};base64,{base64String}";

                var success = await _service.UpdateProfilePhotoAsync(userId, dataUrl);
                if (!success)
                    return BadRequest("Failed to update profile photo");

                return Ok(new { message = "Profile photo updated successfully", photoUrl = dataUrl });
            }
        }
        catch (Exception ex)
        {
            return BadRequest($"Error uploading file: {ex.Message}");
        }
    }

    /// <summary>
    /// Delete user profile photo (requires authentication)
    /// </summary>
    [HttpDelete("profile-photo")]
    [Authorize]
    public async Task<IActionResult> DeleteProfilePhoto()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized("User not authenticated");

        var success = await _service.UpdateProfilePhotoAsync(userId, null);
        if (!success)
            return BadRequest("Failed to delete profile photo");

        return Ok("Profile photo deleted successfully");
    }
}
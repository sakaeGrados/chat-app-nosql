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
public class MessagesController : ControllerBase
{
    private readonly ChatService _chatService;
    private readonly IHubContext<ChatHub> _hubContext;

    public MessagesController(ChatService chatService, IHubContext<ChatHub> hubContext)
    {
        _chatService = chatService;
        _hubContext = hubContext;
    }

    private string GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
               throw new UnauthorizedAccessException("User not found in token");
    }

    /// <summary>
    /// Send a global message visible to all users
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SendGlobalMessage([FromBody] SendMessageDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest("Message content cannot be empty");

        var userId = GetCurrentUserId();
        await _chatService.SendGlobalMessageAsync(userId, request);

        // Obtener el mensaje guardado para obtener el ID y datos completos
        var message = await _chatService.GetGlobalMessagesAsync(1);
        if (message.Any())
        {
            var latestMsg = message.First();
            // Notificar a todos los clientes en tiempo real
            await _hubContext.Clients.All.SendAsync("ReceiveGlobalMessage", new
            {
                Id = latestMsg.Id,
                UserId = latestMsg.UserId,
                Username = latestMsg.Username,
                Content = latestMsg.Content,
                Timestamp = latestMsg.Timestamp
            });
        }

        return Ok(new { success = true, message = "Global message sent" });
    }

    /// <summary>
    /// Get all global messages
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetGlobalMessages([FromQuery] int limit = 50)
    {
        var messages = await _chatService.GetGlobalMessagesAsync(limit);
        return Ok(messages);
    }

    /// <summary>
    /// Send a private message to a specific user
    /// </summary>
    [HttpPost("private")]
    public async Task<IActionResult> SendPrivateMessage([FromBody] SendPrivateMessageDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Content))
            return BadRequest("Message content cannot be empty");

        if (string.IsNullOrWhiteSpace(dto.ReceiverId))
            return BadRequest("Receiver ID is required");

        var senderId = GetCurrentUserId();

        if (senderId == dto.ReceiverId)
            return BadRequest("Cannot send message to yourself");

        await _chatService.SendPrivateMessageAsync(senderId, dto);

        // Obtener el mensaje guardado
        var messages = await _chatService.GetPrivateConversationAsync(senderId, dto.ReceiverId);
        if (messages.Any())
        {
            var latestMsg = messages.Last();
            // Notificar a través de SignalR
            await _hubContext.Clients.All.SendAsync("ReceivePrivateMessage", new
            {
                Id = latestMsg.Id,
                SenderId = latestMsg.UserId,
                SenderUsername = latestMsg.Username,
                ReceiverId = dto.ReceiverId,
                Content = latestMsg.Content,
                Timestamp = latestMsg.Timestamp
            });
        }

        return Ok(new { success = true, message = "Private message sent" });
    }

    /// <summary>
    /// Get private message conversation between two users
    /// </summary>
    [HttpGet("private/{receiverId}")]
    public async Task<IActionResult> GetPrivateConversation(string receiverId)
    {
        if (string.IsNullOrWhiteSpace(receiverId))
            return BadRequest("Receiver ID is required");

        var userId = GetCurrentUserId();

        if (userId == receiverId)
            return BadRequest("Cannot get conversation with yourself");

        var messages = await _chatService.GetPrivateConversationAsync(userId, receiverId);
        return Ok(messages);
    }

    /// <summary>
    /// Notificar que el usuario está escribiendo (cliente puede llamar esto)
    /// </summary>
    [HttpPost("typing/private/{receiverId}")]
    public async Task<IActionResult> NotifyTypingPrivate(string receiverId)
    {
        var userId = GetCurrentUserId();
        
        if (string.IsNullOrWhiteSpace(receiverId) || userId == receiverId)
            return BadRequest("Invalid receiver");

        // Notificar al hub
        await _hubContext.Clients.All.SendAsync("UserTypingPrivate", new
        {
            SenderId = userId,
            ReceiverId = receiverId
        });

        return Ok(new { success = true });
    }
}

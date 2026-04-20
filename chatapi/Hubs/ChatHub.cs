using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using chatapi.Services;

namespace chatapi.Hubs;

/// <summary>
/// ChatHub maneja todas las conexiones en tiempo real para:
/// - Mensajes globales
/// - Mensajes privados
/// - Mensajes de grupo
/// - Estado de usuarios conectados
/// </summary>
public class ChatHub : Hub
{
    // Diccionario para rastrear qué usuario está conectado con qué connectionId
    private static readonly ConcurrentDictionary<string, string> UserConnections = 
        new ConcurrentDictionary<string, string>();

    // Diccionario para rastrear a qué grupos están suscritos los usuarios
    private static readonly ConcurrentDictionary<string, HashSet<string>> UserGroups = 
        new ConcurrentDictionary<string, HashSet<string>>();

    private readonly UserService _userService;

    public ChatHub(UserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Se llama cuando un cliente se conecta al hub
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        Console.WriteLine($"Usuario conectado: {Context.ConnectionId}");
    }

    /// <summary>
    /// Se llama cuando un cliente se desconecta del hub
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Obtener el UserId del usuario desconectado buscando por connectionId
        var userConnection = UserConnections.FirstOrDefault(kvp => kvp.Value == Context.ConnectionId);
        string userId = userConnection.Key;

        if (!string.IsNullOrEmpty(userId))
        {
            UserConnections.TryRemove(userId, out _);
            UserGroups.TryRemove(userId, out _);
            
            try
            {
                // Marcar usuario como desconectado (IsConnected = false, preserva Status anterior)
                await _userService.SetConnectedAsync(userId, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error actualizando estado al desconectar: {ex.Message}");
            }
            
            // Notificar que el usuario se desconectó
            await Clients.All.SendAsync("UserDisconnected", new { UserId = userId });
            
            // Notificar que el estado del usuario cambió (status = null porque está desconectado)
            await Clients.All.SendAsync("UserStatusChanged", new 
            { 
                UserId = userId, 
                Status = (string?)null
            });
            
            Console.WriteLine($"Usuario desconectado: {userId} - IsConnected = false");
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Cliente envía su UserId para asociarlo con su connectionId
    /// </summary>
    public async Task RegisterUser(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return;

        // Remover conexión anterior si existe
        UserConnections.TryRemove(userId, out _);
        
        // Registrar la nueva conexión
        UserConnections.TryAdd(userId, Context.ConnectionId);

        // Marcar usuario como conectado (IsConnected = true, Status = online o el anterior)
        await _userService.SetConnectedAsync(userId, true);

        // Notificar a todos que un usuario se conectó
        await Clients.All.SendAsync("UserConnected", new { UserId = userId });
        
        // Notificar que el estado del usuario cambió a online
        await Clients.All.SendAsync("UserStatusChanged", new 
        { 
            UserId = userId, 
            Status = "online" 
        });
        
        Console.WriteLine($"Usuario registrado: {userId} - ConnectionId: {Context.ConnectionId} - Estado: online");
    }

    /// <summary>
    /// Obtener lista de usuarios conectados
    /// </summary>
    public Task<List<string>> GetConnectedUsers()
    {
        return Task.FromResult(UserConnections.Keys.ToList());
    }

    // ============== MENSAJES GLOBALES ==============

    /// <summary>
    /// Notificar a todos los clientes sobre un nuevo mensaje global
    /// </summary>
    public async Task NotifyGlobalMessage(string messageId, string userId, string username, string content, DateTime timestamp)
    {
        await Clients.All.SendAsync("ReceiveGlobalMessage", new
        {
            Id = messageId,
            UserId = userId,
            Username = username,
            Content = content,
            Timestamp = timestamp
        });
    }

    // ============== MENSAJES PRIVADOS ==============

    /// <summary>
    /// Enviar un mensaje privado a un usuario específico
    /// </summary>
    public async Task SendPrivateMessage(string recipientUserId, string messageId, string senderId, 
                                         string senderUsername, string content, DateTime timestamp)
    {
        if (!UserConnections.TryGetValue(recipientUserId, out var recipientConnectionId))
        {
            // El destinatario no está conectado, pero el mensaje ya está guardado en BD
            Console.WriteLine($"Usuario {recipientUserId} no está conectado");
            return;
        }

        // Enviar al destinatario
        await Clients.Client(recipientConnectionId).SendAsync("ReceivePrivateMessage", new
        {
            Id = messageId,
            SenderId = senderId,
            SenderUsername = senderUsername,
            Content = content,
            Timestamp = timestamp
        });

        // Enviar confirmación al remitente
        if (UserConnections.TryGetValue(senderId, out var senderConnectionId))
        {
            await Clients.Client(senderConnectionId).SendAsync("PrivateMessageSent", new
            {
                RecipientId = recipientUserId,
                MessageId = messageId
            });
        }
    }

    /// <summary>
    /// Notificar que alguien está escribiendo un mensaje privado
    /// </summary>
    public async Task NotifyTypingPrivate(string recipientUserId, string senderId, string senderUsername)
    {
        if (!UserConnections.TryGetValue(recipientUserId, out var recipientConnectionId))
            return;

        await Clients.Client(recipientConnectionId).SendAsync("UserTypingPrivate", new
        {
            SenderId = senderId,
            SenderUsername = senderUsername
        });
    }

    // ============== MENSAJES DE GRUPO ==============

    /// <summary>
    /// Suscribir usuario a un grupo
    /// </summary>
    public async Task JoinGroupChat(string groupId, string userId)
    {
        // Agregar a memoria de grupos
        UserGroups.AddOrUpdate(userId, 
            new HashSet<string> { groupId }, 
            (key, set) => { set.Add(groupId); return set; });

        // Agregar a grupo SignalR
        await Groups.AddToGroupAsync(Context.ConnectionId, $"group_{groupId}");

        // Notificar al grupo
        await Clients.Group($"group_{groupId}").SendAsync("UserJoinedGroup", new
        {
            GroupId = groupId,
            UserId = userId
        });

        Console.WriteLine($"Usuario {userId} se unió al grupo {groupId}");
    }

    /// <summary>
    /// Desuscribir usuario de un grupo
    /// </summary>
    public async Task LeaveGroupChat(string groupId, string userId)
    {
        // Remover de memoria de grupos
        if (UserGroups.TryGetValue(userId, out var groups))
        {
            groups.Remove(groupId);
            if (groups.Count == 0)
                UserGroups.TryRemove(userId, out _);
        }

        // Remover de grupo SignalR
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"group_{groupId}");

        // Notificar al grupo
        await Clients.Group($"group_{groupId}").SendAsync("UserLeftGroup", new
        {
            GroupId = groupId,
            UserId = userId
        });

        Console.WriteLine($"Usuario {userId} salió del grupo {groupId}");
    }

    /// <summary>
    /// Notificar a todos en el grupo sobre un nuevo mensaje
    /// </summary>
    public async Task NotifyGroupMessage(string groupId, string messageId, string userId, 
                                        string username, string content, DateTime timestamp)
    {
        await Clients.Group($"group_{groupId}").SendAsync("ReceiveGroupMessage", new
        {
            Id = messageId,
            GroupId = groupId,
            UserId = userId,
            Username = username,
            Content = content,
            Timestamp = timestamp
        });
    }

    /// <summary>
    /// Notificar que alguien está escribiendo en el grupo
    /// </summary>
    public async Task NotifyTypingGroup(string groupId, string userId, string username)
    {
        await Clients.Group($"group_{groupId}").SendAsync("UserTypingGroup", new
        {
            GroupId = groupId,
            UserId = userId,
            Username = username
        });
    }

    // ============== UTILIDADES ==============

    /// <summary>
    /// Obtener todos los usuarios conectados a un grupo
    /// </summary>
    public Task<List<string>> GetGroupUsers(string groupId)
    {
        var groupConnectionGroupName = $"group_{groupId}";
        var users = UserConnections
            .Where(kvp => UserGroups.TryGetValue(kvp.Key, out var groups) && 
                         groups.Contains(groupId))
            .Select(kvp => kvp.Key)
            .ToList();

        return Task.FromResult(users);
    }

    /// <summary>
    /// Verificar si un usuario está conectado
    /// </summary>
    public Task<bool> IsUserConnected(string userId)
    {
        return Task.FromResult(UserConnections.ContainsKey(userId));
    }

    /// <summary>
    /// Notificar a todos cuando un usuario cambia de estado
    /// </summary>
    public async Task NotifyStatusChange(string userId, string newStatus)
    {
        await Clients.All.SendAsync("UserStatusChanged", new
        {
            UserId = userId,
            Status = newStatus
        });
    }
}

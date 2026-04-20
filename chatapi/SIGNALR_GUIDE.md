# SignalR Integration - Guía de Uso

## 📋 Resumen

La API ahora incluye **SignalR** para mensajes en tiempo real en:
- ✅ Mensajes globales
- ✅ Mensajes privados
- ✅ Mensajes de grupo
- ✅ Notificación de usuario conectado/desconectado
- ✅ Indicador de escritura en tiempo real

---

## 🔗 Conexión al Hub

### URL del Hub
```
ws://localhost:5000/api/chat
```

### Conexión desde JavaScript/TypeScript

```javascript
import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
    .withUrl("http://localhost:5000/api/chat", {
        accessTokenFactory: () => localStorage.getItem("token"),
        withCredentials: true
    })
    .withAutomaticReconnect()
    .withHubProtocol(new signalR.JsonHubProtocol())
    .build();

// Conectar
connection.start().then(() => {
    console.log("Conectado al hub");
    // Registrar el usuario
    connection.invoke("RegisterUser", userId);
}).catch(err => console.error(err));
```

---

## 👤 Métodos del Cliente para Llamar al Servidor

### 1. Registrar Usuario
```javascript
connection.invoke("RegisterUser", userId)
    .catch(err => console.error(err));
```

### 2. Unirse a un Grupo (Chat de Grupo)
```javascript
connection.invoke("JoinGroupChat", groupId, userId)
    .catch(err => console.error(err));
```

### 3. Salir de un Grupo
```javascript
connection.invoke("LeaveGroupChat", groupId, userId)
    .catch(err => console.error(err));
```

### 4. Notificar Escritura Privada
```javascript
connection.invoke("NotifyTypingPrivate", recipientUserId, senderId, senderUsername)
    .catch(err => console.error(err));
```

### 5. Notificar Escritura en Grupo
```javascript
connection.invoke("NotifyTypingGroup", groupId, userId, username)
    .catch(err => console.error(err));
```

### 6. Obtener Usuarios Conectados
```javascript
connection.invoke("GetConnectedUsers")
    .then(users => console.log(users))
    .catch(err => console.error(err));
```

### 7. Verificar si Usuario Está Conectado
```javascript
connection.invoke("IsUserConnected", userId)
    .then(isConnected => console.log(isConnected))
    .catch(err => console.error(err));
```

---

## 📨 Eventos a Escuchar del Servidor

### 1. Mensaje Global Recibido
```javascript
connection.on("ReceiveGlobalMessage", (message) => {
    console.log("Mensaje global:", {
        id: message.id,
        userId: message.userId,
        username: message.username,
        content: message.content,
        timestamp: message.timestamp
    });
});
```

### 2. Mensaje Privado Recibido
```javascript
connection.on("ReceivePrivateMessage", (message) => {
    console.log("Mensaje privado:", {
        id: message.id,
        senderId: message.senderId,
        senderUsername: message.senderUsername,
        receiverId: message.receiverId,
        content: message.content,
        timestamp: message.timestamp
    });
});
```

### 3. Confirmación de Mensaje Privado Enviado
```javascript
connection.on("PrivateMessageSent", (confirmation) => {
    console.log("Mensaje enviado:", confirmation.messageId);
});
```

### 4. Usuario Escribiendo Privado
```javascript
connection.on("UserTypingPrivate", (data) => {
    console.log(`${data.senderUsername} está escribiendo...`);
});
```

### 5. Mensaje de Grupo Recibido
```javascript
connection.on("ReceiveGroupMessage", (message) => {
    console.log("Mensaje de grupo:", {
        id: message.id,
        groupId: message.groupId,
        userId: message.userId,
        username: message.username,
        content: message.content,
        timestamp: message.timestamp
    });
});
```

### 6. Usuario Escribiendo en Grupo
```javascript
connection.on("UserTypingGroup", (data) => {
    console.log(`${data.username} está escribiendo en el grupo...`);
});
```

### 7. Usuario Conectado
```javascript
connection.on("UserConnected", (data) => {
    console.log(`${data.userId} se conectó`);
});
```

### 8. Usuario Desconectado
```javascript
connection.on("UserDisconnected", (data) => {
    console.log(`${data.userId} se desconectó`);
});
```

### 9. Usuario se Unió a Grupo
```javascript
connection.on("UserJoinedGroup", (data) => {
    console.log(`${data.userId} se unió al grupo ${data.groupId}`);
});
```

### 10. Usuario Salió de Grupo
```javascript
connection.on("UserLeftGroup", (data) => {
    console.log(`${data.userId} salió del grupo ${data.groupId}`);
});
```

### 11. Grupo Creado
```javascript
connection.on("GroupCreated", (data) => {
    console.log("Nuevo grupo creado:", {
        groupId: data.groupId,
        groupName: data.groupName,
        creatorId: data.creatorId
    });
});
```

---

## 🌊 Flujo Completo: Chat Global

### 1. Usuario A se Conecta
```javascript
connection.start();
connection.invoke("RegisterUser", userA_id);
```

### 2. Usuario A Envía Mensaje (via REST API)
```javascript
fetch("http://localhost:5000/api/messages", {
    method: "POST",
    headers: {
        "Authorization": `Bearer ${token}`,
        "Content-Type": "application/json"
    },
    body: JSON.stringify({
        content: "¡Hola a todos!"
    })
});
```

### 3. Todos Reciben el Mensaje en Tiempo Real
```javascript
connection.on("ReceiveGlobalMessage", (message) => {
    // El mensaje aparece instantáneamente en la pantalla
});
```

---

## 🎯 Flujo Completo: Chat Privado

### 1. Usuario A se Conecta
```javascript
connection.start();
connection.invoke("RegisterUser", userA_id);
```

### 2. Usuario A Envía Mensaje Privado (via REST API)
```javascript
fetch("http://localhost:5000/api/messages/private", {
    method: "POST",
    headers: {
        "Authorization": `Bearer ${token}`,
        "Content-Type": "application/json"
    },
    body: JSON.stringify({
        receiverId: userB_id,
        content: "Hola Usuario B"
    })
});
```

### 3. Usuario B Recibe el Mensaje en Tiempo Real
```javascript
connection.on("ReceivePrivateMessage", (message) => {
    // El mensaje privado aparece instantáneamente
});
```

---

## 👥 Flujo Completo: Chat de Grupo

### 1. Usuario A Crea un Grupo (via REST API)
```javascript
const group = await fetch("http://localhost:5000/api/groups", {
    method: "POST",
    headers: {
        "Authorization": `Bearer ${token}`,
        "Content-Type": "application/json"
    },
    body: JSON.stringify({
        name: "Proyecto XYZ"
    })
}).then(r => r.json());
```

### 2. Todos Reciben la Notificación en Tiempo Real
```javascript
connection.on("GroupCreated", (data) => {
    console.log("Nuevo grupo creado:", data.groupName);
});
```

### 3. Usuario A se Suscribe al Grupo
```javascript
connection.invoke("JoinGroupChat", group.id, userA_id);
```

### 4. Usuario B se Suscribe al Grupo
```javascript
connection.invoke("JoinGroupChat", group.id, userB_id);
```

### 5. Usuario A Envía Mensaje al Grupo (via REST API)
```javascript
fetch(`http://localhost:5000/api/groups/message`, {
    method: "POST",
    headers: {
        "Authorization": `Bearer ${token}`,
        "Content-Type": "application/json"
    },
    body: JSON.stringify({
        groupId: group.id,
        content: "¿Cómo va el proyecto?"
    })
});
```

### 6. Todos en el Grupo Reciben el Mensaje
```javascript
connection.on("ReceiveGroupMessage", (message) => {
    // El mensaje aparece instantáneamente para todos en el grupo
});
```

---

## ⌨️ Indicador de Escritura

### 1. Notificar que el Usuario está Escribiendo (Privado)
```javascript
// Cuando el usuario empieza a escribir
connection.invoke("NotifyTypingPrivate", recipientId, userId, username);
```

### 2. Recibir Notificación
```javascript
connection.on("UserTypingPrivate", (data) => {
    console.log(`${data.senderUsername} está escribiendo...`);
});
```

### 3. Indicador de Escritura en Grupo
```javascript
// Cuando el usuario empieza a escribir
connection.invoke("NotifyTypingGroup", groupId, userId, username);

// Recibir notificación
connection.on("UserTypingGroup", (data) => {
    console.log(`${data.username} está escribiendo en el grupo...`);
});
```

---

## 🔌 Desconexión

```javascript
connection.stop();
```

---

## 📦 Instalación de Cliente SignalR

### NPM
```bash
npm install @microsoft/signalr
```

### CDN
```html
<script src="https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.0/signalr.min.js"></script>
```

---

## ⚙️ Configuración en Program.cs

### Ya está Configurado:
```csharp
// En Program.cs
builder.Services.AddSignalR();

// ...

// En Middleware
app.MapHub<ChatHub>("/api/chat");
```

---

## 🐛 Troubleshooting

### Error: "Conexión rechazada"
- Verificar que la API está corriendo: `dotnet run`
- Verificar URL del hub: `ws://localhost:5000/api/chat`
- Verificar CORS está habilitado para WebSocket

### Error: "Acceso denegado (401)"
- Verificar que el token JWT es válido
- Verificar que se está enviando correctamente en el header `Authorization`

### Conexión cae frecuentemente
- Aumentar timeout en Program.cs
- Configurar reconexión automática en el cliente

---

## 📝 Ejemplo Completo React

```jsx
import { useEffect, useState } from 'react';
import * as signalR from '@microsoft/signalr';

function ChatComponent() {
    const [connection, setConnection] = useState(null);
    const [messages, setMessages] = useState([]);
    const [userTyping, setUserTyping] = useState('');

    useEffect(() => {
        const conn = new signalR.HubConnectionBuilder()
            .withUrl('http://localhost:5000/api/chat', {
                accessTokenFactory: () => localStorage.getItem('token'),
                withCredentials: true
            })
            .withAutomaticReconnect()
            .build();

        conn.start()
            .then(() => {
                console.log('Conectado');
                conn.invoke('RegisterUser', userId);
            })
            .catch(err => console.error(err));

        // Escuchar mensajes
        conn.on('ReceiveGlobalMessage', (message) => {
            setMessages(prev => [...prev, message]);
        });

        conn.on('UserTypingPrivate', (data) => {
            setUserTyping(`${data.senderUsername} está escribiendo...`);
        });

        setConnection(conn);

        return () => conn.stop();
    }, []);

    const sendMessage = async (content) => {
        await fetch('http://localhost:5000/api/messages', {
            method: 'POST',
            headers: {
                'Authorization': `Bearer ${localStorage.getItem('token')}`,
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ content })
        });
    };

    return (
        <div>
            <div>
                {messages.map(msg => (
                    <p key={msg.id}>{msg.username}: {msg.content}</p>
                ))}
            </div>
            {userTyping && <p>{userTyping}</p>}
            <input 
                onKeyPress={(e) => {
                    if (e.key === 'Enter') {
                        sendMessage(e.target.value);
                        e.target.value = '';
                    }
                }} 
            />
        </div>
    );
}
```

---

## 🎯 Resumen

✅ **SignalR está completamente integrado**
✅ **Mensajes en tiempo real: Global, Privado, Grupo**
✅ **Indicador de conexión/desconexión de usuarios**
✅ **Indicador de escritura en tiempo real**
✅ **Reconexión automática**
✅ **Autenticación JWT integrada**

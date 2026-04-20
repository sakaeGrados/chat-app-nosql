// Private Chat Module
class PrivateChat {
    static connection = null;
    static currentChatWith = null;

    static async init() {
        Auth.init();
        await this.setupSignalR();
        this.loadUsersList();
        this.setupEventListeners();
    }

    static async setupSignalR() {
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl('http://localhost:5026/api/chat', {
                accessTokenFactory: () => API.token,
                withCredentials: true
            })
            .withAutomaticReconnect()
            .build();

        this.connection.on('ReceivePrivateMessage', (message) => {
            if (message.senderId === this.currentChatWith) {
                this.displayMessage(message.senderName, message.content, 'received');
            }
        });

        this.connection.on('PrivateMessageSent', (message) => {
            if (message.receiverId === this.currentChatWith) {
                this.displayMessage('Tú', message.content, 'sent');
            }
        });

        this.connection.on('UserTypingPrivate', (data) => {
            if (data.userId === this.currentChatWith) {
                this.showTypingIndicator(data.senderName);
            }
        });

        try {
            await this.connection.start();
            const userId = Auth.getCurrentUserId();
            await this.connection.invoke('RegisterUser', userId);
        } catch (err) {
            console.error('Error conectando SignalR:', err);
            UI.showError('Error conectando en tiempo real');
        }
    }

    static async loadUsersList() {
        try {
            const users = await API.getAllUsers();
            const usersList = this.createUsersList(users);
            
            const container = document.querySelector('.main-content');
            if (container) {
                const userListDiv = document.createElement('div');
                userListDiv.className = 'users-list';
                userListDiv.style.cssText = `
                    position: absolute;
                    left: 70px;
                    top: 0;
                    width: 250px;
                    height: 100%;
                    background: white;
                    border-right: 1px solid #ddd;
                    overflow-y: auto;
                    padding: 10px 0;
                `;
                userListDiv.innerHTML = usersList;
                container.style.position = 'relative';
                container.appendChild(userListDiv);
                
                // Ajustar chat container
                const chatContainer = document.querySelector('.chat-container');
                if (chatContainer) {
                    chatContainer.style.marginLeft = '250px';
                }
            }
        } catch (error) {
            console.error('Error cargando usuarios:', error);
        }
    }

    static createUsersList(users) {
        const currentUserId = Auth.getCurrentUserId();
        let html = '<div style="padding: 10px; border-bottom: 1px solid #ddd;"><strong>Usuarios</strong></div>';
        
        users.forEach(user => {
            if (user.id !== currentUserId) {
                html += `
                    <div class="user-item" style="
                        padding: 10px 15px;
                        cursor: pointer;
                        border-bottom: 1px solid #eee;
                        transition: background 0.2s;
                    " onclick="PrivateChat.selectUser('${user.id}', '${user.username}')">
                        <div style="font-weight: 500; color: #333;">${user.username}</div>
                        <div style="font-size: 12px; color: #999;">${user.phoneNumber || 'Sin teléfono'}</div>
                    </div>
                `;
            }
        });
        
        return html;
    }

    static selectUser(userId, username) {
        this.currentChatWith = userId;
        document.getElementById('chatTitle').textContent = `Chat con ${username}`;
        document.getElementById('messages').innerHTML = '';
        this.loadMessages(userId);
    }

    static async loadMessages(userId) {
        try {
            const messages = await API.getPrivateConversation(userId);
            const messagesList = document.getElementById('messages');
            messagesList.innerHTML = '';

            messages.forEach(msg => {
                const isOwn = msg.senderId === Auth.getCurrentUserId();
                this.displayMessage(
                    isOwn ? 'Tú' : msg.senderName,
                    msg.content,
                    isOwn ? 'sent' : 'received'
                );
            });
        } catch (error) {
            UI.showError(error.message);
        }
    }

    static setupEventListeners() {
        const sendBtn = document.getElementById('sendBtn');
        const messageInput = document.getElementById('messageInput');

        if (sendBtn) {
            sendBtn.addEventListener('click', () => {
                this.sendMessage();
            });
        }

        if (messageInput) {
            messageInput.addEventListener('keypress', (e) => {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    this.sendMessage();
                }
            });

            // Notificar que está escribiendo
            let typingTimeout;
            messageInput.addEventListener('input', () => {
                clearTimeout(typingTimeout);
                if (this.currentChatWith && this.connection) {
                    this.connection.invoke('NotifyTypingPrivate', this.currentChatWith).catch(console.error);
                }
                typingTimeout = setTimeout(() => {}, 3000);
            });
        }

        const logoutBtn = document.getElementById('logoutBtn');
        if (logoutBtn) {
            logoutBtn.addEventListener('click', () => {
                Auth.logout();
            });
        }
    }

    static async sendMessage() {
        if (!this.currentChatWith) {
            UI.showError('Selecciona un usuario primero');
            return;
        }

        const messageInput = document.getElementById('messageInput');
        const content = messageInput.value.trim();

        if (!content) {
            UI.showError('El mensaje no puede estar vacío');
            return;
        }

        try {
            await API.sendPrivateMessage(this.currentChatWith, content);
            this.displayMessage('Tú', content, 'sent');
            messageInput.value = '';
        } catch (error) {
            UI.showError(error.message);
        }
    }

    static displayMessage(user, content, type) {
        const messages = document.getElementById('messages');
        const messageDiv = document.createElement('div');
        messageDiv.className = `message message-${type}`;
        messageDiv.innerHTML = `
            <div class="message-user">${user}</div>
            <div class="message-content">${content}</div>
            <div class="message-time">${new Date().toLocaleTimeString()}</div>
        `;
        messages.appendChild(messageDiv);
        messages.scrollTop = messages.scrollHeight;
    }

    static showTypingIndicator(username) {
        const messages = document.getElementById('messages');
        const existing = messages.querySelector('.typing-indicator');
        if (existing) existing.remove();

        const typingDiv = document.createElement('div');
        typingDiv.className = 'typing-indicator';
        typingDiv.innerHTML = `<em>${username} está escribiendo...</em>`;
        typingDiv.style.cssText = `
            color: #999;
            font-size: 12px;
            padding: 10px;
            text-align: center;
        `;
        messages.appendChild(typingDiv);

        setTimeout(() => typingDiv.remove(), 3000);
    }

    static async destroy() {
        if (this.connection) {
            await this.connection.stop();
        }
    }
}

// Script de SignalR si no está cargado
if (!window.signalR) {
    const script = document.createElement('script');
    script.src = 'https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.0/signalr.min.js';
    document.head.appendChild(script);
}

// Inicializar cuando el DOM esté listo
document.addEventListener('DOMContentLoaded', () => {
    PrivateChat.init();
});

// Limpiar al cerrar la página
window.addEventListener('beforeunload', () => {
    PrivateChat.destroy();
});

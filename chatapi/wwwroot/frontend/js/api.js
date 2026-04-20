// API Module - Handles all API calls
class API {
    static BASE_URL = 'http://localhost:5026/api';
    static token = null;

    static setToken(token) {
        this.token = token;
    }

    static getHeaders(includeAuth = true) {
        const headers = {
            'Content-Type': 'application/json'
        };

        if (includeAuth && this.token) {
            headers['Authorization'] = `Bearer ${this.token}`;
        }

        return headers;
    }

    static async handleResponse(response) {
        if (response.status === 401) {
            localStorage.removeItem('token');
            localStorage.removeItem('userId');
            window.location.href = 'login.html';
            throw new Error('Sesión expirada');
        }

        if (!response.ok) {
            const error = await response.json().catch(() => ({}));
            throw new Error(error.message || `Error ${response.status}`);
        }

        const contentType = response.headers.get('content-type') || '';
        if (contentType.includes('application/json')) {
            return response.json();
        }

        const text = await response.text();
        return text;
    }

    // ============== AUTH ==============
    static async login(credentials) {
        const response = await fetch(`${this.BASE_URL}/auth/login`, {
            method: 'POST',
            headers: this.getHeaders(false),
            body: JSON.stringify({
                login: credentials.username,
                password: credentials.password
            })
        });
        return this.handleResponse(response);
    }

    static async register(userData) {
        const response = await fetch(`${this.BASE_URL}/auth/register`, {
            method: 'POST',
            headers: this.getHeaders(false),
            body: JSON.stringify({
                username: userData.username,
                phoneNumber: userData.phoneNumber,
                password: userData.password
            })
        });
        return this.handleResponse(response);
    }

    // ============== MESSAGES ==============
    static async sendGlobalMessage(content) {
        const response = await fetch(`${this.BASE_URL}/messages`, {
            method: 'POST',
            headers: this.getHeaders(),
            body: JSON.stringify({ content })
        });
        return this.handleResponse(response);
    }

    static async getGlobalMessages(limit = 50) {
        const response = await fetch(`${this.BASE_URL}/messages?limit=${limit}`, {
            headers: this.getHeaders()
        });
        return this.handleResponse(response);
    }

    static async sendPrivateMessage(receiverId, content) {
        const response = await fetch(`${this.BASE_URL}/messages/private`, {
            method: 'POST',
            headers: this.getHeaders(),
            body: JSON.stringify({ receiverId, content })
        });
        return this.handleResponse(response);
    }

    static async getPrivateConversation(userId) {
        const response = await fetch(`${this.BASE_URL}/messages/private/${userId}`, {
            headers: this.getHeaders()
        });
        return this.handleResponse(response);
    }

    static async notifyTypingPrivate(receiverId) {
        const response = await fetch(`${this.BASE_URL}/messages/typing/private/${receiverId}`, {
            method: 'POST',
            headers: this.getHeaders()
        });
        return this.handleResponse(response);
    }

    // ============== GROUPS ==============
    static async createGroup(name) {
        const response = await fetch(`${this.BASE_URL}/groups`, {
            method: 'POST',
            headers: this.getHeaders(),
            body: JSON.stringify({ name })
        });
        return this.handleResponse(response);
    }

    static async joinGroup(groupId) {
        const response = await fetch(`${this.BASE_URL}/groups/join`, {
            method: 'POST',
            headers: this.getHeaders(),
            body: JSON.stringify({ groupId })
        });
        return this.handleResponse(response);
    }

    static async sendGroupMessage(groupId, content) {
        const response = await fetch(`${this.BASE_URL}/groups/message`, {
            method: 'POST',
            headers: this.getHeaders(),
            body: JSON.stringify({ groupId, content })
        });
        return this.handleResponse(response);
    }

    static async getGroupMessages(groupId, count = 50) {
        const response = await fetch(`${this.BASE_URL}/groups/${groupId}/messages?count=${count}`, {
            headers: this.getHeaders()
        });
        return this.handleResponse(response);
    }

    static async getMyGroups() {
        const response = await fetch(`${this.BASE_URL}/groups/user/my-groups`, {
            headers: this.getHeaders()
        });
        return this.handleResponse(response);
    }

    static async getAllGroups(search = "") {
        const url = search ? `${this.BASE_URL}/groups/all?search=${encodeURIComponent(search)}` : `${this.BASE_URL}/groups/all`;
        const response = await fetch(url, {
            headers: this.getHeaders()
        });
        return this.handleResponse(response);
    }

    static async getGroup(groupId) {
        const response = await fetch(`${this.BASE_URL}/groups/${groupId}`, {
            headers: this.getHeaders()
        });
        return this.handleResponse(response);
    }

    static async getGroupMembers(groupId) {
        const response = await fetch(`${this.BASE_URL}/groups/${groupId}/members`, {
            headers: this.getHeaders()
        });
        return this.handleResponse(response);
    }

    static async removeGroupMember(groupId, memberId) {
        const response = await fetch(`${this.BASE_URL}/groups/${groupId}/members/${memberId}`, {
            method: 'DELETE',
            headers: this.getHeaders()
        });
        return this.handleResponse(response);
    }

    static async notifyTypingGroup(groupId) {
        const response = await fetch(`${this.BASE_URL}/groups/${groupId}/typing`, {
            method: 'POST',
            headers: this.getHeaders()
        });
        return this.handleResponse(response);
    }

    // ============== USERS ==============
    static async getUser(userId) {
        const response = await fetch(`${this.BASE_URL}/users/${userId}`, {
            headers: this.getHeaders()
        });
        return this.handleResponse(response);
    }

    static async getAllUsers(limit = 100) {
        const response = await fetch(`${this.BASE_URL}/users?limit=${limit}`, {
            headers: this.getHeaders()
        });
        return this.handleResponse(response);
    }

    static async searchUsers(query) {
        const response = await fetch(`${this.BASE_URL}/users/search?q=${encodeURIComponent(query)}`, {
            headers: this.getHeaders()
        });
        return this.handleResponse(response);
    }

    static async updateUser(userId, username, phoneNumber) {
        const response = await fetch(`${this.BASE_URL}/users/${userId}`, {
            method: 'PUT',
            headers: this.getHeaders(),
            body: JSON.stringify({ username, phoneNumber })
        });
        return this.handleResponse(response);
    }

    static async deleteUser(userId) {
        const response = await fetch(`${this.BASE_URL}/users/${userId}`, {
            method: 'DELETE',
            headers: this.getHeaders()
        });
        return this.handleResponse(response);
    }

    // ============== FRIENDS ==============
    static async getFriends() {
        const response = await fetch(`${this.BASE_URL}/friends`, {
            headers: this.getHeaders()
        });
        return this.handleResponse(response);
    }

    static async addFriend(friendId) {
        const response = await fetch(`${this.BASE_URL}/friends`, {
            method: 'POST',
            headers: this.getHeaders(),
            body: JSON.stringify({ friendId })
        });
        return this.handleResponse(response);
    }

    static async acceptFriendRequest(friendId) {
        const response = await fetch(`${this.BASE_URL}/friends/${friendId}/accept`, {
            method: 'POST',
            headers: this.getHeaders()
        });
        return this.handleResponse(response);
    }

    static async rejectFriendRequest(friendId) {
        const response = await fetch(`${this.BASE_URL}/friends/${friendId}/reject`, {
            method: 'POST',
            headers: this.getHeaders()
        });
        return this.handleResponse(response);
    }

    static async getFriendRequests() {
        const response = await fetch(`${this.BASE_URL}/friends/requests`, {
            headers: this.getHeaders()
        });
        return this.handleResponse(response);
    }

    static async getSentFriendRequests() {
        const response = await fetch(`${this.BASE_URL}/friends/requests/sent`, {
            headers: this.getHeaders()
        });
        return this.handleResponse(response);
    }

    static async cancelFriendRequest(friendId) {
        const response = await fetch(`${this.BASE_URL}/friends/${friendId}/cancel`, {
            method: 'POST',
            headers: this.getHeaders()
        });
        return this.handleResponse(response);
    }

    static async removeFriend(friendId) {
        const response = await fetch(`${this.BASE_URL}/friends/${friendId}`, {
            method: 'DELETE',
            headers: this.getHeaders()
        });
        return this.handleResponse(response);
    }

    static async checkIfFriends(userId) {
        const response = await fetch(`${this.BASE_URL}/friends/check/${userId}`, {
            headers: this.getHeaders()
        });
        return this.handleResponse(response);
    }

    static async getMutualFriends(userId) {
        const response = await fetch(`${this.BASE_URL}/friends/mutual/${userId}`, {
            headers: this.getHeaders()
        });
        return this.handleResponse(response);
    }

    // ============== PRIVATE MESSAGES ==============
    static async getPrivateMessages(userId, limit = 50) {
        const response = await fetch(`${this.BASE_URL}/messages/private/${userId}?limit=${limit}`, {
            headers: this.getHeaders()
        });
        return this.handleResponse(response);
    }

    // ============== USER STATUS ==============
    static async updateUserStatus(status) {
        const response = await fetch(`${this.BASE_URL}/users/status`, {
            method: 'PUT',
            headers: this.getHeaders(),
            body: JSON.stringify({ Status: status })
        });
        return this.handleResponse(response);
    }

    // ============== PROFILE PHOTO ==============
    static async uploadProfilePhoto(file) {
        if (!file) {
            throw new Error('No file provided');
        }

        const formData = new FormData();
        formData.append('file', file);

        const response = await fetch(`${this.BASE_URL}/users/profile-photo`, {
            method: 'POST',
            headers: {
                'Authorization': `Bearer ${this.token}`
                // IMPORTANT: Do NOT set Content-Type header when using FormData
                // The browser will automatically set it to multipart/form-data with correct boundary
            },
            body: formData
        });
        return this.handleResponse(response);
    }

    static async deleteProfilePhoto() {
        const response = await fetch(`${this.BASE_URL}/users/profile-photo`, {
            method: 'DELETE',
            headers: this.getHeaders()
        });
        return this.handleResponse(response);
    }

    // ============== REFETCH UTILITIES ==============
    static async refetchUserData() {
        /**
         * Refetch all user-related data after profile changes
         * Triggers refresh in all open pages (private-chat, usuarios, etc)
         */
        try {
            // Refetch user profile
            const userId = localStorage.getItem('userId');
            if (userId) {
                const userProfile = await this.getUser(userId);
                if (userProfile && userProfile.profilePhoto) {
                    localStorage.setItem('userProfilePhoto', userProfile.profilePhoto);
                }
            }
            
            // Trigger refresh in pages that are listening
            if (typeof window.RefreshUserData === 'function') {
                window.RefreshUserData();
            }
        } catch (e) {
            console.error('Error refetching user data:', e);
        }
    }
}

// Inicializar token desde localStorage
document.addEventListener('DOMContentLoaded', () => {
    const token = localStorage.getItem('token');
    if (token) {
        API.setToken(token);
    }
});


// Auth Module - Authentication handling
class Auth {
    static init() {
        const token = localStorage.getItem('token');
        const userId = localStorage.getItem('userId');
        const isLoggedIn = token && userId;
        const currentPage = window.location.pathname.split('/').pop();

        // If not logged in and not on auth pages, redirect to login
        if (!isLoggedIn && !['login.html', 'register.html'].includes(currentPage)) {
            window.location.href = 'login.html';
            return;
        }

        // If logged in and on auth pages, redirect to home
        if (isLoggedIn && ['login.html', 'register.html'].includes(currentPage)) {
            window.location.href = 'home.html';
            return;
        }
    }

    static async login(credentials) {
        try {
            // Clear previous user session data before logging in new user
            localStorage.clear();
            
            const response = await API.login(credentials);
            if (response.success) {
                localStorage.setItem('token', response.token);
                localStorage.setItem('userId', response.userId);
                localStorage.setItem('username', credentials.username);
                
                // IMPORTANT: Update API token in memory immediately after saving to localStorage
                API.setToken(response.token);
                
                // Load user profile from API to get profilePhoto from database
                try {
                    const userProfile = await API.getUser(response.userId);
                    if (userProfile && userProfile.profilePhoto) {
                        localStorage.setItem('userProfilePhoto', userProfile.profilePhoto);
                    }
                } catch (e) {
                    console.warn('Could not load profile photo on login:', e);
                }
                
                // Preload friends list from API to ensure they have profilePhoto
                try {
                    const friends = await API.getFriends();
                    // This ensures friends data is available when pages load
                    console.log('Friends preloaded on login:', friends);
                } catch (e) {
                    console.warn('Could not preload friends on login:', e);
                }
                
                UI.showSuccess('Login exitoso');
                window.location.href = 'home.html';
            }
        } catch (error) {
            UI.showError(error.message);
        }
    }

    static async register(userData) {
        try {
            await API.register(userData);
            UI.showSuccess('Registro exitoso. Ahora puedes iniciar sesión.');
            setTimeout(() => {
                window.location.href = 'login.html';
            }, 2000);
        } catch (error) {
            UI.showError(error.message);
        }
    }

    static logout() {
        // Clear ALL session data for this user
        localStorage.clear();
        window.location.href = 'login.html';
    }

    static getCurrentUserId() {
        return localStorage.getItem('userId');
    }

    static getToken() {
        return localStorage.getItem('token');
    }
}

// Initialize auth on page load
document.addEventListener('DOMContentLoaded', () => {
    Auth.init();
});
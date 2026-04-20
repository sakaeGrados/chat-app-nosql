// Sidebar Module - Handles user menu, status, and profile functionality
class SidebarUI {
    static userStatus = 'online'; // online, away, dnd, offline
    static signalRConnection = null;
    static userStatusMap = {}; // Guardar estado de todos los usuarios
    static isInitialized = false;

    static init() {
        if (this.isInitialized) {
            this.displayUserInitials();
            this.loadUserStatus();
            return;
        }

        this.isInitialized = true;
        this.updateSidebarMenuLabels();
        this.loadSidebarState();
        this.displayUserInitials();
        this.setupEventListeners();
        this.loadUserStatus();
        this.setupSignalR();
    }

    static setupSignalR() {
        // Escuchar cambios de estado de otros usuarios
        // Esta función se ejecutará en cada página con SignalR
        // Se llamará desde las páginas de chat directamente
    }

    static registerStatusChangeListener(connection) {
        if (!connection) return;

        if (connection.__sidebarStatusListenerRegistered) {
            return;
        }
        
        this.signalRConnection = connection;
        connection.__sidebarStatusListenerRegistered = true;
        
        connection.on('UserStatusChanged', (data) => {
            const userId = data?.userId || data?.UserId;
            const status = data?.status || data?.Status;

            if (userId && status) {
                this.userStatusMap[userId] = status;
                console.log(`Usuario ${userId} cambió estado a ${status}`);
                
                // Si es el usuario actual, actualizar UI
                const currentUserId = localStorage.getItem('userId');
                if (currentUserId === userId) {
                    this.setUserStatus(status);
                }
            }
        });
    }

    static displayUserInitials() {
        try {
            const username = localStorage.getItem('username') || 'US';
            const initials = this.getInitials(username);
            const profilePhoto = localStorage.getItem('userProfilePhoto');
            
            // Update all initials elements
            const initialsElems = document.querySelectorAll('[id*="Initials"]');
            initialsElems.forEach(elem => {
                if (profilePhoto) {
                    // Si hay foto, mostrarla
                    elem.style.backgroundImage = `url('${profilePhoto}')`;
                    elem.style.backgroundSize = 'cover';
                    elem.style.backgroundPosition = 'center';
                    elem.textContent = '';
                } else {
                    // Si no hay foto, mostrar iniciales
                    elem.style.backgroundImage = '';
                    elem.textContent = initials;
                }
            });
        } catch (error) {
            console.error('Error displaying user initials:', error);
        }
    }

    static getInitials(username) {
        if (!username) return 'US';
        const parts = username.trim().split(' ');
        if (parts.length >= 2) {
            return (parts[0][0] + parts[1][0]).toUpperCase();
        }
        return username.substring(0, 2).toUpperCase();
    }

    static setupEventListeners() {
        // Toggle Sidebar Button
        const toggleBtn = document.getElementById('toggleSidebar');
        console.log('Toggle button found:', toggleBtn); // DEBUG
        if (toggleBtn) {
            console.log('Adding click listener to toggle button'); // DEBUG
            toggleBtn.addEventListener('click', () => {
                console.log('Toggle clicked!'); // DEBUG
                this.toggleSidebar();
            });
        } else {
            console.warn('Toggle button NOT found!'); // DEBUG
        }

        // User menu button toggle
        const userBtn = document.getElementById('sidebarUserBtn');
        const dropdown = document.getElementById('sidebarUserDropdown');

        if (userBtn && dropdown) {
            userBtn.addEventListener('click', (e) => {
                e.stopPropagation();
                dropdown.classList.toggle('open');
                // Close status submenu if open
                const statusSubmenu = document.getElementById('statusSubmenu');
                if (statusSubmenu) {
                    statusSubmenu.remove();
                }
            });

            // Close dropdown when clicking outside
            document.addEventListener('click', (e) => {
                if (!userBtn.contains(e.target) && !dropdown.contains(e.target)) {
                    dropdown.classList.remove('open');
                    const statusSubmenu = document.getElementById('statusSubmenu');
                    if (statusSubmenu) {
                        statusSubmenu.remove();
                    }
                }
            });
        }

        // Logout button
        const logoutBtn = document.getElementById('logoutBtnSidebar');
        if (logoutBtn) {
            logoutBtn.addEventListener('click', (e) => {
                e.preventDefault();
                Auth.logout();
            });
        }

        // View profile button
        const profileBtn = document.getElementById('viewProfileLinkSidebar');
        if (profileBtn) {
            profileBtn.addEventListener('click', (e) => {
                e.preventDefault();
                window.location.href = 'profile.html';
            });
        }

        // Status dropdown button - toggle submenu
        const statusBtn = document.getElementById('statusDropdownBtn');
        if (statusBtn) {
            statusBtn.addEventListener('click', (e) => {
                e.stopPropagation();
                
                // Toggle status submenu
                let submenu = document.getElementById('statusSubmenu');
                if (submenu) {
                    submenu.remove();
                } else {
                    this.createStatusSubmenu(statusBtn);
                }
            });
        }

        // Settings button (if present)
        const settingsBtn = document.getElementById('settingsBottomBtn');
        if (settingsBtn) {
            settingsBtn.addEventListener('click', () => {
                this.showSettingsMenu();
            });
        }
    }

    static createStatusSubmenu(statusBtn) {
        const submenu = document.createElement('div');
        submenu.id = 'statusSubmenu';
        submenu.className = 'status-submenu';
        
        const options = [
            { value: 'online', label: 'Conectado', emoji: '🟢' },
            { value: 'away', label: 'Ausente', emoji: '🟡' },
            { value: 'dnd', label: 'No molestar', emoji: '🔴' },
            { value: 'invisible', label: 'Invisible', emoji: '⚫' }
        ];

        submenu.innerHTML = options.map(opt => `
            <button class="status-option" data-status="${opt.value}">
                <span class="status-emoji">${opt.emoji}</span>
                <span class="status-label">${opt.label}</span>
                ${this.userStatus === opt.value ? '<span class="status-check">✓</span>' : ''}
            </button>
        `).join('');

        // Position submenu relative to the status button
        statusBtn.parentElement.insertBefore(submenu, statusBtn.nextSibling);

        // Add click handlers to status options
        const statusOptions = submenu.querySelectorAll('.status-option');
        statusOptions.forEach(option => {
            option.addEventListener('click', (e) => {
                e.stopPropagation();
                const status = option.dataset.status;
                this.selectStatus(status);
                // Close submenu
                submenu.remove();
            });
        });

        // Close submenu on outside click
        document.addEventListener('click', (e) => {
            if (!submenu.contains(e.target) && !statusBtn.contains(e.target)) {
                if (document.getElementById('statusSubmenu')) {
                    submenu.remove();
                }
            }
        }, { once: true });
    }

    static async selectStatus(status) {
        try {
            // Call API to update status
            await API.updateUserStatus(status);
            this.setUserStatus(status);
        } catch (error) {
            console.error('Error updating status:', error);
            UI.showError('Error al actualizar el estado');
        }
    }

    static setUserStatus(status) {
        this.userStatus = status;
        localStorage.setItem('userStatus', status);
        this.updateStatusUI();

        // Send status to server (if connection exists)
        // This could be enhanced to use SignalR
    }

    static loadUserStatus() {
        const savedStatus = localStorage.getItem('userStatus') || 'online';
        this.userStatus = savedStatus;
        this.updateStatusUI();
    }

    static toggleSidebar() {
        const sidebar = document.querySelector('.sidebar');
        if (sidebar) {
            sidebar.classList.toggle('collapsed');
            const isCollapsed = sidebar.classList.contains('collapsed');
            localStorage.setItem('sidebarCollapsed', isCollapsed);
            this.updateMenuItemsDisplay(isCollapsed);
        }
    }

    static loadSidebarState() {
        const isCollapsed = localStorage.getItem('sidebarCollapsed') === 'true';
        const sidebar = document.querySelector('.sidebar');
        if (sidebar && isCollapsed) {
            sidebar.classList.add('collapsed');
            this.updateMenuItemsDisplay(true);
        }
    }

    // Mapa de items de menú a archivos de foto
    static imageMap = {
        'Inicio': 'hogar.png',
        'Chat Global': 'world-wide-web.png',
        'Chats Privados': 'charlar.png',
        'Grupos': 'business-people.png',
        'Usuarios': 'anadir.png'
    };

    static updateSidebarMenuLabels() {
        const menuItems = document.querySelectorAll('.menu-item');
        menuItems.forEach(item => {
            const fullText = item.textContent.trim();
            // Extraer nombre del item (remover emoji)
            const itemName = fullText.split(' ').slice(1).join(' ');
            
            // Guardar texto original para tooltip y restauración
            item.setAttribute('data-full-text', itemName);
            item.setAttribute('data-title', itemName);
            item.setAttribute('data-item-name', itemName);
            
            // Obtener imagen correspondiente
            const imageName = this.imageMap[itemName];
            if (imageName) {
                item.setAttribute('data-image', imageName);
                
                // Crear contenedor para imagen y texto (sin emoji)
                item.innerHTML = `
                    <img src="/frontend/photos/${imageName}" alt="${itemName}" class="menu-item-image">
                    <span class="menu-item-text">${itemName}</span>
                `;
                
                // Mostrar solo imagen si está colapsado
                const sidebar = document.querySelector('.sidebar');
                if (sidebar?.classList.contains('collapsed')) {
                    item.classList.add('collapsed-item');
                }
            }
        });
    }

    static updateMenuItemsDisplay(isCollapsed) {
        const menuItems = document.querySelectorAll('.menu-item');
        menuItems.forEach(item => {
            const imageName = item.getAttribute('data-image');
            if (imageName) {
                if (isCollapsed) {
                    // Mostrar solo imagen
                    item.classList.add('collapsed-item');
                } else {
                    // Mostrar imagen + texto
                    item.classList.remove('collapsed-item');
                }
            }
        });
    }

    static updateStatusUI() {
        const statusIndicator = document.getElementById('statusIndicator');
        const statusText = document.getElementById('statusText');

        if (statusIndicator && statusText) {
            // Remove all status classes
            statusIndicator.classList.remove('status-online', 'status-away', 'status-dnd', 'status-offline');
            
            // Null-safe: Si no hay estado, no renderizar indicador
            if (!this.userStatus) {
                statusIndicator.style.display = 'none';
                statusText.textContent = '';
                return;
            }
            
            statusIndicator.style.display = '';
            
            // Add appropriate class and text
            const statusMap = {
                'online': { class: 'status-online', text: 'Conectado' },
                'away': { class: 'status-away', text: 'Ausente' },
                'dnd': { class: 'status-dnd', text: 'No molestar' },
                'offline': { class: 'status-offline', text: 'Desconectado' },
                'invisible': { class: 'status-offline', text: 'Invisible' }
            };

            const status = statusMap[this.userStatus] || statusMap['online'];
            statusIndicator.classList.add(status.class);
            statusText.textContent = status.text;
        }
    }

    static showSettingsMenu() {
        UI.showInfo('Función de configuración en desarrollo');
    }

    static updateProfilePhoto(imageData) {
        // Actualizar foto en todos los elementos
        const initialsElems = document.querySelectorAll('[id*="Initials"]');
        initialsElems.forEach(elem => {
            elem.style.backgroundImage = `url('${imageData}')`;
            elem.style.backgroundSize = 'cover';
            elem.style.backgroundPosition = 'center';
            elem.textContent = '';
        });
    }

    static resetProfilePhoto() {
        const username = localStorage.getItem('username') || 'US';
        const initials = this.getInitials(username);
        
        const initialsElems = document.querySelectorAll('[id*="Initials"]');
        initialsElems.forEach(elem => {
            elem.style.backgroundImage = '';
            elem.textContent = initials;
        });
    }
}

// Initialize sidebar when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    SidebarUI.init();
});

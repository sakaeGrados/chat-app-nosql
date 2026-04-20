# Codebase Analysis - Chat API

## 1. Backend Controllers & API Endpoints

### **AuthController** (`chatapi/Controllers/AuthController.cs`)
| Endpoint | Method | Auth | Request DTO | Response | Status Field |
|----------|--------|------|-------------|----------|--------------|
| `/api/auth/register` | POST | ❌ | `RegisterDto` | Success message | N/A |
| `/api/auth/login` | POST | ❌ | `LoginDto` | `LoginResponseDto` | ✅ (Success: bool) |
| `/api/auth/profile` | GET | ✅ | - | `{ UserId, Message }` | N/A |
| `/api/auth/logout` | POST | ✅ | - | "Logout successful" | N/A |

### **UsersController** (`chatapi/Controllers/UsersController.cs`)
| Endpoint | Method | Auth | Response DTO |
|----------|--------|------|--------------|
| `/api/users/{id}` | GET | ❌ | `UserDto` |
| `/api/users/profile/me` | GET | ✅ | `UserDto` |
| `/api/users/{id}` | PUT | ✅ | Success message |
| `/api/users/{id}` | DELETE | ✅ | Success message |
| `/api/users` | GET | ✅ | Array of `UserDto` |
| `/api/users/search?q={query}` | GET | ❌ | Array of `UserDto` |
| `/api/users/change-password` | POST | ✅ | Success message |

### **MessagesController** (`chatapi/Controllers/MessagesController.cs`)
| Endpoint | Method | Auth | Request DTO | Response |
|----------|--------|------|-------------|----------|
| `/api/messages` | POST | ✅ | `SendMessageDto` | `{ success, message }` |
| `/api/messages` | GET | ✅ | - | Array of `MessageDto` |
| `/api/messages/private` | POST | ✅ | `SendPrivateMessageDto` | SignalR broadcast |

### **FriendsController** (`chatapi/Controllers/FriendsController.cs`)
| Endpoint | Method | Auth | Request | Response |
|----------|--------|------|---------|----------|
| `/api/friends` | POST | ✅ | `{ FriendId }` | `{ success, message }` |
| `/api/friends/{friendId}/accept` | POST | ✅ | - | `{ success, message }` |
| `/api/friends/{friendId}/reject` | POST | ✅ | - | `{ success, message }` |
| `/api/friends` | GET | ✅ | - | Array of friends |

### **GroupsController** (`chatapi/Controllers/GroupsController.cs`)
| Endpoint | Method | Auth | Request DTO | Response |
|----------|--------|------|-------------|----------|
| `/api/groups` | POST | ✅ | `CreateGroupDto` | Group object |
| `/api/groups/join` | POST | ✅ | `JoinGroupDto` | `{ success, message }` |
| `/api/groups/message` | POST | ✅ | `SendGroupMessageDto` | `{ success, message }` |

---

## 2. DTO Structure & Status Fields

### Core DTOs with Status Tracking:
```
✅ UserDto
   - Id: string
   - Username: string
   - PhoneNumber: string
   - Status: string = "online" ⭐ [online, away, dnd, invisible]
   - ProfilePhoto: string? (Base64 or URL)

✅ MessageDto (Global & Private)
   - Id: string
   - UserId: string
   - Username: string
   - Content: string
   - Timestamp: DateTime

✅ GroupMessageDto
   - Id: string
   - GroupId: string
   - UserId: string
   - Username: string
   - Content: string
   - Timestamp: DateTime

✅ LoginResponseDto ⭐ (Status tracking)
   - Success: bool ← **Key for login status**
   - Message: string?
   - UserId: string?
   - Token: string?

✅ Message Request DTOs:
   - SendMessageDto: { Content }
   - SendPrivateMessageDto: { ReceiverId, Content }
   - SendGroupMessageDto: { GroupId, Content }
```

---

## 3. Models - User, Friend, Message

### **User Model** (`chatapi/Models/User.cs`)
```csharp
- Id: string (ObjectId)
- Username: string [Required]
- PhoneNumber: string [Required]
- PasswordHash: string [Required]
- Status: string = "online" ⭐ [online, away, dnd, invisible]
- ProfilePhoto: string? (Base64 encoded image or URL)
```
**MongoDB Element Names**: `username`, `phoneNumber`, `passwordHash`, `status`, `profilePhoto`

### **Friend Model** (`chatapi/Models/Friend.cs`)
```csharp
- Id: string (ObjectId)
- UserId: string [Required] ← Sender
- FriendId: string [Required] ← Receiver
- CreatedAt: DateTime = UtcNow
- Status: string = "pending" ⭐ [pending, accepted, blocked]
```
**MongoDB Element Names**: `userId`, `friendId`, `createdAt`, `status`

### **Message Model** (`chatapi/Models/Message.cs`)
```csharp
- Id: string (ObjectId)
- SenderId: string [Required]
- ReceiverId: string? (null = global chat) ← Can be null or UserId
- Content: string [Required]
- Timestamp: DateTime = UtcNow
```
**MongoDB Element Names**: `senderId`, `receiverId`, `content`, `timestamp`

### **Group Model** (`chatapi/Models/Group.cs`)
```csharp
- Id: string (ObjectId)
- Name: string [Required, MaxLength(100)]
- CreatorId: string?
```

### **GroupMessage Model** (`chatapi/Models/GroupMessage.cs`)
- Similar structure to Message but with GroupId instead of ReceiverId

---

## 4. Frontend Files - JavaScript Modules

### **File Structure**: `chatapi/wwwroot/frontend/js/`

| File | Purpose | Key Classes/Functions | Status Handling |
|------|---------|----------------------|-----------------|
| **api.js** | HTTP API calls & SignalR | `class API { }` | Token management, 401 redirect |
| **auth.js** | Authentication flow | `class Auth { }` | Login/logout, token persistence |
| **sidebar.js** | Sidebar UI & user menu | `class SidebarUI { }` | ✅ User status display |
| **chat.js** | Message display & sending | `class Chat { }` | Message rendering |
| **ui.js** | General UI utilities | `class UI { }` | Error/success notifications |

### **Key Features in JavaScript**:

#### **api.js** (`lines 1-50+`)
- Centralized API client with `BASE_URL = 'http://localhost:5026/api'`
- Token management: `setToken()`, `getHeaders(includeAuth = true)`
- Response handling: Automatically redirects on 401 (session expired)
- Methods: `login()`, `register()`, `sendMessage()`, etc.

#### **auth.js** (`lines 1-60+`)
- Session validation on page load
- Redirect logic: Non-auth pages require login
- Local storage keys: `token`, `userId`, `username`
- Methods: `login()`, `register()`, `logout()`, `getCurrentUserId()`

#### **sidebar.js** (`lines 1-100+`) ⭐ **STATUS TRACKING**
- `userStatus = 'online'` (online, away, dnd, offline)
- `userStatusMap = {}` → Caches all users' status
- `displayUserInitials()` → Shows avatar or initials
- `registerStatusChangeListener(connection)` → SignalR listener for `UserStatusChanged`
- `setupSignalR()` → Prepares real-time updates
- Status change listener updates UI when other users' status changes

---

## 5. Frontend HTML Pages - Current Structure

### **Pages Directory**: `chatapi/wwwroot/frontend/pages/`

| Page | File | Purpose | Status Display | User Features |
|------|------|---------|-----------------|----------------|
| **Home/Dashboard** | `home.html` | Main dashboard grid | Sidebar status | Quick access cards |
| **Global Chat** | `global-chat.html` | Global message feed | Sidebar status + connection status | Real-time messages |
| **Private Chat** | `private-chat.html` | 1-to-1 conversations | Sidebar status + user list | Conversations panel |
| **Groups** | `groups.html` | Group management | Sidebar status | Create, join, list groups |
| **Users** | `usuarios.html` | User search & friends | Sidebar status + friend list | Search, add friends |
| **Profile** | `profile.html` | User profile editing | Avatar upload, username change | Photo upload, password change |
| **Login** | `login.html` | Authentication | N/A | Username/phone + password |
| **Register** | `register.html` | User registration | N/A | Username, phone, password |

### **Status Display Locations** ⭐:
1. **Sidebar** (all pages): Status indicator + text ("Conectado", "Ausente", "No molestar", "Invisible")
2. **Global Chat**: Connection status badge (`connectionStatus` element)
3. **Private Chat**: User list showing individual user status
4. **Users Page**: Friend status indicators
5. **Profile Page**: User's own status (editable)

---

## 6. Key Findings

### **Where User Status is Returned in API Responses**:
✅ **UserDto** contains `Status` field (default: "online")
- Endpoints: `GET /api/users/{id}`, `GET /api/users`, `GET /api/users/search`
- Login response includes user status through cached user object

✅ **SignalR Broadcasting**:
- Event: `UserStatusChanged` → transmitted when user changes status
- Payload: `{ userId, status }` or `{ UserId, Status }`
- Listened in: `sidebar.js` via `registerStatusChangeListener()`

### **Friendship Relationship Validation**:
**FriendService.cs** (`chatapi/Services/FriendService.cs`):
1. **AddFriendAsync()**: Checks if users exist, prevents self-friending, prevents duplicate requests
2. **AcceptFriendRequestAsync()**: Verifies request exists with `status="pending"`, updates to `"accepted"`
3. **RejectFriendRequestAsync()**: Removes pending request
4. **Friend Model Status**: `pending → accepted → blocked`

### **Photos/Avatars Storage**:
📍 **Location Identification**:
- User Model: `ProfilePhoto?: string` (Base64 encoded or URL)
- Frontend: `localStorage.getItem('userProfilePhoto')`
- Display: `sidebar.js` shows `backgroundImage` with photo or fallback to initials
- Upload: `profile.html` has file input (`#profilePhotoInput`) + `changePhotoBtn`

### **Current Dropdown/Menu Implementations**:
1. **Sidebar User Dropdown** (all pages):
   - Button: `#sidebarUserBtn` → Toggle dropdown
   - Dropdown ID: `#sidebarUserDropdown` (class: `open`)
   - Options:
     - `#viewProfileLinkSidebar` → Link to profile
     - `#statusDropdownBtn` → Status selector
     - `#logoutBtnSidebar` → Logout

2. **Status Dropdown** (sidebar):
   - Contains: Status indicator dot + status text
   - Expected options: Online, Away, DND, Invisible (hardcoded in `userStatus`)

3. **Message Context Menu** (in chat pages):
   - Would be in `chat.js` (not fully shown in sample)

### **Layout Structure - CSS Styling** (`chatapi/wwwroot/frontend/css/styles.css`):

#### **Height & Overflow**:
```css
.app-container:
  - height: 100vh ← Full viewport height

.sidebar:
  - width: 280px (default) / 70px (collapsed)
  - height: 100vh
  - overflow: visible (header) / hidden (collapsed items)

.sidebar-header:
  - min-height: 60px
  - overflow: visible

.sidebar-menu:
  - max-height: calc(100vh - 220px)
  - overflow-y: auto
  - Line items: height 3rem
  - Overflow: hidden (truncate text when collapsed)

.sidebar-footer:
  - Position: absolute (bottom of sidebar)
  - Padding: 20px

.sidebar-user-btn:
  - height: 50px (default) / 45px (collapsed)
  - width: 50px

.status-indicator:
  - height: 12px
  - width: 12px
  - Border-radius: 50% (circular dot)
```

#### **Responsive Behavior**:
- Sidebar collapses to 70px width
- Menu text hidden with `overflow: hidden`
- Hover tooltips appear via `::after` pseudo-element
- Main content adjusts: `margin-left` changes when sidebar toggles

---

## 7. Critical Integration Points

### **Real-Time Status Updates**:
```
User changes status in UI
    ↓
SignalR Hub receives `UserStatusChanged` event
    ↓
`sidebar.js` listener updates `userStatusMap[userId]`
    ↓
UserDto status field updated (next API call)
    ↓
Frontend displays new status badge
```

### **Authentication Flow**:
```
1. Login → API validates credentials → JWT token issued
2. Token stored: localStorage['token']
3. All subsequent requests: Authorization header with Bearer token
4. 401 response → redirect to login.html
```

### **Friend Validation**:
```
1. User A sends friend request to User B
2. FriendService checks: B exists, A≠B, no duplicate
3. Friend doc created with status="pending"
4. B can accept (status→"accepted") or reject (delete)
```

### **Message Delivery**:
```
Global: Message → MongoDB → SignalR broadcasts to all → UI renders
Private: Message → MongoDB → SignalR to target user + sender → UI renders
Group: Message → MongoDB → SignalR to group members → UI renders
```

---

## 8. File Path Summary

### **Backend**:
- Controllers: `chatapi/Controllers/{Auth,Users,Messages,Friends,Groups}Controller.cs`
- Services: `chatapi/Services/{Auth,User,Chat,Friend,Group}Service.cs`
- Models: `chatapi/Models/{User,Message,Friend,Group}.cs`
- DTOs: `chatapi/DTO/{UserDto,MessageDto,...}.cs`
- Config: `chatapi/Config/{MongoContext,JwtSettings}.cs`
- Hub: `chatapi/Hubs/ChatHub.cs` (SignalR)

### **Frontend**:
- JavaScript: `chatapi/wwwroot/frontend/js/{api,auth,chat,sidebar,ui}.js`
- Styles: `chatapi/wwwroot/frontend/css/styles.css`
- HTML Pages: `chatapi/wwwroot/frontend/pages/{home,profile,global-chat,private-chat,groups,usuarios,login,register}.html`

---

## 9. Potential Issues & Gaps

⚠️ **Status persistence**: User status not persisted to database on change (only in SignalR)
⚠️ **Profile photo encoding**: Base64 encoding may cause payload bloat
⚠️ **Friendship bidirectional**: Only stored unidirectionally (UserA→UserB), requires extra logic for reverse lookup
⚠️ **Real-time status**: Requires active SignalR connection; offline users' status may be stale
⚠️ **Cache invalidation**: Redis cache for users cleared on profile update but may cause race conditions

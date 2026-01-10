# Coral Settings System - Implementation Plan

## Overview

This plan covers the implementation of Coral's settings system, including:
1. **Account management** - View logged-in user, change password, logout
2. **Main settings page** - Central hub for all app configuration
3. **Music library management** - Add, remove, and scan music libraries on-demand
4. **General settings** - Backend URL configuration, app preferences
5. **About section** - Version info, credits

## Current State Analysis

### Authentication System
Coral now has a full authentication system:
- **User entity** with username, password hash, and role (Admin/User)
- **Device tracking** for session management across devices
- **Cookie-based auth** for web/Electron, JWT for native apps

**Existing endpoints:**
- `GET /api/auth/status` - Check if setup required and auth status
- `POST /api/auth/register` - Register first user (admin) or new users
- `POST /api/auth/login` - Login with username/password
- `POST /api/auth/logout` - Logout current session
- `GET /api/auth/me` - Get current user info
- `POST /api/auth/changePassword` - Change current user's password

**Frontend state:**
- `currentUserAtom` - Stores logged-in user (id, username, role)
- `useAuth()` hook - Provides login, logout, register functions
- Auth is initialized on app load and persisted

**Missing UI:**
- No way to see who is logged in
- No logout button accessible from main UI
- No password change form

### Music Library System
Coral's backend currently has:
- **MusicLibrary** entity with `LibraryPath` and `LastScan` fields
- **GET** `/api/onboarding/musicLibraries` - List all registered libraries
- **POST** `/api/onboarding/register?path=<path>` - Register a new library
- **POST** `/api/library/scan` - Trigger scan of all libraries
- **GET** `/api/onboarding/listDirectories?path=<path>` - Browse directories (for directory picker)

**Missing:** Delete/remove library endpoint (needs to be added to backend)

### Current Settings
- Backend URL configuration exists in onboarding flow only
- No centralized settings page
- Theme toggle exists in UI but not in a settings context

---

## Architecture Overview

### Settings Page Navigation

**Navigation structure:**

```
app/
  (tabs)/
    index.tsx
    library/
      albums.tsx
    search.tsx
  settings/              # New
    index.tsx           # Main settings page
    account.tsx         # Account settings (user info, password, logout)
    theme.tsx           # Theme customization (see theme.md)
    libraries.tsx       # Music library management
  artists/
  albums/
```

**Access points:**
- **Web:** Settings button in sidebar
- **Mobile:** Settings icon in tab bar or header
- **Keyboard shortcut:** `Cmd/Ctrl + ,` (future enhancement)

---

## Main Settings Page Design

**Location:** `app/settings/index.tsx`

**UI Structure:**

```typescript
Settings
├─ Account
│  ├─ [Avatar] Username                              [>]
│  │           Role: Admin
│  └─ (navigates to account settings page)
│
├─ General
│  ├─ Backend URL: https://coral.example.com [Edit]
│  └─ App Version: 1.0.0
│
├─ Music Libraries                                    [>]
│  └─ X libraries configured
│
├─ Appearance                                          [>]
│  ├─ Theme Mode: Light / Dark / System [Toggle]
│  └─ Custom Colors (see theme customization)
│
└─ About
   ├─ Version: 1.0.0
   ├─ GitHub Repository [Link]
   └─ License: MIT
```

**Component Structure:**

```typescript
// app/settings/index.tsx

export default function SettingsPage() {
  const currentUser = useAtomValue(currentUserAtom);

  return (
    <ScrollView>
      <SettingsSection title="Account">
        <AccountCard
          username={currentUser?.username}
          role={currentUser?.role}
          onPress={() => router.push('/settings/account')}
        />
      </SettingsSection>

      <SettingsSection title="General">
        <SettingItem
          label="Backend URL"
          value={backendUrl}
          onPress={() => navigate to backend URL editor}
        />
      </SettingsSection>

      <SettingsSection title="Music Libraries">
        <SettingItem
          label="Libraries"
          value={`${libraryCount} configured`}
          onPress={() => router.push('/settings/libraries')}
        />
      </SettingsSection>

      <SettingsSection title="Appearance">
        <ThemeToggle />
        <SettingItem
          label="Theme Customization"
          onPress={() => router.push('/settings/theme')}
        />
      </SettingsSection>

      <SettingsSection title="About">
        <SettingItem label="Version" value="1.0.0" />
        <SettingItem label="GitHub" onPress={() => openURL(...)} />
      </SettingsSection>
    </ScrollView>
  );
}
```

---

## Account Settings Page

**Location:** `app/settings/account.tsx`

**UI Structure:**

```typescript
Account Settings

┌─────────────────────────────────────────────────────────┐
│  [Avatar]  Username                                     │
│            Role: Admin                                  │
└─────────────────────────────────────────────────────────┘

Security
├─ Change Password                                    [>]
│  └─ Opens inline form or modal
│
└─ Active Sessions (future)                           [>]
   └─ View and manage logged-in devices

Danger Zone
└─ [Log Out]
   └─ Red destructive button, logs out current session
```

**Component Structure:**

```typescript
// app/settings/account.tsx

export default function AccountSettingsPage() {
  const currentUser = useAtomValue(currentUserAtom);
  const { logout, isLoading } = useAuth();
  const [showPasswordForm, setShowPasswordForm] = useState(false);

  return (
    <ScrollView>
      {/* User Info Card */}
      <View className="p-4 bg-card rounded-lg mb-6">
        <View className="flex-row items-center gap-4">
          <Avatar size="lg" />
          <View>
            <Text className="text-xl font-semibold">{currentUser?.username}</Text>
            <Text className="text-muted-foreground">{currentUser?.role}</Text>
          </View>
        </View>
      </View>

      {/* Security Section */}
      <SettingsSection title="Security">
        <SettingItem
          icon={KeyIcon}
          label="Change Password"
          onPress={() => setShowPasswordForm(true)}
        />
      </SettingsSection>

      {/* Password Change Form (inline or modal) */}
      {showPasswordForm && (
        <PasswordChangeForm
          onSuccess={() => setShowPasswordForm(false)}
          onCancel={() => setShowPasswordForm(false)}
        />
      )}

      {/* Danger Zone */}
      <SettingsSection title="Danger Zone">
        <Button
          variant="destructive"
          onPress={logout}
          disabled={isLoading}
        >
          <LogOutIcon />
          <Text>Log Out</Text>
        </Button>
      </SettingsSection>
    </ScrollView>
  );
}
```

**Password Change Form:**

```typescript
// components/settings/password-change-form.tsx

interface PasswordChangeFormProps {
  onSuccess: () => void;
  onCancel: () => void;
}

export function PasswordChangeForm({ onSuccess, onCancel }: PasswordChangeFormProps) {
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');

  const changePasswordMutation = useChangePassword();
  const { showToast } = useToast();

  const handleSubmit = async () => {
    // Validation
    if (!currentPassword || !newPassword || !confirmPassword) {
      showToast('Please fill in all fields');
      return;
    }
    if (newPassword !== confirmPassword) {
      showToast('New passwords do not match');
      return;
    }
    if (newPassword.length < 6) {
      showToast('Password must be at least 6 characters');
      return;
    }

    try {
      await changePasswordMutation.mutateAsync({
        body: { currentPassword, newPassword }
      });
      showToast('Password changed successfully');
      onSuccess();
    } catch (error) {
      showToast('Current password is incorrect');
    }
  };

  return (
    <View className="p-4 bg-card rounded-lg gap-4">
      <Text className="font-medium">Change Password</Text>
      <Input
        placeholder="Current password"
        secureTextEntry
        value={currentPassword}
        onChangeText={setCurrentPassword}
      />
      <Input
        placeholder="New password"
        secureTextEntry
        value={newPassword}
        onChangeText={setNewPassword}
      />
      <Input
        placeholder="Confirm new password"
        secureTextEntry
        value={confirmPassword}
        onChangeText={setConfirmPassword}
      />
      <View className="flex-row gap-2 justify-end">
        <Button variant="ghost" onPress={onCancel}>
          <Text>Cancel</Text>
        </Button>
        <Button onPress={handleSubmit} disabled={changePasswordMutation.isPending}>
          <Text>{changePasswordMutation.isPending ? 'Saving...' : 'Save'}</Text>
        </Button>
      </View>
    </View>
  );
}
```

---

## Music Library Management

### Backend API Requirements

**New endpoint needed:**
```csharp
// In OnboardingController or LibraryController
[HttpDelete]
[Route("musicLibraries/{libraryId}")]
public async Task<ActionResult> RemoveMusicLibrary(Guid libraryId)
{
    // Delete library and cascade delete related tracks/albums
    // Return Ok() on success, NotFound() if library doesn't exist
}
```

**Implementation in IndexerService:**
```csharp
public async Task<bool> RemoveMusicLibrary(Guid libraryId)
{
    var library = await _context.MusicLibraries
        .Include(l => l.AudioFiles)
        .FirstOrDefaultAsync(l => l.Id == libraryId);

    if (library == null) return false;

    // Cascade delete will handle AudioFiles and related entities
    _context.MusicLibraries.Remove(library);
    await _context.SaveChangesAsync();
    return true;
}
```

**Existing endpoints to use:**
- `GET /api/onboarding/musicLibraries` - List libraries
- `POST /api/onboarding/register?path=<path>` - Add library
- `POST /api/library/scan` - Scan all libraries
- `GET /api/onboarding/listDirectories?path=<path>` - Directory browser

### Frontend Components

**Directory Picker Component:**
```typescript
// components/settings/directory-picker.tsx

interface DirectoryPickerProps {
  initialPath?: string;
  onPathSelect: (path: string) => void;
  onCancel: () => void;
}

export function DirectoryPicker({ initialPath, onPathSelect, onCancel }: DirectoryPickerProps) {
  // State
  const [currentPath, setCurrentPath] = useState(initialPath || '/');
  const [directories, setDirectories] = useState<string[]>([]);
  const [isLoading, setIsLoading] = useState(false);

  // Fetch directories for current path
  useEffect(() => {
    fetchDirectories(currentPath);
  }, [currentPath]);

  // UI Components:
  // - Breadcrumb navigation (shows current path, allows going up)
  // - Directory list (clickable to navigate down)
  // - Select/Cancel buttons
  // - Loading state
}
```

**Library List Component:**
```typescript
// components/settings/library-list.tsx

interface LibraryListProps {
  libraries: MusicLibraryDto[];
  onRemove: (libraryId: string) => void;
  isRemoving: boolean;
}

export function LibraryList({ libraries, onRemove, isRemoving }: LibraryListProps) {
  // Display list of libraries with:
  // - Path (truncated if too long)
  // - Last scan timestamp (formatted as "2 hours ago")
  // - Remove button (with confirmation dialog)
  // - Empty state if no libraries
}
```

### Music Libraries Page

**Location:** `app/settings/libraries.tsx`

**UI Structure:**

```typescript
Music Libraries

[List of existing libraries]
┌─────────────────────────────────────────────────────────┐
│ /path/to/library/1                                      │
│ Last scanned: 2 hours ago                     [Remove]  │
├─────────────────────────────────────────────────────────┤
│ /path/to/library/2                                      │
│ Last scanned: 1 day ago                       [Remove]  │
└─────────────────────────────────────────────────────────┘

[+ Add Library]

[Scan All Libraries]

Last full scan: 2 hours ago
```

**Add Library Flow:**
1. User clicks "+ Add Library"
2. Directory picker modal appears
3. User browses filesystem and selects directory
4. POST to `/api/onboarding/register?path=<selected-path>`
5. On success:
   - Close modal
   - Refresh library list
   - Show toast: "Library added successfully"
   - Optionally ask "Scan now?" with Yes/No dialog

**Remove Library Flow:**
1. User clicks "Remove" on a library
2. Confirmation dialog appears:
   - Title: "Remove Library?"
   - Message: "This will remove all indexed tracks, albums, and artists from this library. This action cannot be undone."
   - Actions: [Cancel] [Remove]
3. On confirm:
   - DELETE to `/api/musicLibraries/{libraryId}`
   - Show loading state on the library item
   - On success:
     - Refresh library list
     - Show toast: "Library removed"
   - On error:
     - Show error toast with message

**Scan Flow:**
1. User clicks "Scan All Libraries"
2. Button shows loading state
3. POST to `/api/library/scan`
4. Show toast: "Scanning libraries..."
5. On complete (how to detect?):
   - Refresh library list (updated LastScan timestamps)
   - Show toast: "Scan complete"

### Data Management

**React Query hooks:**
```typescript
// lib/hooks/use-music-libraries.ts

export function useMusicLibraries() {
  return useQuery({
    queryKey: ['musicLibraries'],
    queryFn: async () => {
      const response = await client.GET('/api/onboarding/musicLibraries');
      return response.data;
    }
  });
}

export function useAddLibrary() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (path: string) => {
      const response = await client.POST('/api/onboarding/register', {
        params: { query: { path } }
      });
      return response.data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries(['musicLibraries']);
    }
  });
}

export function useRemoveLibrary() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (libraryId: string) => {
      const response = await client.DELETE(`/api/musicLibraries/${libraryId}`);
      return response.data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries(['musicLibraries']);
    }
  });
}

export function useScanLibraries() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      const response = await client.POST('/api/library/scan');
      return response.data;
    },
    onSuccess: () => {
      // Refresh libraries to update LastScan timestamps
      queryClient.invalidateQueries(['musicLibraries']);
    }
  });
}
```

---

## Implementation Steps

### Phase 1: Settings Infrastructure
1. Create reusable settings components:
   - `SettingsSection` (section header + children)
   - `SettingItem` (label, value, navigation arrow, optional icon)
   - `SettingToggle` (label + switch)
   - `AccountCard` (avatar, username, role)
2. Create `app/settings/index.tsx` (main settings page)
3. Add navigation to settings from sidebar (web) and/or tab bar (mobile)

### Phase 2: Account Settings (Priority - enables logout and password change)
4. Create `app/settings/account.tsx` (account settings page)
5. Create `PasswordChangeForm` component
6. Wire up logout functionality using `useAuth()` hook
7. Wire up password change using `useChangePassword()` from generated client
8. Test login → settings → logout flow
9. Test password change flow

### Phase 3: Backend - Music Library Management
10. Add `RemoveMusicLibrary` method to `IIndexerService` and `IndexerService`
11. Add `DELETE /api/musicLibraries/{libraryId}` endpoint to `OnboardingController` or `LibraryController`
12. Test endpoint with Swagger/Postman
13. Verify cascade delete behavior (tracks/albums should be removed)
14. Regenerate OpenAPI spec and run `bun generate-client` in coral-app

### Phase 4: Music Library UI Components
15. Create `DirectoryPicker` component
16. Create `LibraryList` component
17. Create `use-music-libraries.ts` React Query hooks
18. Create confirmation dialog component (or use existing)

### Phase 5: Music Libraries Page
19. Create `app/settings/libraries.tsx`
20. Wire up library list with data fetching
21. Implement add library flow with directory picker
22. Implement remove library flow with confirmation
23. Implement scan all libraries functionality
24. Add loading states and error handling

### Phase 6: General Settings
25. Add backend URL editor (reuse from onboarding)
26. Add app version display
27. Add about section with links

### Phase 7: Polish & Testing
28. Add loading states and error handling for all operations
29. Test on iOS, Android, and Web
30. Add user feedback (toast notifications for all actions)
31. Ensure accessibility (screen reader labels, keyboard navigation)
32. Add empty states (no libraries configured)
33. Test edge cases (invalid paths, duplicate libraries, etc.)

---

## Technical Considerations

### Library Deletion Strategy

**Option 1 (Recommended): Cascade Delete**
- Remove library AND all associated tracks/albums/artists/playlists
- Pros: Clean database, no orphaned data
- Cons: Destructive, need good confirmation UI
- Implementation: Ensure EF Core cascade delete is configured

**Option 2: Soft Delete**
- Mark library as deleted but keep tracks
- Pros: Safer, can restore library later
- Cons: Database bloat over time, more complex queries
- Implementation: Add `IsDeleted` flag to MusicLibrary entity

**Recommendation:** Use cascade delete with strong confirmation UI and clear messaging.

### Directory Picker Implementation

**Web:**
- Custom browser using `/api/onboarding/listDirectories` API
- Show breadcrumb navigation for current path
- Allow going up (parent directory) and down (child directories)
- Show "Select" button to confirm current path

**iOS/Android:**
- Attempt to use native file picker if available (Expo DocumentPicker)
- Fallback to web-style browser if native picker not available
- Note: Mobile apps typically can't browse server filesystem, may need different UX

**Security:**
- Server should validate paths to prevent directory traversal attacks
- Reject paths with `..`, absolute paths outside allowed directories, etc.

### Scan Progress

**Current implementation:**
- Fire-and-forget POST request
- No progress updates
- No way to know when scan completes

**Future enhancement:**
- WebSocket or Server-Sent Events for real-time progress
- Show progress bar with file count/percentage
- Show "Scanning..." status on library items
- Enable cancel operation

**Short-term solution:**
- Show optimistic loading state
- Poll `/api/onboarding/musicLibraries` every 5 seconds to check LastScan updates
- Stop polling after reasonable timeout (5 minutes)

### Error Handling

**Invalid paths:**
- Server returns 400 Bad Request
- Frontend shows error toast: "Invalid path selected"

**Duplicate libraries:**
- Backend should check if path already registered
- Return 409 Conflict
- Frontend shows error toast: "This library is already registered"

**Permission issues:**
- Server can't read directory
- Return 403 Forbidden
- Frontend shows error toast: "Permission denied"

**Network errors:**
- All mutations should have error handling
- Show generic error toast with retry option

---

## Future Enhancements

- **Scan progress:** Real-time progress updates via WebSocket/SSE
- **Partial scan:** Scan individual libraries instead of all at once
- **Auto-scan:** Schedule automatic scans (e.g., daily at 3am)
- **Library statistics:** Show track count, album count, total size per library
- **Library health:** Detect missing files, corrupt metadata, etc.
- **Library aliases:** Give friendly names to library paths
- **Smart playlists:** Auto-generate playlists based on library content
- **Multi-select:** Bulk operations (remove multiple libraries at once)
- **Import/Export:** Backup and restore library configuration

---

## Files to Create/Modify

### Backend (C#)
**Already complete:**
- `POST /api/auth/changePassword` endpoint (implemented in AuthController.cs)
- `ChangePasswordAsync` method in UserService.cs

**Files to modify for library management:**
- `src/Coral.Api/Controllers/OnboardingController.cs` OR `src/Coral.Api/Controllers/LibraryController.cs` (add DELETE endpoint)
- `src/Coral.Services/IIndexerService.cs` (add RemoveMusicLibrary method signature)
- `src/Coral.Services/IndexerService.cs` (implement RemoveMusicLibrary method)
- `src/Coral.Api/openapi.json` (regenerate after adding endpoint)

### Frontend (React Native)
**New files:**
- `components/settings/settings-section.tsx`
- `components/settings/setting-item.tsx`
- `components/settings/account-card.tsx`
- `components/settings/password-change-form.tsx`
- `components/settings/directory-picker.tsx`
- `components/settings/library-list.tsx`
- `lib/hooks/use-music-libraries.ts`
- `app/settings/index.tsx`
- `app/settings/account.tsx`
- `app/settings/libraries.tsx`

**Files to modify:**
- `app/(tabs)/_layout.tsx` OR `components/ui/sidebar.tsx` (add settings navigation)
- `components/util/theme-toggle.tsx` (may need to adapt for settings page)

---

## Notes

This settings system provides a centralized location for all app configuration. The music library management feature gives users full control over their media sources, with clear feedback and confirmation for destructive actions.

The theme customization feature is documented separately in `theme.md` and integrates into this settings system via `app/settings/theme.tsx`.

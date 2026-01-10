import { Platform, View, Pressable } from 'react-native';
import { useRouter } from 'expo-router';
import { useState, useEffect } from 'react';
import { useAtomValue } from 'jotai';
import { ChevronLeftIcon, ChevronRightIcon, SearchIcon, SettingsIcon, LogOutIcon, UserIcon } from 'lucide-react-native';

import { Text } from '@/components/ui/text';
import { Input } from '@/components/ui/input';
import { Icon } from '@/components/ui/icon';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { currentUserAtom, themeAtom, lastSearchQueryAtom } from '@/lib/state';
import { useAuth } from '@/lib/hooks/use-auth';

/**
 * Custom title bar with navigation, search, and user menu.
 *
 * Platform-specific behavior:
 * - Electron macOS (hiddenInset): Traffic light buttons on the left (~70px)
 * - Electron Windows (titleBarOverlay): Native controls on the right (~138px)
 * - Electron Linux (hidden): Varies by desktop environment, typically right (~138px)
 * - Web browser: No window controls, no navigation buttons (browser has them)
 *
 * Renders on web platform (both Electron and browser).
 */
export function TitleBar() {
  // Only show on web platform
  if (Platform.OS !== 'web') {
    return null;
  }

  if (typeof window === 'undefined') {
    return null;
  }

  const isElectron = !!(window as any).electronAPI;

  return <TitleBarContent isElectron={isElectron} />;
}

interface TitleBarContentProps {
  isElectron: boolean;
}

function TitleBarContent({ isElectron }: TitleBarContentProps) {
  const router = useRouter();
  const currentUser = useAtomValue(currentUserAtom);
  const theme = useAtomValue(themeAtom);
  const { logout } = useAuth();

  // Detect platform for control positioning (only relevant for Electron)
  const isMac = isElectron && navigator.platform.toUpperCase().indexOf('MAC') >= 0;

  const handleLogout = async () => {
    await logout();
    router.replace('/');
  };

  return (
    <View
      className="bg-background border-b border-border flex-row items-center justify-between px-2"
      style={{
        height: 33,
        // @ts-expect-error - webkit-app-region is not in React Native types but works in Electron
        WebkitAppRegion: isElectron ? 'drag' : undefined,
      }}
    >
      {/* Left side: macOS traffic lights spacer + navigation buttons */}
      <View className="flex-row items-center">
        {/* macOS Electron: reserve space for traffic lights */}
        {isMac && <View style={{ width: 70 }} />}

        {/* Navigation buttons - only show in Electron (browsers have their own) */}
        {isElectron && (
          <View
            className="flex-row items-center justify-center"
            style={{
              width: 80,
              // @ts-expect-error
              WebkitAppRegion: 'no-drag',
            }}
          >
            <NavButton icon={ChevronLeftIcon} onPress={() => router.back()} />
            <NavButton icon={ChevronRightIcon} onPress={() => window.history.forward()} />
          </View>
        )}
      </View>

      {/* Centered search box (absolute positioning) */}
      <View
        className="absolute left-0 right-0 items-center pointer-events-none"
        style={{ height: 33 }}
      >
        <View
          className="pointer-events-auto justify-center"
          style={{
            width: '50%',
            height: '100%',
            // @ts-expect-error
            WebkitAppRegion: isElectron ? 'no-drag' : undefined,
          }}
        >
          <SearchBox />
        </View>
      </View>

      {/* Right side: User avatar + window controls spacer */}
      <View className="flex-row items-center">
        <View
          style={{
            // @ts-expect-error
            WebkitAppRegion: isElectron ? 'no-drag' : undefined,
          }}
        >
          <UserMenu
            username={currentUser?.username}
            role={currentUser?.role}
            onAccount={() => router.push('/settings/account')}
            onSettings={() => router.push('/settings')}
            onLogout={handleLogout}
          />
        </View>

        {/* Windows/Linux Electron: reserve space for window controls */}
        {isElectron && !isMac && <View style={{ width: 138 }} />}
      </View>
    </View>
  );
}

interface NavButtonProps {
  icon: typeof ChevronLeftIcon;
  onPress: () => void;
}

function NavButton({ icon: IconComponent, onPress }: NavButtonProps) {
  const theme = useAtomValue(themeAtom);
  const iconColor = theme === 'dark' ? '#a1a1aa' : '#71717a';

  return (
    <Pressable
      onPress={onPress}
      className="w-7 h-7 rounded items-center justify-center hover:bg-accent/50 active:bg-accent"
    >
      <Icon as={IconComponent} className="size-4" color={iconColor} />
    </Pressable>
  );
}

function SearchBox() {
  const router = useRouter();
  const lastSearchQuery = useAtomValue(lastSearchQueryAtom);
  const [query, setQuery] = useState(lastSearchQuery);
  const theme = useAtomValue(themeAtom);
  const iconColor = theme === 'dark' ? '#71717a' : '#a1a1aa';

  // Auto-search with debounce
  useEffect(() => {
    const timer = setTimeout(() => {
      if (query.trim()) {
        router.push(`/search?q=${encodeURIComponent(query.trim())}`);
      }
    }, 300);
    return () => clearTimeout(timer);
  }, [query, router]);

  return (
    <View className="flex-1 flex-row items-center my-1">
      <View className="relative w-full">
        <View className="absolute left-2 top-0 bottom-0 justify-center z-10">
          <Icon as={SearchIcon} className="size-3" color={iconColor} />
        </View>
        <Input
          placeholder="Search..."
          value={query}
          onChangeText={setQuery}
          className="text-xs pl-7 pr-3"
          style={{ height: 24 }}
        />
      </View>
    </View>
  );
}

interface UserMenuProps {
  username?: string;
  role?: string;
  onAccount: () => void;
  onSettings: () => void;
  onLogout: () => void;
}

function UserMenu({ username, role, onAccount, onSettings, onLogout }: UserMenuProps) {
  const theme = useAtomValue(themeAtom);
  const iconColor = theme === 'dark' ? '#a1a1aa' : '#71717a';

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Pressable className="w-7 h-7 rounded-full bg-accent items-center justify-center hover:bg-accent/80 focus:outline-none">
          {username ? (
            <Text className="text-xs font-medium">
              {username[0]}
            </Text>
          ) : (
            <Icon as={UserIcon} className="size-4" color={iconColor} />
          )}
        </Pressable>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" side="bottom" className="w-40">
        <DropdownMenuItem onPress={onAccount}>
          <View>
            <Text className="text-sm font-medium leading-tight">
              {username || 'User'}
            </Text>
            <Text className="text-xs text-muted-foreground leading-tight">
              {role || 'Member'}
            </Text>
          </View>
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuItem onPress={onSettings}>
          <Icon as={SettingsIcon} className="size-4 mr-2" />
          <Text>Settings</Text>
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuItem onPress={onLogout} variant="destructive">
          <Icon as={LogOutIcon} className="size-4 mr-2" />
          <Text>Log out</Text>
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

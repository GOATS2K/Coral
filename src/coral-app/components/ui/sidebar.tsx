import { View, Pressable } from 'react-native';
import { Link, usePathname } from 'expo-router';
import { HomeIcon, SearchIcon, LibraryIcon, MenuIcon, Heart } from 'lucide-react-native';
import { Text } from '@/components/ui/text';
import { useAtomValue } from 'jotai';
import { themeAtom } from '@/lib/state';
import { cn } from '@/lib/utils';
import { ThemeToggle } from '@/components/util/theme-toggle';
import { useState } from 'react';
import { Tooltip, TooltipTrigger, TooltipContent } from '@/components/ui/tooltip';

interface NavItemProps {
  href: string;
  icon: React.ComponentType<{ size?: number; color?: string }>;
  label: string;
  isActive: boolean;
  collapsed: boolean;
}

function NavItem({ href, icon: Icon, label, isActive, collapsed }: NavItemProps) {
  const theme = useAtomValue(themeAtom);
  const iconColor = isActive
    ? (theme === 'dark' ? '#fff' : '#000')
    : (theme === 'dark' ? '#71717a' : '#a1a1aa');

  const button = (
    <Link href={href} asChild>
      <Pressable
        className={cn(
          'flex-row items-center rounded-lg transition-colors px-4 h-11',
          collapsed ? 'gap-0 justify-center' : 'gap-3',
          isActive
            ? 'bg-accent'
            : 'hover:bg-accent/50'
        )}
      >
        <Icon size={20} color={iconColor} />
        {!collapsed && (
          <Text
            className={cn(
              'text-base -mt-0.5',
              isActive
                ? 'text-foreground font-semibold'
                : 'text-muted-foreground'
            )}
          >
            {label}
          </Text>
        )}
      </Pressable>
    </Link>
  );

  if (!collapsed) return button;

  return (
    <Tooltip>
      <TooltipTrigger asChild>
        {button}
      </TooltipTrigger>
      <TooltipContent side="right">
        <Text>{label}</Text>
      </TooltipContent>
    </Tooltip>
  );
}

export function Sidebar() {
  const pathname = usePathname();
  const theme = useAtomValue(themeAtom);
  const [collapsed, setCollapsed] = useState(false);

  const navItems = [
    { href: '/', icon: HomeIcon, label: 'Home' },
    { href: '/search', icon: SearchIcon, label: 'Search' },
    { href: '/library/albums', icon: LibraryIcon, label: 'Library' },
  ];

  const playlistItems = [
    { href: '/playlists/favorite-tracks', icon: Heart, label: 'Favorite Tracks' },
  ];

  const iconColor = theme === 'dark' ? '#a1a1aa' : '#71717a';

  const hamburgerButton = (
    <Pressable onPress={() => setCollapsed(!collapsed)} className={cn("flex-row items-center rounded-lg hover:bg-accent/50 py-3", collapsed ? "justify-center px-4" : "px-4 -ml-0.5")}>
      <MenuIcon size={20} color={iconColor} />
    </Pressable>
  );

  return (
    <View className={cn('bg-card border-r border-border pt-6 pb-4 flex-col h-full', collapsed ? 'w-20 px-2' : 'w-64 px-4')}>
      <View className={cn("mb-6 flex-row items-center", collapsed ? "justify-center" : "")}>
        {collapsed ? (
          <Tooltip>
            <TooltipTrigger asChild>
              {hamburgerButton}
            </TooltipTrigger>
            <TooltipContent side="right">
              <Text>Menu</Text>
            </TooltipContent>
          </Tooltip>
        ) : (
          hamburgerButton
        )}
      </View>
      <View className="flex-1">
        {/* Main Navigation */}
        <View className="gap-1 mb-6">
          {navItems.map((item) => (
            <NavItem
              key={item.href}
              href={item.href}
              icon={item.icon}
              label={item.label}
              isActive={item.href === '/' ? pathname === item.href : pathname.startsWith(item.href)}
              collapsed={collapsed}
            />
          ))}
        </View>

        {/* Playlists Section */}
        <View className="gap-1">
          {!collapsed && (
            <Text className="text-muted-foreground text-xs font-semibold uppercase px-4 mb-2">
              Playlists
            </Text>
          )}
          {playlistItems.map((item) => (
            <NavItem
              key={item.href}
              href={item.href}
              icon={item.icon}
              label={item.label}
              isActive={pathname.startsWith(item.href)}
              collapsed={collapsed}
            />
          ))}
        </View>
      </View>
      <View className={cn("flex-row", collapsed ? "justify-center" : "")}>
        <ThemeToggle />
      </View>
    </View>
  );
}

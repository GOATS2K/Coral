import { View, Pressable } from 'react-native';
import { Link, usePathname } from 'expo-router';
import { HomeIcon, SearchIcon, LibraryIcon } from 'lucide-react-native';
import { Text } from '@/components/ui/text';
import { useAtomValue } from 'jotai';
import { themeAtom } from '@/lib/state';
import { cn } from '@/lib/utils';
import { ThemeToggle } from '@/components/util/theme-toggle';

interface NavItemProps {
  href: string;
  icon: React.ComponentType<{ size?: number; color?: string }>;
  label: string;
  isActive: boolean;
}

function NavItem({ href, icon: Icon, label, isActive }: NavItemProps) {
  const theme = useAtomValue(themeAtom);
  const iconColor = isActive
    ? (theme === 'dark' ? '#fff' : '#000')
    : (theme === 'dark' ? '#71717a' : '#a1a1aa');

  return (
    <Link href={href} asChild>
      <Pressable
        className={cn(
          'flex-row items-center gap-3 px-4 py-3 rounded-lg transition-colors',
          isActive
            ? 'bg-accent'
            : 'hover:bg-accent/50'
        )}
      >
        <Icon size={20} color={iconColor} />
        <Text
          className={cn(
            'text-base',
            isActive
              ? 'text-foreground font-semibold'
              : 'text-muted-foreground'
          )}
        >
          {label}
        </Text>
      </Pressable>
    </Link>
  );
}

export function Sidebar() {
  const pathname = usePathname();

  const navItems = [
    { href: '/', icon: HomeIcon, label: 'Home' },
    { href: '/search', icon: SearchIcon, label: 'Search' },
    { href: '/library', icon: LibraryIcon, label: 'Library' },
  ];

  return (
    <View className="w-64 bg-card border-r border-border p-4">
      <View className="mb-6 flex-row items-center justify-between pl-4">
        <Text variant="h4">Coral</Text>
        <ThemeToggle />
      </View>
      <View className="gap-1">
        {navItems.map((item) => (
          <NavItem
            key={item.href}
            href={item.href}
            icon={item.icon}
            label={item.label}
            isActive={pathname === item.href}
          />
        ))}
      </View>
    </View>
  );
}

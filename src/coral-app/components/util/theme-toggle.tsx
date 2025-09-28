import { MoonStarIcon, SunIcon } from 'lucide-react-native';
import { useColorScheme } from 'nativewind';
import { Button } from '@/components/ui/button';
import { Icon } from '@/components/ui/icon';

import * as React from 'react';

import { useAtom } from 'jotai'
import { themeAtom } from '@/app/state';


const THEME_ICONS = {
  light: SunIcon,
  dark: MoonStarIcon,
};

export function ThemeToggle() {
  const [preferredColor, setPrefferedColor] = useAtom(themeAtom)

  return (
    <Button
      onPressIn={() => {
        const color = preferredColor == 'dark' ? "light" : 'dark'
        setPrefferedColor(color)
      }}
      size="icon"
      variant="ghost"
      className="rounded-full web:mx-4">
      <Icon as={THEME_ICONS[preferredColor ?? 'light']} className="size-5" />
    </Button>
  );
}
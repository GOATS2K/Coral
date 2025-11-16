import { MoonStarIcon, SunIcon, MonitorIcon } from 'lucide-react-native';
import { Button } from '@/components/ui/button';
import { Icon } from '@/components/ui/icon';
import { Tooltip, TooltipTrigger, TooltipContent } from '@/components/ui/tooltip';
import { Text } from '@/components/ui/text';

import * as React from 'react';

import { useAtom } from 'jotai'
import { themePreferenceAtom, type ThemePreference } from '@/lib/state';


const THEME_ICONS = {
  light: SunIcon,
  dark: MoonStarIcon,
  system: MonitorIcon,
};

export function ThemeToggle() {
  const [themePreference, setThemePreference] = useAtom(themePreferenceAtom)

  const cycleTheme = () => {
    const cycle: Record<ThemePreference, ThemePreference> = {
      light: 'dark',
      dark: 'system',
      system: 'light',
    };
    setThemePreference(cycle[themePreference]);
  };

  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <Button
          onPressIn={cycleTheme}
          size="icon"
          variant="ghost"
          className="rounded-full">
          <Icon as={THEME_ICONS[themePreference]} className="size-5" />
        </Button>
      </TooltipTrigger>
      <TooltipContent>
        <Text>Toggle Theme</Text>
      </TooltipContent>
    </Tooltip>
  );
}
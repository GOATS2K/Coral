import { Appearance, Platform } from 'react-native';

export default function getPrefferedColor(): 'light' | 'dark' {
  let prefersDarkMode = false;
  if (Platform.OS === 'web') {
    prefersDarkMode =
      window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
  } else {
    prefersDarkMode = Appearance.getColorScheme() === 'dark';
  }

  return prefersDarkMode ? "dark" : "light";
}

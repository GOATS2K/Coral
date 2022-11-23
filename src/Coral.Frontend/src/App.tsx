import { MantineProvider } from '@mantine/core';
import Album from './pages/Album';

export default function App() {
  return (
    <MantineProvider withGlobalStyles withNormalizeCSS theme={{ colorScheme: 'light' }}>
      <Album></Album>
    </MantineProvider>
  );
}
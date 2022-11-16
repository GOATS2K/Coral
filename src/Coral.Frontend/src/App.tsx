import { MantineProvider } from '@mantine/core';
import Player from './components/Player';

export default function App() {
  return (
    <MantineProvider withGlobalStyles withNormalizeCSS>
      <Player></Player>
    </MantineProvider>
  );
}
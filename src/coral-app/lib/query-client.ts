import { QueryClient } from '@tanstack/react-query';

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // Add logging for debugging
      logger: {
        log: (...args) => console.info('[QueryClient]', ...args),
        warn: (...args) => console.warn('[QueryClient]', ...args),
        error: (...args) => console.error('[QueryClient]', ...args),
      },
    },
  },
});

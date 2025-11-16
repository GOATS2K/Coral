import { useEffect, useRef, useState } from 'react';

/**
 * Hook that debounces a loading state to prevent flash of loading indicators
 * for fast operations. Only shows loading after the specified delay.
 *
 * @param isLoading - The actual loading state to debounce
 * @param delay - Delay in ms before showing loading (default: 250ms)
 * @param minDisplayTime - Minimum time to display loading once shown (default: 0ms)
 * @returns Whether to show the loading indicator
 */
export function useDebouncedLoading(
  isLoading: boolean,
  delay: number = 250,
  minDisplayTime: number = 0
): boolean {
  const [shouldShowLoading, setShouldShowLoading] = useState(false);
  const showTimerRef = useRef<NodeJS.Timeout | null>(null);
  const hideTimerRef = useRef<NodeJS.Timeout | null>(null);
  const loadingStartTimeRef = useRef<number | null>(null);

  useEffect(() => {
    if (isLoading) {
      // Clear any existing hide timer when loading starts
      if (hideTimerRef.current) {
        clearTimeout(hideTimerRef.current);
        hideTimerRef.current = null;
      }

      // Start timer to show loading after delay
      if (!shouldShowLoading && !showTimerRef.current) {
        showTimerRef.current = setTimeout(() => {
          setShouldShowLoading(true);
          loadingStartTimeRef.current = Date.now();
          showTimerRef.current = null;
        }, delay);
      }
    } else {
      // Clear show timer if loading finished before delay
      if (showTimerRef.current) {
        clearTimeout(showTimerRef.current);
        showTimerRef.current = null;
      }

      // If loading is currently shown, handle minimum display time
      if (shouldShowLoading && minDisplayTime > 0 && loadingStartTimeRef.current) {
        const timeShown = Date.now() - loadingStartTimeRef.current;
        const remainingTime = Math.max(0, minDisplayTime - timeShown);

        if (remainingTime > 0) {
          hideTimerRef.current = setTimeout(() => {
            setShouldShowLoading(false);
            loadingStartTimeRef.current = null;
            hideTimerRef.current = null;
          }, remainingTime);
        } else {
          setShouldShowLoading(false);
          loadingStartTimeRef.current = null;
        }
      } else if (shouldShowLoading) {
        setShouldShowLoading(false);
        loadingStartTimeRef.current = null;
      }
    }

    // Cleanup timers on unmount or dependency change
    return () => {
      if (showTimerRef.current) {
        clearTimeout(showTimerRef.current);
        showTimerRef.current = null;
      }
      if (hideTimerRef.current) {
        clearTimeout(hideTimerRef.current);
        hideTimerRef.current = null;
      }
    };
  }, [isLoading, delay, minDisplayTime, shouldShowLoading]);

  return shouldShowLoading;
}
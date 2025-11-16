import React, { ReactNode } from 'react';
import { View, ActivityIndicator, Animated } from 'react-native';
import { useDebouncedLoading } from '@/hooks/use-debounced-loading';

interface DebouncedLoaderProps {
  /**
   * Whether the content is currently loading
   */
  isLoading: boolean;

  /**
   * The loading UI to display (can be any ReactNode)
   * If not provided, defaults to an ActivityIndicator
   */
  children?: ReactNode;

  /**
   * Delay in milliseconds before showing the loader (default: 250ms)
   */
  delay?: number;

  /**
   * Minimum time to display the loader once shown (default: 0ms)
   * Prevents jarring quick appearance/disappearance
   */
  minDisplayTime?: number;

  /**
   * Whether to fade in the loader (default: true)
   */
  fadeIn?: boolean;

  /**
   * Whether to render as full screen (default: true)
   */
  fullScreen?: boolean;

  /**
   * Additional className for the container
   */
  className?: string;
}

/**
 * Component that debounces loading states to prevent flashing loading indicators.
 * Provides a simple, drop-in solution for adding loading delays.
 *
 * @example
 * // Basic usage with default ActivityIndicator
 * <DebouncedLoader isLoading={isLoading} />
 *
 * @example
 * // Custom loading content
 * <DebouncedLoader isLoading={isLoading}>
 *   <Text>Loading...</Text>
 * </DebouncedLoader>
 *
 * @example
 * // Custom delay and inline mode
 * <DebouncedLoader isLoading={isLoading} delay={500} fullScreen={false}>
 *   <ActivityIndicator size="small" />
 * </DebouncedLoader>
 */
export function DebouncedLoader({
  isLoading,
  children,
  delay = 250,
  minDisplayTime = 0,
  fadeIn = true,
  fullScreen = true,
  className = '',
}: DebouncedLoaderProps) {
  const shouldShowLoading = useDebouncedLoading(isLoading, delay, minDisplayTime);
  const fadeAnim = React.useRef(new Animated.Value(0)).current;

  React.useEffect(() => {
    if (fadeIn) {
      Animated.timing(fadeAnim, {
        toValue: shouldShowLoading ? 1 : 0,
        duration: 200,
        useNativeDriver: true,
      }).start();
    } else {
      fadeAnim.setValue(shouldShowLoading ? 1 : 0);
    }
  }, [shouldShowLoading, fadeIn, fadeAnim]);

  if (!shouldShowLoading && !fadeIn) {
    return null;
  }

  // Default loading content
  const loadingContent = children || <ActivityIndicator size="large" />;

  // Container styles based on fullScreen prop
  const containerClasses = fullScreen
    ? `flex-1 items-center justify-center bg-background ${className}`
    : className;

  if (fadeIn) {
    return (
      <Animated.View
        style={[
          { opacity: fadeAnim },
          fullScreen && { flex: 1, alignItems: 'center', justifyContent: 'center' }
        ]}
        className={containerClasses}
        pointerEvents={shouldShowLoading ? 'auto' : 'none'}
      >
        {loadingContent}
      </Animated.View>
    );
  }

  if (!shouldShowLoading) {
    return null;
  }

  return (
    <View
      style={fullScreen ? { flex: 1, alignItems: 'center', justifyContent: 'center' } : undefined}
      className={containerClasses}
    >
      {loadingContent}
    </View>
  );
}
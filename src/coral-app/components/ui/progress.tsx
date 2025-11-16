import React from 'react';
import { View } from 'react-native';
import { cn } from '@/lib/utils';

interface ProgressProps {
  value?: number; // 0-100, undefined = indeterminate
  className?: string;
  indicatorClassName?: string;
}

export function Progress({ value, className, indicatorClassName }: ProgressProps) {
  const isIndeterminate = value === undefined;
  const progressValue = Math.min(100, Math.max(0, value ?? 0));

  return (
    <View className={cn('h-2 w-full overflow-hidden rounded-full bg-secondary', className)}>
      {isIndeterminate ? (
        <View className={cn('h-full w-full animate-pulse bg-primary', indicatorClassName)} />
      ) : (
        <View
          className={cn('h-full bg-primary', indicatorClassName)}
          style={{ width: `${progressValue}%` }}
        />
      )}
    </View>
  );
}

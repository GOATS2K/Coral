import { Platform } from 'react-native';
import { createPortal } from 'react-dom';
import { useEffect } from 'react';
import { Text } from '@/components/ui/text';
import { useToast } from '@/lib/hooks/use-toast';

export function ToastContainer() {
  const { toast, hideToast } = useToast();

  // Auto-hide toast after 3 seconds
  useEffect(() => {
    if (toast) {
      const timer = setTimeout(() => hideToast(), 3000);
      return () => clearTimeout(timer);
    }
  }, [toast, hideToast]);

  if (Platform.OS !== 'web' || !toast || typeof document === 'undefined') {
    return null;
  }

  return createPortal(
    <div
      className="fixed bottom-20 left-1/2 -translate-x-1/2 bg-popover border border-border rounded-md shadow-lg px-4 py-3 z-50 animate-in fade-in slide-in-from-bottom-2"
      style={{ maxWidth: '90vw' }}
    >
      <Text className="text-foreground text-sm">{toast.message}</Text>
    </div>,
    document.body
  );
}

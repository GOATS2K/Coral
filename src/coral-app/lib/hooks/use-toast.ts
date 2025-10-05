import { atom, useAtom } from 'jotai';
import { useCallback } from 'react';

interface Toast {
  id: string;
  message: string;
}

const toastAtom = atom<Toast | null>(null);

export function useToast() {
  const [toast, setToast] = useAtom(toastAtom);

  const showToast = useCallback((message: string) => {
    const id = Math.random().toString(36);
    setToast({ id, message });
  }, [setToast]);

  const hideToast = useCallback(() => {
    setToast(null);
  }, [setToast]);

  return {
    toast,
    showToast,
    hideToast,
  };
}

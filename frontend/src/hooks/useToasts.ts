import { useCallback, useRef, useState } from 'react';

export type ToastKind = 'info' | 'success' | 'error';

export interface Toast {
  id: number;
  kind: ToastKind;
  text: string;
}

export function useToasts(ttl = 4500) {
  const [toasts, setToasts] = useState<Toast[]>([]);
  const seq = useRef(0);

  const dismiss = useCallback((id: number) => {
    setToasts((list) => list.filter((t) => t.id !== id));
  }, []);

  const push = useCallback(
    (kind: ToastKind, text: string) => {
      const id = (seq.current += 1);
      setToasts((list) => [...list.slice(-3), { id, kind, text }]);
      window.setTimeout(() => dismiss(id), ttl);
    },
    [dismiss, ttl],
  );

  return { toasts, push, dismiss };
}

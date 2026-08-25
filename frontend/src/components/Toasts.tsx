import type { Toast } from '../hooks/useToasts';
import { IconAlert, IconCheck, IconClose, IconSparkle } from './Icons';

const ICON = {
  info: IconSparkle,
  success: IconCheck,
  error: IconAlert,
};

interface Props {
  toasts: Toast[];
  onDismiss: (id: number) => void;
}

export function Toasts({ toasts, onDismiss }: Props) {
  if (toasts.length === 0) return null;

  return (
    <div className="toasts" role="status" aria-live="polite">
      {toasts.map((t) => {
        const Icon = ICON[t.kind];
        return (
          <div key={t.id} className={`toast toast--${t.kind}`}>
            <Icon size={16} />
            <span style={{ flex: 1, minWidth: 0 }}>{t.text}</span>
            <button
              className="icon-btn icon-btn--sm"
              onClick={() => onDismiss(t.id)}
              aria-label="Закрыть"
            >
              <IconClose size={13} />
            </button>
          </div>
        );
      })}
    </div>
  );
}

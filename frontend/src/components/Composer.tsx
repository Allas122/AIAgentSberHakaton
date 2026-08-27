import { useEffect, useImperativeHandle, useRef, useState } from 'react';
import type { RefObject } from 'react';
import { IconClip, IconClose, IconFile, IconSend } from './Icons';

export interface ComposerHandle {
  fill: (text: string) => void;
  focus: () => void;
}

interface Props {
  disabled: boolean;
  busy: boolean;
  placeholder?: string;
  blockedReason?: string | null;
  handleRef?: RefObject<ComposerHandle | null>;
  onSend: (text: string, file: File | null) => void;
}

const ACCEPT = '.docx,.pdf';
const ALLOWED = ['.docx', '.pdf'];
const MAX_MB = 50;
const MAX_CONTENT = 5000;

export function Composer({
  disabled,
  busy,
  placeholder,
  blockedReason,
  handleRef,
  onSend,
}: Props) {
  const [text, setText] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const [dragging, setDragging] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const areaRef = useRef<HTMLTextAreaElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  useImperativeHandle(handleRef, () => ({
    fill: (value: string) => {
      setText(value);
      requestAnimationFrame(() => areaRef.current?.focus());
    },
    focus: () => areaRef.current?.focus(),
  }));

  useEffect(() => {
    const el = areaRef.current;
    if (!el) return;
    el.style.height = 'auto';
    el.style.height = `${el.scrollHeight}px`;
  }, [text]);

  const attach = (candidate: File | undefined | null) => {
    if (!candidate) return;
    if (!ALLOWED.some((extension) => candidate.name.toLowerCase().endsWith(extension))) {
      setError('Заявку нужно приложить в формате .docx или .pdf');
      return;
    }
    if (candidate.size > MAX_MB * 1024 * 1024) {
      setError(`Файл больше ${MAX_MB} МБ`);
      return;
    }
    setError(null);
    setFile(candidate);
  };

  const submit = () => {
    if (disabled || busy) return;
    if (!text.trim() && !file) return;
    if (blockedReason) {
      setError(blockedReason);
      return;
    }
    if (file && text.length > MAX_CONTENT) {
      setError(`С файлом можно передать не больше ${MAX_CONTENT} символов`);
      return;
    }
    onSend(text, file);
    setText('');
    setFile(null);
    setError(null);
  };

  const onKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey && !e.nativeEvent.isComposing) {
      e.preventDefault();
      submit();
    }
  };

  const canSend = !disabled && !busy && (text.trim().length > 0 || file !== null);

  return (
    <div className="composer">
      <div className="composer__inner">
        {(file || error) && (
          <div className="composer__attachments">
            {file && (
              <span className="attach-pill">
                <IconFile size={14} />
                <span title={file.name}>{file.name}</span>
                <button
                  className="icon-btn icon-btn--sm"
                  onClick={() => setFile(null)}
                  aria-label="Убрать файл"
                >
                  <IconClose size={13} />
                </button>
              </span>
            )}
            {error && (
              <span className="chip chip--warn">
                <span className="chip__text">{error}</span>
              </span>
            )}
          </div>
        )}

        <div
          className="composer__box"
          data-dragging={dragging}
          onDragOver={(e) => {
            e.preventDefault();
            setDragging(true);
          }}
          onDragLeave={() => setDragging(false)}
          onDrop={(e) => {
            e.preventDefault();
            setDragging(false);
            attach(e.dataTransfer.files?.[0]);
          }}
        >
          <input
            ref={inputRef}
            type="file"
            accept={ACCEPT}
            className="sr-only"
            tabIndex={-1}
            aria-hidden="true"
            onChange={(e) => {
              attach(e.target.files?.[0]);
              e.target.value = '';
            }}
          />

          <button
            className="icon-btn"
            onClick={() => inputRef.current?.click()}
            disabled={disabled || busy}
            title="Приложить заявку (.docx или .pdf)"
            aria-label="Приложить заявку"
          >
            <IconClip />
          </button>

          <textarea
            ref={areaRef}
            className="composer__textarea"
            rows={1}
            value={text}
            disabled={disabled}
            placeholder={placeholder ?? 'Спросите про заявку или приложите документ…'}
            onChange={(e) => setText(e.target.value)}
            onKeyDown={onKeyDown}
            aria-label="Сообщение"
          />

          <button
            className="send-btn"
            onClick={submit}
            disabled={!canSend}
            title="Отправить"
            aria-label="Отправить сообщение"
          >
            <IconSend size={17} />
          </button>
        </div>

        <p className="composer__hint">
          <kbd>Enter</kbd> — отправить, <kbd>Shift</kbd> + <kbd>Enter</kbd> — новая строка
        </p>
      </div>
    </div>
  );
}

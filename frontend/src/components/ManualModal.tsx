import { useEffect, useRef, useState } from 'react';
import type { ManualScope } from '../api/types';
import { ManualScope as Scope } from '../api/types';
import { IconFile, IconUpload } from './Icons';

interface Props {
  onClose: () => void;
  onSubmit: (title: string, file: File, scope: ManualScope) => Promise<void>;
}

const ACCEPT = '.md,.txt,.docx,.pdf,.csv,.xlsx';
const ALLOWED = ['.md', '.txt', '.docx', '.pdf', '.csv', '.xlsx'];
const MAX_MB = 10;
const TITLE_MIN = 4;
const TITLE_MAX = 500;

export function ManualModal({ onClose, onSubmit }: Props) {
  const [title, setTitle] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const [dragging, setDragging] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [scope, setScope] = useState<ManualScope>(Scope.Staff);

  const inputRef = useRef<HTMLInputElement>(null);
  const titleRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    titleRef.current?.focus();
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !busy) onClose();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [busy, onClose]);

  const attach = (candidate: File | undefined | null) => {
    if (!candidate) return;
    const name = candidate.name.toLowerCase();

    if (!ALLOWED.some((extension) => name.endsWith(extension))) {
      setError(`Документ принимается в форматах ${ALLOWED.join(', ')}`);
      return;
    }
    if (candidate.size > MAX_MB * 1024 * 1024) {
      setError(`Файл больше ${MAX_MB} МБ`);
      return;
    }
    setError(null);
    setFile(candidate);
    if (!title.trim()) setTitle(candidate.name.replace(/\.[^.]+$/, ''));
  };

  const titleError =
    title.trim().length > 0 && title.trim().length < TITLE_MIN
      ? `Название — минимум ${TITLE_MIN} символа`
      : title.trim().length > TITLE_MAX
        ? `Название — максимум ${TITLE_MAX} символов`
        : null;

  const canSubmit = !!file && !titleError && title.trim().length >= TITLE_MIN && !busy;

  const submit = async () => {
    if (!canSubmit || !file) return;
    setBusy(true);
    try {
      await onSubmit(title.trim(), file, scope);
      onClose();
    } catch {
      setBusy(false);
    }
  };

  return (
    <>
      <div className="overlay" onClick={() => !busy && onClose()} />
      <div className="modal" role="dialog" aria-modal="true" aria-label="Загрузка документа в базу знаний">
        <h2 className="modal__title">Загрузить документ в базу знаний</h2>
        <p className="modal__sub">
          Принимаются .md, .txt, .docx, .pdf, а также таблицы .csv и .xlsx — например выгрузка
          мониторинга. Сначала файл сохраняется в хранилище, а разбор на разделы идёт в фоне —
          окно можно закрыть, о ходе и итогах разбора сообщу отдельно. Для документов крупнее
          256 КБ нужен вход под служебным аккаунтом.
        </p>

        <div className="field" style={{ marginBottom: 16 }}>
          <span className="label">Кому доступен</span>
          <div className="scope-choice" role="radiogroup" aria-label="Кому доступен документ">
            <button
              type="button"
              role="radio"
              aria-checked={scope === Scope.Staff}
              className={scope === Scope.Staff ? 'scope-choice__item is-active' : 'scope-choice__item'}
              disabled={busy}
              onClick={() => setScope(Scope.Staff)}
            >
              <span className="scope-choice__title">Служебный</span>
              <span className="scope-choice__hint">Только в служебных чатах. Сюда — мониторинги и отчёты</span>
            </button>
            <button
              type="button"
              role="radio"
              aria-checked={scope === Scope.Public}
              className={scope === Scope.Public ? 'scope-choice__item is-active' : 'scope-choice__item'}
              disabled={busy}
              onClick={() => setScope(Scope.Public)}
            >
              <span className="scope-choice__title">Общий</span>
              <span className="scope-choice__hint">Виден всем, включая студентов и гостей</span>
            </button>
          </div>
        </div>

        <div className="field" style={{ marginBottom: 16 }}>
          <label className="label" htmlFor="manual-title">
            Название
          </label>
          <input
            id="manual-title"
            ref={titleRef}
            className="input"
            value={title}
            disabled={busy}
            onChange={(e) => setTitle(e.target.value)}
            placeholder="Например: Мониторинг контингента 2026"
            aria-invalid={titleError !== null}
          />
          {titleError && (
            <span className="label" style={{ color: 'var(--danger)' }}>
              {titleError}
            </span>
          )}
        </div>

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
          className="dropzone"
          style={{ width: '100%' }}
          data-dragging={dragging}
          disabled={busy}
          onClick={() => inputRef.current?.click()}
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
          {file ? <IconFile size={22} /> : <IconUpload size={22} />}
          <span>{file ? file.name : 'Перетащите файл или нажмите, чтобы выбрать'}</span>
        </button>

        {error && (
          <p className="modal__sub" style={{ color: 'var(--danger)', margin: '10px 0 0' }}>
            {error}
          </p>
        )}

        <div className="modal__actions">
          <button className="btn btn--subtle" onClick={onClose} disabled={busy}>
            Отмена
          </button>
          <button
            className="btn btn--primary"
            onClick={submit}
            disabled={!canSubmit}
          >
            {busy ? 'Сохраняю…' : 'Загрузить'}
          </button>
        </div>
      </div>
    </>
  );
}

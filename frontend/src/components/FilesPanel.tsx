import { useCallback, useEffect, useRef, useState } from 'react';
import { api } from '../api/client';
import type { StoredDocument } from '../api/types';
import { DOCUMENT_KIND_LABELS, DOCUMENT_KINDS, REVIEW_STAGE_LABELS } from '../api/types';
import { IconFile } from './Icons';

const RUNNING_STAGES = ['Queued', 'Reviewing'];
const REFRESH_MS = 5000;

interface Props {
  notify: (kind: 'info' | 'success' | 'error', text: string) => void;
  onManualsChanged?: () => void;
}

const DELETE_WARNINGS: Record<string, string> = {
  Manual:
    'Удалить методичку из хранилища вместе с разобранными разделами? ' +
    'Проверять по ней заявки больше не получится, уже готовые разборы останутся.',
  GrantApplication:
    'Удалить заявку из хранилища вместе с её разбором? ' +
    'Сообщения в чате останутся, но файл по ссылке из них больше не скачается.',
};

const DELETE_FALLBACK_WARNING = 'Удалить файл из хранилища? Действие необратимо.';

const formatSize = (bytes: number) => {
  if (bytes >= 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} МБ`;
  if (bytes >= 1024) return `${Math.round(bytes / 1024)} КБ`;
  return `${bytes} Б`;
};

const formatDate = (raw: string) => {
  const date = new Date(raw);
  return Number.isNaN(date.getTime())
    ? raw
    : date.toLocaleString('ru-RU', { dateStyle: 'short', timeStyle: 'short' });
};

export function FilesPanel({ notify, onManualsChanged }: Props) {
  const [items, setItems] = useState<StoredDocument[]>([]);
  const [total, setTotal] = useState(0);
  const [kind, setKind] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [downloading, setDownloading] = useState<string | null>(null);
  const [exporting, setExporting] = useState<string | null>(null);
  const [pendingDelete, setPendingDelete] = useState<string | null>(null);
  const [deleting, setDeleting] = useState<string | null>(null);

  const load = useCallback(async (silent = false) => {
    if (!silent) setLoading(true);
    setError(null);
    try {
      const page = await api.documents(kind);
      setItems(page.items);
      setTotal(page.total);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Не удалось загрузить список файлов');
    } finally {
      if (!silent) setLoading(false);
    }
  }, [kind]);

  useEffect(() => {
    void load();
  }, [load]);

  const loadRef = useRef(load);
  loadRef.current = load;

  const running = items.some((item) => item.review && RUNNING_STAGES.includes(item.review.stage));

  useEffect(() => {
    if (!running) return;

    const timer = window.setInterval(() => void loadRef.current(true), REFRESH_MS);
    return () => window.clearInterval(timer);
  }, [running]);

  const download = async (item: StoredDocument) => {
    setDownloading(item.id);
    try {
      await api.downloadDocument(item.id);
    } catch (e) {
      notify('error', e instanceof Error ? e.message : 'Не удалось скачать файл');
    } finally {
      setDownloading(null);
    }
  };

  const downloadReview = async (item: StoredDocument) => {
    const review = item.review;
    if (!review?.chatId || !review.messageId) return;

    setExporting(item.id);
    try {
      await api.downloadReview(review.chatId, review.messageId);
    } catch (e) {
      notify('error', e instanceof Error ? e.message : 'Не удалось скачать разбор');
    } finally {
      setExporting(null);
    }
  };

  const remove = async (item: StoredDocument) => {
    setDeleting(item.id);
    try {
      const removed = await api.deleteDocument(item.id);

      setItems((list) => list.filter((x) => x.id !== item.id));
      setTotal((count) => Math.max(count - 1, 0));

      notify('success', removed.message);

      if (removed.manualRemoved) onManualsChanged?.();
    } catch (e) {
      notify('error', e instanceof Error ? e.message : 'Не удалось удалить файл');
      void load(true);
    } finally {
      setDeleting(null);
      setPendingDelete(null);
    }
  };

  const reviewRunning = (item: StoredDocument) =>
    !!item.review && RUNNING_STAGES.includes(item.review.stage);

  return (
    <div className="letters">
      <div className="letters__inner">
        <header className="letters__head">
          <h1 className="letters__title">Загруженные файлы</h1>
          <p className="letters__sub">
            Методички и заявки, попавшие в хранилище, — в том виде, в котором их загрузили.
            Всего: {total}.
          </p>
        </header>

        <section className="letters__block">
          <div className="assignments__filters">
            <button
              className={`btn btn--ghost${kind === null ? ' btn--active' : ''}`}
              onClick={() => setKind(null)}
            >
              Все
            </button>
            {DOCUMENT_KINDS.map((k) => (
              <button
                key={k.value}
                className={`btn btn--ghost${kind === k.value ? ' btn--active' : ''}`}
                onClick={() => setKind(k.value)}
              >
                {k.label}
              </button>
            ))}
          </div>
        </section>

        {error && (
          <span className="chip chip--warn">
            <span className="chip__text">{error}</span>
          </span>
        )}

        {loading ? (
          <span className="note">Загружаю…</span>
        ) : items.length === 0 ? (
          <span className="note">
            Файлов пока нет. Они появляются, когда вы загружаете методичку или заявку на проверку.
          </span>
        ) : (
          <ul className="assignments">
            {items.map((item) => (
              <li key={item.id} className="assignments__item">
                <div className="assignments__head">
                  <span className="assignments__title">
                    <IconFile size={15} /> {item.fileName ?? 'Без имени'}
                  </span>
                  <span className="assignments__actions">
                    {item.review?.messageId && item.review.chatId && (
                      <button
                        className="btn btn--sm btn--ghost"
                        disabled={exporting === item.id}
                        onClick={() => void downloadReview(item)}
                      >
                        {exporting === item.id ? 'Готовлю…' : 'Разбор .docx'}
                      </button>
                    )}
                    <button
                      className="btn btn--sm btn--ghost"
                      disabled={downloading === item.id}
                      onClick={() => void download(item)}
                    >
                      {downloading === item.id ? 'Готовлю…' : 'Скачать'}
                    </button>
                    <button
                      className="btn btn--sm btn--ghost"
                      disabled={reviewRunning(item) || deleting === item.id}
                      title={
                        reviewRunning(item)
                          ? 'Заявка сейчас в разборе — удалить можно после его конца'
                          : 'Удалить файл из хранилища'
                      }
                      onClick={() => setPendingDelete(item.id)}
                    >
                      Удалить
                    </button>
                  </span>
                </div>

                {pendingDelete === item.id && (
                  <div
                    className="note"
                    role="alertdialog"
                    aria-label="Подтверждение удаления"
                    style={{ display: 'flex', flexDirection: 'column', gap: 8, marginTop: 8 }}
                  >
                    <span>{DELETE_WARNINGS[item.kind] ?? DELETE_FALLBACK_WARNING}</span>
                    <span className="assignments__actions">
                      <button
                        className="btn btn--sm btn--danger"
                        disabled={deleting === item.id}
                        onClick={() => void remove(item)}
                      >
                        {deleting === item.id ? 'Удаляю…' : 'Удалить'}
                      </button>
                      <button
                        className="btn btn--sm btn--ghost"
                        disabled={deleting === item.id}
                        onClick={() => setPendingDelete(null)}
                      >
                        Отмена
                      </button>
                    </span>
                  </div>
                )}

                <div className="assignments__meta">
                  <span className="chip">{DOCUMENT_KIND_LABELS[item.kind] ?? item.kind}</span>
                  <span className="chip">{formatSize(item.sizeBytes)}</span>
                  <span className="chip">{formatDate(item.createdAt)}</span>
                  {item.review && (
                    <span
                      className={`chip${item.review.stage === 'Failed' ? ' chip--warn' : ''}`}
                      title={item.review.detail ?? undefined}
                    >
                      {REVIEW_STAGE_LABELS[item.review.stage] ?? item.review.stage}
                    </span>
                  )}
                  {item.review?.totalScore != null && item.review.maxScore != null && (
                    <span className="chip">
                      {item.review.totalScore} из {item.review.maxScore} баллов
                    </span>
                  )}
                  {!!item.review?.unverifiedCount && (
                    <span className="chip chip--warn" title="Разделы, которые агент не нашёл в заявке">
                      проверить вручную: {item.review.unverifiedCount}
                    </span>
                  )}
                  {item.containsPersonalData && (
                    <span className="chip chip--warn" title="Файл хранится в исходном виде">
                      персональные данные
                    </span>
                  )}
                </div>

                {item.review?.detail && (
                  <span className="note">{item.review.detail}</span>
                )}

                {item.containsPersonalData && (
                  <span className="note">
                    Файл хранится как загружен, с персональными данными. Обезличивание
                    применяется только перед отправкой в модель — в хранилище попадает оригинал.
                  </span>
                )}
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}

import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { api } from '../api/client';
import type { ChatMessage, MessageFile } from '../api/types';
import { IconAlert, IconCheck, IconCopy, IconFile, IconSparkle } from './Icons';
import { Markdown } from './Markdown';

interface Props {
  messages: ChatMessage[];
  aiStatus: string | null;
  busy: boolean;
  loading: boolean;
  chatId: string | null;
  notify: (kind: 'info' | 'success' | 'error', text: string) => void;
}

const isStoredMessage = (id: string) => /^\d+-\d+$/.test(id);

function DownloadButton({ chatId, messageId, notify }: {
  chatId: string;
  messageId: string;
  notify: Props['notify'];
}) {
  const [busy, setBusy] = useState(false);

  const download = async () => {
    setBusy(true);
    try {
      await api.downloadReview(chatId, messageId);
    } catch (e) {
      notify('error', e instanceof Error ? e.message : 'Не удалось выгрузить документ');
    } finally {
      setBusy(false);
    }
  };

  return (
    <button
      className="icon-btn icon-btn--sm"
      onClick={download}
      disabled={busy}
      title={busy ? 'Готовлю файл…' : 'Скачать .docx'}
      aria-label="Скачать разбор в .docx"
    >
      <IconFile size={15} />
    </button>
  );
}

function Attachment({ file, chatId, notify }: {
  file: MessageFile;
  chatId: string | null;
  notify: Props['notify'];
}) {
  const [pending, setPending] = useState<'source' | 'review' | null>(null);

  const run = async (kind: 'source' | 'review', action: () => Promise<void>) => {
    setPending(kind);
    try {
      await action();
    } catch (e) {
      notify('error', e instanceof Error ? e.message : 'Не удалось скачать файл');
    } finally {
      setPending(null);
    }
  };

  const reviewId = file.reviewMessageId;

  return (
    <div className="attachment">
      <span className="attachment__icon" aria-hidden="true">
        <IconFile size={18} />
      </span>

      <div className="attachment__body">
        <div className="attachment__name" title={file.fileName}>
          {file.fileName}
        </div>
        <div className="attachment__meta">
          {reviewId ? 'Заявка проверена' : 'Заявка на проверке'}
        </div>
      </div>

      <div className="attachment__actions">
        <button
          className="btn btn--subtle btn--xs"
          disabled={pending !== null}
          onClick={() => void run('source', () => api.downloadDocument(file.documentId))}
        >
          {pending === 'source' ? 'Скачиваю…' : 'Заявка'}
        </button>

        {reviewId && chatId && (
          <button
            className="btn btn--subtle btn--xs"
            disabled={pending !== null}
            onClick={() => void run('review', () => api.downloadReview(chatId, reviewId))}
          >
            {pending === 'review' ? 'Скачиваю…' : 'Разбор'}
          </button>
        )}
      </div>
    </div>
  );
}

function CopyButton({ text }: { text: string }) {
  const [done, setDone] = useState(false);

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(text);
      setDone(true);
      window.setTimeout(() => setDone(false), 1600);
    } catch {
    }
  };

  return (
    <button
      className="icon-btn icon-btn--sm"
      onClick={copy}
      title={done ? 'Скопировано' : 'Скопировать'}
      aria-label="Скопировать ответ"
    >
      {done ? <IconCheck size={15} /> : <IconCopy size={15} />}
    </button>
  );
}

function Bubble({ message, chatId, notify }: {
  message: ChatMessage;
  chatId: string | null;
  notify: Props['notify'];
}) {
  const isUser = message.author === 'user';

  return (
    <article className={`msg msg--${isUser ? 'user' : 'ai'}`}>
      {!isUser && (
        <span className="avatar avatar--ai" aria-hidden="true">
          <IconSparkle size={16} />
        </span>
      )}

      <div className="msg__body">
        {!isUser && (
          <div className="msg__author">
            {message.authorName}
            {message.failed && (
              <span className="chip chip--warn">
                <IconAlert size={12} />
                <span className="chip__text">не доставлено</span>
              </span>
            )}
          </div>
        )}

        {message.file ? (
          <Attachment file={message.file} chatId={chatId} notify={notify} />
        ) : (
          message.fileName && (
            <div className="msg__file">
              <IconFile size={15} />
              <span>{message.fileName}</span>
            </div>
          )
        )}

        {isUser ? (
          message.content && (
            <div className="bubble">
              <Markdown text={message.content} />
            </div>
          )
        ) : (
          <Markdown text={message.content} />
        )}

        {isUser && message.failed && (
          <div className="msg__author" style={{ justifyContent: 'flex-end', marginTop: 4 }}>
            <span className="chip chip--warn">
              <IconAlert size={12} />
              <span className="chip__text">не отправлено</span>
            </span>
          </div>
        )}

        {!isUser && message.content && (
          <div className="msg__actions">
            <CopyButton text={message.content} />
            {chatId && isStoredMessage(message.id) && (
              <DownloadButton chatId={chatId} messageId={message.id} notify={notify} />
            )}
          </div>
        )}
      </div>
    </article>
  );
}

export function MessageList({ messages, aiStatus, busy, loading, chatId, notify }: Props) {
  const endRef = useRef<HTMLDivElement>(null);
  const scrollRef = useRef<HTMLDivElement>(null);
  const pinnedRef = useRef(true);

  useLayoutEffect(() => {
    const el = scrollRef.current;
    if (!el) return;
    const onScroll = () => {
      pinnedRef.current = el.scrollHeight - el.scrollTop - el.clientHeight < 120;
    };
    el.addEventListener('scroll', onScroll, { passive: true });
    return () => el.removeEventListener('scroll', onScroll);
  }, []);

  useEffect(() => {
    if (pinnedRef.current) {
      endRef.current?.scrollIntoView({ block: 'end', behavior: messages.length > 1 ? 'smooth' : 'auto' });
    }
  }, [messages, aiStatus, busy]);

  return (
    <div className="thread" ref={scrollRef}>
      <div className="thread__inner">
        {loading && <div className="typing">Загружаю историю…</div>}

        {messages.map((m) => (
          <Bubble key={m.id} message={m} chatId={chatId} notify={notify} />
        ))}

        {busy && (
          <div className="msg msg--ai">
            <span className="avatar avatar--ai" aria-hidden="true">
              <IconSparkle size={16} />
            </span>
            <div className="msg__body">
              <div className="typing" role="status" aria-live="polite">
                <span className="typing__dots">
                  <i />
                  <i />
                  <i />
                </span>
                <span>{aiStatus ?? 'Агент думает…'}</span>
              </div>
            </div>
          </div>
        )}

        <div ref={endRef} />
      </div>
    </div>
  );
}

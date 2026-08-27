import { useCallback, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import type { ChatSummary, Identity, ManualData } from '../api/types';
import { MANUAL_STAGE_LABELS } from '../api/types';
import { isStaff, routes } from '../routes';
import type { Section } from '../routes';
import {
  IconBook,
  IconChevron,
  IconLogo,
  IconMessage,
  IconMoon,
  IconPlus,
  IconSearch,
  IconSidebar,
  IconSun,
  IconTrash,
  IconUpload,
} from './Icons';
import type { Theme } from '../hooks/useTheme';

interface Props {
  chats: ChatSummary[];
  staffChats: ChatSummary[];
  activeChatId: string | null;
  manuals: ManualData[];
  manualId: string | null;
  staffChat: boolean;
  identity: Identity;
  view: Section;
  theme: Theme;
  onSelectChat: (id: string) => void;
  onDeleteChat: (id: string) => Promise<void>;
  onNewChat: () => void;
  onNewStaffChat: () => void;
  onSelectManual: (id: string) => void;
  onSelectView: (view: Section) => void;
  onLogin: () => void;
  onLogout: () => void;
  onUploadManual: () => void;
  onSetTheme: (theme: Theme) => void;
  onClose: () => void;
}

const manualOption = (m: ManualData) => (
  <option key={m.id} value={m.id}>
    {m.stage === 'Ready' ? m.title : `${m.title} — ${MANUAL_STAGE_LABELS[m.stage]}`}
  </option>
);

export function Sidebar({
  chats,
  staffChats,
  activeChatId,
  manuals,
  manualId,
  staffChat,
  identity,
  view,
  theme,
  onSelectChat,
  onDeleteChat,
  onNewChat,
  onNewStaffChat,
  onSelectManual,
  onSelectView,
  onLogin,
  onLogout,
  onUploadManual,
  onSetTheme,
  onClose,
}: Props) {
  const [query, setQuery] = useState('');
  const [pendingDelete, setPendingDelete] = useState<string | null>(null);
  const [deleting, setDeleting] = useState<string | null>(null);

  const canUploadManual = isStaff(identity);

  const manualGroups = useMemo(
    () => ({
      staff: manuals.filter((m) => m.scope === 'Staff'),
      common: manuals.filter((m) => m.scope === 'Public'),
    }),
    [manuals],
  );

  const selectedManual = useMemo(
    () => manuals.find((m) => m.id === manualId) ?? null,
    [manuals, manualId],
  );

  const search = useCallback(
    (list: ChatSummary[]) => {
      const q = query.trim().toLowerCase();
      return q ? list.filter((c) => c.title.toLowerCase().includes(q)) : list;
    },
    [query],
  );

  const filtered = useMemo(() => search(chats), [search, chats]);
  const filteredStaff = useMemo(() => search(staffChats), [search, staffChats]);

  const renderSection = (
    label: string,
    all: ChatSummary[],
    shown: ChatSummary[],
    emptyHint: string,
  ) => (
    <>
      <div className="sidebar__label">
        <span>{label}</span>
        {all.length > 0 && <span>{all.length}</span>}
      </div>

      {shown.length === 0 ? (
        <p className="empty-hint">{all.length === 0 ? emptyHint : 'Ничего не найдено.'}</p>
      ) : (
        <ul className="chat-list">
          {shown.map((chat) => (
            <li key={chat.id} className="chat-row">
              <Link
                className="chat-item"
                to={routes.chatById(chat.id)}
                aria-current={chat.id === activeChatId}
                onClick={() => onSelectChat(chat.id)}
                title={chat.title}
              >
                <IconMessage size={15} className="chat-item__icon" />
                <span className="chat-item__title">{chat.title}</span>
              </Link>

              <button
                className="chat-row__delete icon-btn icon-btn--sm"
                onClick={() => setPendingDelete(chat.id)}
                title="Удалить чат"
                aria-label={`Удалить чат «${chat.title}»`}
              >
                <IconTrash size={14} />
              </button>

              {pendingDelete === chat.id && (
                <div className="chat-row__confirm" role="alertdialog" aria-label="Подтверждение удаления">
                  <span className="note">Удалить чат вместе с историей и файлами?</span>
                  <div className="chat-row__confirm-actions">
                    <button
                      className="btn btn--sm btn--danger"
                      disabled={deleting === chat.id}
                      onClick={async () => {
                        setDeleting(chat.id);
                        try {
                          await onDeleteChat(chat.id);
                        } finally {
                          setDeleting(null);
                          setPendingDelete(null);
                        }
                      }}
                    >
                      {deleting === chat.id ? 'Удаляю…' : 'Удалить'}
                    </button>
                    <button className="btn btn--sm btn--ghost" onClick={() => setPendingDelete(null)}>
                      Отмена
                    </button>
                  </div>
                </div>
              )}
            </li>
          ))}
        </ul>
      )}
    </>
  );

  return (
    <aside className="sidebar">
      <div className="sidebar__head">
        <div className="brand">
          <span className="brand__mark">
            <IconLogo size={16} />
          </span>
          <span className="brand__name">ИИ-помощник проректора</span>
        </div>
        <button className="icon-btn" onClick={onClose} title="Скрыть панель" aria-label="Скрыть панель">
          <IconSidebar />
        </button>
      </div>

      <div className="sidebar__block">
        <button className="btn btn--primary btn--block" onClick={onNewChat}>
          <IconPlus size={16} />
          Новый чат
        </button>
        {canUploadManual && (
          <button className="btn btn--ghost btn--block" onClick={onNewStaffChat}>
            <IconPlus size={15} />
            Служебный чат
          </button>
        )}
      </div>

      {(identity.role === 'Rector' || identity.role === 'Coordinator') && (
        <div className="sidebar__block" style={{ paddingTop: 0 }}>
          <div className="views" role="tablist" aria-label="Раздел">
            <Link
              className="view-tab"
              role="tab"
              to={routes.chat}
              aria-selected={view === 'chat'}
              onClick={() => onSelectView('chat')}
            >
              Заявки
            </Link>
            {identity.role === 'Rector' && (
              <Link
                className="view-tab"
                role="tab"
                to={routes.letters}
                aria-selected={view === 'letters'}
                onClick={() => onSelectView('letters')}
              >
                Письма
              </Link>
            )}
            <Link
              className="view-tab"
              role="tab"
              to={routes.assignments}
              aria-selected={view === 'assignments'}
              onClick={() => onSelectView('assignments')}
            >
              Поручения
            </Link>
            <Link
              className="view-tab"
              role="tab"
              to={routes.files}
              aria-selected={view === 'files'}
              onClick={() => onSelectView('files')}
            >
              Файлы
            </Link>
            <Link
              className="view-tab"
              role="tab"
              to={routes.usage}
              aria-selected={view === 'usage'}
              onClick={() => onSelectView('usage')}
            >
              Токены
            </Link>
            {identity.role === 'Rector' && (
              <Link
                className="view-tab"
                role="tab"
                to={routes.accounts}
                aria-selected={view === 'accounts'}
                onClick={() => onSelectView('accounts')}
              >
                Учётки
              </Link>
            )}
            {identity.role === 'Rector' && (
              <Link
                className="view-tab"
                role="tab"
                to={routes.organization}
                aria-selected={view === 'organization'}
                onClick={() => onSelectView('organization')}
              >
                Организация
              </Link>
            )}
          </div>
        </div>
      )}

      <div className="search">
        <IconSearch size={15} className="search__icon" />
        <input
          className="search__input"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Поиск по чатам"
          aria-label="Поиск по чатам"
        />
      </div>

      <div className="sidebar__body">
        {renderSection('Чаты по заявкам', chats, filtered, 'Пока пусто. Начните новый чат.')}

        {canUploadManual &&
          renderSection(
            'Служебные чаты',
            staffChats,
            filteredStaff,
            'Служебных чатов пока нет.',
          )}

        <div className="sidebar__label">
          <span>База знаний</span>
        </div>

        <div className="sidebar__block">
          <div className="field">
            <div className="select">
              <select
                value={manualId ?? ''}
                onChange={(e) => onSelectManual(e.target.value)}
                aria-label="Документ базы знаний"
                disabled={manuals.length === 0}
              >
                {manuals.length === 0 ? (
                  <option value="">Нет загруженных документов</option>
                ) : staffChat ? (
                  <>
                    {manualGroups.staff.length > 0 && (
                      <optgroup label="Служебные">
                        {manualGroups.staff.map(manualOption)}
                      </optgroup>
                    )}
                    {manualGroups.common.length > 0 && (
                      <optgroup label="Общие">
                        {manualGroups.common.map(manualOption)}
                      </optgroup>
                    )}
                  </>
                ) : (
                  manuals.map(manualOption)
                )}
              </select>
              <IconChevron className="select__chevron" />
            </div>

            {selectedManual && selectedManual.stage !== 'Ready' && (
              <span
                className="note"
                style={
                  selectedManual.stage === 'Partial' || selectedManual.stage === 'Failed'
                    ? { color: 'var(--danger)' }
                    : undefined
                }
              >
                {selectedManual.detail ??
                  `Положение ${MANUAL_STAGE_LABELS[selectedManual.stage] ?? selectedManual.stage}`}
              </span>
            )}

            {canUploadManual ? (
              <button className="btn btn--ghost btn--block" onClick={onUploadManual}>
                <IconUpload size={15} />
                Загрузить положение
              </button>
            ) : (
              <span className="note">
                Загружать положения могут только сотрудники. Войдите под служебным аккаунтом —
                проверить свою заявку можно и в гостевом режиме.
              </span>
            )}
          </div>
        </div>

      </div>

      <div className="sidebar__foot">
        <div className="theme-switch" role="group" aria-label="Тема оформления">
          <button
            className="theme-switch__opt"
            aria-pressed={theme === 'light'}
            onClick={() => onSetTheme('light')}
          >
            <IconSun size={15} />
            День
          </button>
          <button
            className="theme-switch__opt"
            aria-pressed={theme === 'dark'}
            onClick={() => onSetTheme('dark')}
          >
            <IconMoon size={15} />
            Ночь
          </button>
        </div>

        <button
          className="btn btn--ghost btn--block"
          onClick={identity.role === 'Guest' || identity.role === null ? onLogin : onLogout}
        >
          {identity.role === 'Guest' || identity.role === null ? 'Войти как сотрудник' : 'Выйти'}
        </button>

        <div className="user-card">
          <span className="avatar">
            <IconBook size={15} />
          </span>
          <div className="user-card__meta">
            <div className="user-card__name">
              {identity.role === 'Guest' ? 'Гостевая сессия' : (identity.name ?? 'Сессия')}
              {identity.role === 'Rector' && <span className="user-card__badge">проректор</span>}
            </div>
            <div className="user-card__sub" title={identity.userId ?? ''}>
              {identity.userId ? `id ${identity.userId.slice(0, 8)}` : 'подключение…'}
            </div>
          </div>
        </div>
      </div>
    </aside>
  );
}

import type { ConnectionState } from '../api/types';
import { IconBook, IconMoon, IconSidebar, IconSun } from './Icons';
import type { Theme } from '../hooks/useTheme';

interface Props {
  title: string;
  manualTitle: string | null;
  connection: ConnectionState;
  theme: Theme;
  sidebarHidden: boolean;
  onToggleSidebar: () => void;
  onToggleTheme: () => void;
}

const CONNECTION_LABEL: Record<ConnectionState, string> = {
  idle: 'Не подключено',
  connecting: 'Подключение…',
  online: 'На связи',
  offline: 'Нет связи',
};

export function Topbar({
  title,
  manualTitle,
  connection,
  theme,
  sidebarHidden,
  onToggleSidebar,
  onToggleTheme,
}: Props) {
  return (
    <header className="topbar">
      {sidebarHidden && (
        <button className="icon-btn" onClick={onToggleSidebar} aria-label="Показать панель">
          <IconSidebar />
        </button>
      )}
      <button className="icon-btn only-mobile" onClick={onToggleSidebar} aria-label="Меню">
        <IconSidebar />
      </button>

      <h1 className="topbar__title">{title}</h1>

      {manualTitle && (
        <span className="chip" title={`Проверка по: ${manualTitle}`}>
          <IconBook size={13} />
          <span className="chip__text">{manualTitle}</span>
        </span>
      )}

      <span className="chip" title={CONNECTION_LABEL[connection]}>
        <span className={`dot dot--${connection === 'idle' ? 'connecting' : connection}`} />
        <span className="chip__text">{CONNECTION_LABEL[connection]}</span>
      </span>

      <button
        className="icon-btn"
        onClick={onToggleTheme}
        aria-label={theme === 'dark' ? 'Включить светлую тему' : 'Включить тёмную тему'}
        title={theme === 'dark' ? 'Светлая тема' : 'Тёмная тема'}
      >
        {theme === 'dark' ? <IconSun /> : <IconMoon />}
      </button>
    </header>
  );
}

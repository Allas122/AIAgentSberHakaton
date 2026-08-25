import { useCallback, useEffect, useState } from 'react';
import { api } from '../api/client';
import type { Account } from '../api/types';
import { ASSIGNABLE_ROLES, ROLE_LABELS } from '../api/types';
import { IconClose, IconSend } from './Icons';

interface Props {
  notify: (kind: 'info' | 'success' | 'error', text: string) => void;
}

const empty = { login: '', password: '', name: '', email: '', role: 'Coordinator' };

export function AccountsPanel({ notify }: Props) {
  const [items, setItems] = useState<Account[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const [form, setForm] = useState(empty);
  const [resetFor, setResetFor] = useState<string | null>(null);
  const [newPassword, setNewPassword] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setItems(await api.accounts());
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Не удалось загрузить учётные записи');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const create = async () => {
    if (!form.login.trim() || !form.password || !form.name.trim() || creating) return;

    setCreating(true);
    try {
      await api.createAccount({
        login: form.login.trim(),
        password: form.password,
        name: form.name.trim(),
        email: form.email.trim() || undefined,
        role: form.role,
      });
      setForm(empty);
      notify('success', 'Учётная запись создана');
      await load();
    } catch (e) {
      notify('error', e instanceof Error ? e.message : 'Не удалось создать учётную запись');
    } finally {
      setCreating(false);
    }
  };

  const changeRole = async (account: Account, role: string) => {
    const previous = items;
    setItems((list) => list.map((x) => (x.id === account.id ? { ...x, role } : x)));
    try {
      await api.updateAccount(account.id, { role });
      notify('success', `Роль изменена: ${ROLE_LABELS[role] ?? role}`);
    } catch (e) {
      setItems(previous);
      notify('error', e instanceof Error ? e.message : 'Не удалось изменить роль');
    }
  };

  const resetPassword = async (account: Account) => {
    if (newPassword.length < 8) {
      notify('error', 'Пароль короче 8 символов');
      return;
    }
    try {
      await api.updateAccount(account.id, { password: newPassword });
      notify('success', `Пароль для «${account.login}» изменён`);
      setResetFor(null);
      setNewPassword('');
    } catch (e) {
      notify('error', e instanceof Error ? e.message : 'Не удалось изменить пароль');
    }
  };

  const remove = async (account: Account) => {
    const previous = items;
    setItems((list) => list.filter((x) => x.id !== account.id));
    try {
      await api.deleteAccount(account.id);
      notify('success', `Учётная запись «${account.login}» удалена`);
    } catch (e) {
      setItems(previous);
      notify('error', e instanceof Error ? e.message : 'Не удалось удалить учётную запись');
    }
  };

  return (
    <div className="letters">
      <div className="letters__inner">
        <header className="letters__head">
          <h1 className="letters__title">Учётные записи</h1>
          <p className="letters__sub">
            Здесь заводятся координаторы и сотрудники. Гостевые сессии сюда не попадают —
            они создаются сами и живут сутки.
          </p>
        </header>

        <section className="letters__block">
          <span className="label">Создать учётную запись</span>

          <div className="assignments__row">
            <input
              className="letters__area letters__area--short"
              value={form.login}
              disabled={creating}
              placeholder="Логин (латиница)"
              onChange={(e) => setForm({ ...form, login: e.target.value })}
              aria-label="Логин"
            />
            <input
              className="letters__area letters__area--short"
              type="password"
              value={form.password}
              disabled={creating}
              placeholder="Пароль, минимум 8 символов"
              onChange={(e) => setForm({ ...form, password: e.target.value })}
              aria-label="Пароль"
            />
          </div>

          <div className="assignments__row">
            <input
              className="letters__area letters__area--short"
              value={form.name}
              disabled={creating}
              placeholder="Имя и фамилия"
              onChange={(e) => setForm({ ...form, name: e.target.value })}
              aria-label="Имя"
            />
            <input
              className="letters__area letters__area--short"
              value={form.email}
              disabled={creating}
              placeholder="Почта, необязательно"
              onChange={(e) => setForm({ ...form, email: e.target.value })}
              aria-label="Почта"
            />
          </div>

          <div className="assignments__filters">
            {ASSIGNABLE_ROLES.map((r) => (
              <button
                key={r.value}
                className={`btn btn--ghost${form.role === r.value ? ' btn--active' : ''}`}
                onClick={() => setForm({ ...form, role: r.value })}
              >
                {r.label}
              </button>
            ))}
          </div>

          <div className="letters__actions">
            <button
              className="btn btn--primary"
              onClick={create}
              disabled={creating || !form.login.trim() || !form.password || !form.name.trim()}
            >
              <IconSend size={15} />
              {creating ? 'Создаю…' : 'Создать'}
            </button>
          </div>
        </section>

        {error && (
          <span className="chip chip--warn">
            <span className="chip__text">{error}</span>
          </span>
        )}

        {loading ? (
          <span className="note">Загружаю…</span>
        ) : (
          <ul className="assignments">
            {items.map((account) => (
              <li key={account.id} className="assignments__item">
                <div className="assignments__head">
                  <span className="assignments__title">
                    {account.name} — {account.login}
                    {account.isSelf && <span className="chip chip--accent">это вы</span>}
                  </span>
                  {!account.isSelf && (
                    <button
                      className="icon-btn icon-btn--sm"
                      onClick={() => void remove(account)}
                      aria-label={`Удалить учётную запись ${account.login}`}
                      title="Удалить"
                    >
                      <IconClose size={13} />
                    </button>
                  )}
                </div>

                <div className="assignments__meta">
                  <span className="chip">{ROLE_LABELS[account.role] ?? account.role}</span>
                  {account.email && <span className="chip">{account.email}</span>}
                </div>

                <div className="assignments__statuses">
                  {ASSIGNABLE_ROLES.map((r) => (
                    <button
                      key={r.value}
                      className={`btn btn--ghost btn--sm${account.role === r.value ? ' btn--active' : ''}`}
                      disabled={account.role === r.value || (account.isSelf && r.value !== 'Rector')}
                      onClick={() => void changeRole(account, r.value)}
                    >
                      {r.label}
                    </button>
                  ))}

                  <button
                    className="btn btn--ghost btn--sm"
                    onClick={() => {
                      setResetFor(resetFor === account.id ? null : account.id);
                      setNewPassword('');
                    }}
                  >
                    Сменить пароль
                  </button>
                </div>

                {resetFor === account.id && (
                  <div className="assignments__row">
                    <input
                      className="letters__area letters__area--short"
                      type="password"
                      value={newPassword}
                      placeholder="Новый пароль, минимум 8 символов"
                      onChange={(e) => setNewPassword(e.target.value)}
                      aria-label="Новый пароль"
                    />
                    <button className="btn btn--primary btn--sm" onClick={() => void resetPassword(account)}>
                      Сохранить
                    </button>
                  </div>
                )}
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}

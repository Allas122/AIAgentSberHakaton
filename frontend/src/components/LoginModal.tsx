import { useEffect, useRef, useState } from 'react';
import type { RegisterInput } from '../api/client';

interface Props {
  onClose: () => void;
  onSubmit: (login: string, password: string) => Promise<void>;
  onRegister: (input: RegisterInput) => Promise<void>;
}

type Mode = 'login' | 'register';

const MIN_LOGIN = 3;
const MIN_PASSWORD = 8;

export function LoginModal({ onClose, onSubmit, onRegister }: Props) {
  const [mode, setMode] = useState<Mode>('login');
  const [login, setLogin] = useState('');
  const [password, setPassword] = useState('');
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loginRef = useRef<HTMLInputElement>(null);
  const registering = mode === 'register';

  useEffect(() => {
    loginRef.current?.focus();
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !busy) onClose();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [busy, onClose]);

  const switchTo = (next: Mode) => {
    if (busy) return;
    setMode(next);
    setError(null);
    setPassword('');
    loginRef.current?.focus();
  };

  const canSubmit =
    !busy &&
    login.trim().length >= (registering ? MIN_LOGIN : 1) &&
    password.length >= (registering ? MIN_PASSWORD : 1) &&
    (!registering || name.trim().length > 0);

  const submit = async () => {
    if (!canSubmit) return;
    setBusy(true);
    setError(null);
    try {
      if (registering) {
        await onRegister({
          login: login.trim(),
          password,
          name: name.trim(),
          email: email.trim() || undefined,
        });
      } else {
        await onSubmit(login.trim(), password);
      }
      onClose();
    } catch (e) {
      setError(
        e instanceof Error
          ? e.message
          : registering
            ? 'Не удалось создать учётную запись'
            : 'Не удалось войти',
      );
      setBusy(false);
    }
  };

  return (
    <>
      <div className="overlay" onClick={() => !busy && onClose()} />
      <div
        className="modal"
        role="dialog"
        aria-modal="true"
        aria-label={registering ? 'Регистрация' : 'Вход в аккаунт'}
      >
        <h2 className="modal__title">{registering ? 'Регистрация' : 'Вход'}</h2>
        <p className="modal__sub">
          {registering
            ? 'Учётная запись сохраняет ваши чаты и проверенные заявки между сессиями. ' +
              'Набор возможностей тот же, что и в гостевом режиме.'
            : 'Без входа система работает в гостевом режиме: чаты и заявки живут сутки и не переносятся ' +
              'на другое устройство.'}
        </p>

        <div className="views" role="tablist" aria-label="Режим">
          <button
            type="button"
            className="view-tab"
            role="tab"
            aria-selected={!registering}
            disabled={busy}
            onClick={() => switchTo('login')}
          >
            Вход
          </button>
          <button
            type="button"
            className="view-tab"
            role="tab"
            aria-selected={registering}
            disabled={busy}
            onClick={() => switchTo('register')}
          >
            Регистрация
          </button>
        </div>

        <form
          className="field"
          style={{ gap: 12 }}
          onSubmit={(e) => {
            e.preventDefault();
            void submit();
          }}
        >
          <div className="field">
            <label className="label" htmlFor="login-name">
              Логин
            </label>
            <input
              id="login-name"
              ref={loginRef}
              className="input"
              value={login}
              disabled={busy}
              autoComplete="username"
              onChange={(e) => setLogin(e.target.value)}
            />
            {registering && (
              <p className="modal__sub" style={{ margin: 0 }}>
                Латиница, цифры, точка, дефис и подчёркивание, от {MIN_LOGIN} символов.
              </p>
            )}
          </div>

          {registering && (
            <div className="field">
              <label className="label" htmlFor="register-display-name">
                Как к вам обращаться
              </label>
              <input
                id="register-display-name"
                className="input"
                value={name}
                disabled={busy}
                autoComplete="name"
                onChange={(e) => setName(e.target.value)}
              />
            </div>
          )}

          <div className="field">
            <label className="label" htmlFor="login-password">
              Пароль
            </label>
            <input
              id="login-password"
              className="input"
              type="password"
              value={password}
              disabled={busy}
              autoComplete={registering ? 'new-password' : 'current-password'}
              onChange={(e) => setPassword(e.target.value)}
            />
            {registering && (
              <p className="modal__sub" style={{ margin: 0 }}>
                От {MIN_PASSWORD} символов.
              </p>
            )}
          </div>

          {registering && (
            <div className="field">
              <label className="label" htmlFor="register-email">
                Почта, если нужна
              </label>
              <input
                id="register-email"
                className="input"
                type="email"
                value={email}
                disabled={busy}
                autoComplete="email"
                onChange={(e) => setEmail(e.target.value)}
              />
            </div>
          )}

          {error && (
            <p className="modal__sub" style={{ color: 'var(--danger)', margin: 0 }}>
              {error}
            </p>
          )}

          <div className="modal__actions">
            <button type="button" className="btn btn--subtle" onClick={onClose} disabled={busy}>
              Отмена
            </button>
            <button type="submit" className="btn btn--primary" disabled={!canSubmit}>
              {busy
                ? registering
                  ? 'Создаю…'
                  : 'Вхожу…'
                : registering
                  ? 'Зарегистрироваться'
                  : 'Войти'}
            </button>
          </div>
        </form>
      </div>
    </>
  );
}

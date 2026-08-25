import { useCallback, useEffect, useState } from 'react';
import { api } from '../api/client';
import type { Account, Assignment, AssignmentStatus } from '../api/types';
import { ASSIGNMENT_SOURCES, ASSIGNMENT_STATUSES } from '../api/types';
import { IconClose, IconSend } from './Icons';

interface Props {
  notify: (kind: 'info' | 'success' | 'error', text: string) => void;
}

const NO_CURATOR = '00000000-0000-0000-0000-000000000000';

const curatorLabel = (account: Account) =>
  account.role === 'Rector' ? `${account.name} (проректор)` : account.name;

const statusLabel = (status: AssignmentStatus) =>
  ASSIGNMENT_STATUSES.find((s) => s.value === status)?.label ?? status;

const isOverdue = (item: Assignment) =>
  item.dueDate !== null &&
  item.status !== 'Done' &&
  item.status !== 'Cancelled' &&
  new Date(item.dueDate).getTime() < Date.now();

const formatDate = (raw: string | null) => {
  if (!raw) return null;
  const date = new Date(raw);
  return Number.isNaN(date.getTime()) ? raw : date.toLocaleDateString('ru-RU');
};

export function AssignmentsPanel({ notify }: Props) {
  const [items, setItems] = useState<Assignment[]>([]);
  const [total, setTotal] = useState(0);
  const [filter, setFilter] = useState<AssignmentStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [assigneeId, setAssigneeId] = useState('');
  const [dueDate, setDueDate] = useState('');
  const [curators, setCurators] = useState<Account[]>([]);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const page = await api.assignments(filter);
      setItems(page.items);
      setTotal(page.total);
    } catch (e) {
      const text = e instanceof Error ? e.message : 'Не удалось загрузить поручения';
      setError(text);
    } finally {
      setLoading(false);
    }
  }, [filter]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    const controller = new AbortController();

    api
      .curators(controller.signal)
      .then(setCurators)
      .catch(() => setCurators([]));

    return () => controller.abort();
  }, []);

  const reassign = async (item: Assignment, nextId: string) => {
    const previous = items;
    const curator = curators.find((c) => c.id === nextId) ?? null;

    setItems((current) =>
      current.map((x) =>
        x.id === item.id
          ? { ...x, assigneeId: curator?.id ?? null, assignee: curator?.name ?? null }
          : x,
      ),
    );

    try {
      await api.updateAssignment(item.id, { assigneeId: nextId || NO_CURATOR });
    } catch (e) {
      setItems(previous);
      notify('error', e instanceof Error ? e.message : 'Не удалось сменить исполнителя');
    }
  };

  const changeStatus = async (item: Assignment, status: AssignmentStatus) => {
    const previous = items;
    setItems((current) => current.map((x) => (x.id === item.id ? { ...x, status } : x)));
    try {
      await api.updateAssignment(item.id, { status });
    } catch (e) {
      setItems(previous);
      notify('error', e instanceof Error ? e.message : 'Не удалось изменить статус');
    }
  };

  const remove = async (item: Assignment) => {
    const previous = items;
    setItems((current) => current.filter((x) => x.id !== item.id));
    setTotal((n) => Math.max(n - 1, 0));
    try {
      await api.deleteAssignment(item.id);
      notify('success', 'Поручение удалено');
    } catch (e) {
      setItems(previous);
      setTotal((n) => n + 1);
      notify('error', e instanceof Error ? e.message : 'Не удалось удалить поручение');
    }
  };

  const create = async () => {
    if (!title.trim() || creating) return;
    setCreating(true);
    try {
      await api.createAssignment({
        title: title.trim(),
        description: description.trim(),
        assigneeId: assigneeId || null,
        dueDate: dueDate || undefined,
      });
      setTitle('');
      setDescription('');
      setAssigneeId('');
      setDueDate('');
      notify('success', 'Поручение заведено');
      await load();
    } catch (e) {
      notify('error', e instanceof Error ? e.message : 'Не удалось завести поручение');
    } finally {
      setCreating(false);
    }
  };

  return (
    <div className="letters">
      <div className="letters__inner">
        <header className="letters__head">
          <h1 className="letters__title">Поручения</h1>
          <p className="letters__sub">
            Поручения заводятся автоматически при разборе входящих писем и вручную здесь.
            Всего: {total}.
          </p>
        </header>

        <section className="letters__block">
          <span className="label">Завести поручение</span>
          <input
            className="letters__area letters__area--short"
            value={title}
            disabled={creating}
            placeholder="Что нужно сделать"
            onChange={(e) => setTitle(e.target.value)}
            aria-label="Формулировка поручения"
          />
          <textarea
            className="letters__area letters__area--short"
            rows={2}
            value={description}
            disabled={creating}
            placeholder="Подробности, условия, основание"
            onChange={(e) => setDescription(e.target.value)}
            aria-label="Описание поручения"
          />
          <div className="assignments__row">
            <select
              className="letters__area letters__area--short"
              value={assigneeId}
              disabled={creating || curators.length === 0}
              onChange={(e) => setAssigneeId(e.target.value)}
              aria-label="Куратор-исполнитель"
            >
              <option value="">
                {curators.length === 0 ? 'Кураторов нет' : 'Без исполнителя'}
              </option>
              {curators.map((curator) => (
                <option key={curator.id} value={curator.id}>
                  {curatorLabel(curator)}
                </option>
              ))}
            </select>
            <input
              className="letters__area letters__area--short"
              type="date"
              value={dueDate}
              disabled={creating}
              onChange={(e) => setDueDate(e.target.value)}
              aria-label="Срок"
            />
          </div>
          <div className="letters__actions">
            <button
              className="btn btn--primary"
              onClick={create}
              disabled={!title.trim() || creating}
            >
              <IconSend size={15} />
              {creating ? 'Завожу…' : 'Завести'}
            </button>
          </div>
        </section>

        <section className="letters__block">
          <div className="assignments__filters">
            <button
              className={`btn btn--ghost${filter === null ? ' btn--active' : ''}`}
              onClick={() => setFilter(null)}
            >
              Все
            </button>
            {ASSIGNMENT_STATUSES.map((s) => (
              <button
                key={s.value}
                className={`btn btn--ghost${filter === s.value ? ' btn--active' : ''}`}
                onClick={() => setFilter(s.value)}
              >
                {s.label}
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
            Поручений нет. Загрузите входящее письмо в разделе «Деловая переписка» — система заведёт
            их сама.
          </span>
        ) : (
          <ul className="assignments">
            {items.map((item) => (
              <li key={item.id} className="assignments__item">
                <div className="assignments__head">
                  <span className="assignments__title">{item.title}</span>
                  <button
                    className="icon-btn icon-btn--sm"
                    onClick={() => void remove(item)}
                    aria-label="Удалить поручение"
                  >
                    <IconClose size={13} />
                  </button>
                </div>

                {item.description && <p className="assignments__desc">{item.description}</p>}

                <div className="assignments__meta">
                  {curators.length > 0 ? (
                    <select
                      className="assignments__curator"
                      value={item.assigneeId ?? ''}
                      onChange={(e) => void reassign(item, e.target.value)}
                      aria-label="Куратор-исполнитель"
                    >
                      <option value="">
                        {item.assigneeId === null && item.assignee
                          ? `${item.assignee} — вне списка`
                          : 'Без исполнителя'}
                      </option>
                      {curators.map((curator) => (
                        <option key={curator.id} value={curator.id}>
                          {curatorLabel(curator)}
                        </option>
                      ))}
                    </select>
                  ) : (
                    item.assignee && <span className="chip">{item.assignee}</span>
                  )}
                  {item.dueDate && (
                    <span className={`chip${isOverdue(item) ? ' chip--warn' : ''}`}>
                      до {formatDate(item.dueDate)}
                      {isOverdue(item) ? ' — просрочено' : ''}
                    </span>
                  )}
                  <span className="chip">{ASSIGNMENT_SOURCES[item.sourceKind] ?? item.sourceKind}</span>
                </div>

                <div className="assignments__statuses">
                  {ASSIGNMENT_STATUSES.map((s) => (
                    <button
                      key={s.value}
                      className={`btn btn--ghost btn--sm${item.status === s.value ? ' btn--active' : ''}`}
                      disabled={item.status === s.value}
                      onClick={() => void changeStatus(item, s.value)}
                    >
                      {s.label}
                    </button>
                  ))}
                </div>

                <span className="note">Статус: {statusLabel(item.status)}</span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}

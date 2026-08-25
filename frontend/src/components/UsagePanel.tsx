import { useCallback, useEffect, useState } from 'react';
import { api } from '../api/client';
import type { UsageRecordPage, UsageReport } from '../api/types';
import { USAGE_OPERATIONS } from '../api/types';

const PAGE_SIZE = 25;

interface Props {
  notify: (kind: 'info' | 'success' | 'error', text: string) => void;
}

const PERIODS = [
  { days: 1, label: 'Сутки' },
  { days: 7, label: 'Неделя' },
  { days: 30, label: 'Месяц' },
];

const formatNumber = (value: number) => value.toLocaleString('ru-RU');

const formatDuration = (raw: string) => {
  const match = /^(?:(\d+)\.)?(\d{2}):(\d{2}):(\d{2})(?:\.(\d+))?$/.exec(raw);
  if (!match) return raw;

  const [, days, hours, minutes, seconds, fraction] = match;
  const total =
    Number(days ?? 0) * 86400 +
    Number(hours) * 3600 +
    Number(minutes) * 60 +
    Number(seconds) +
    Number(`0.${fraction ?? 0}`);

  if (total < 60) return `${total.toFixed(1)} с`;
  if (total < 3600) return `${Math.floor(total / 60)} мин ${Math.round(total % 60)} с`;

  return `${Math.floor(total / 3600)} ч ${Math.round((total % 3600) / 60)} мин`;
};

const operationLabel = (operation: string) => USAGE_OPERATIONS[operation] ?? operation;

const formatTime = (raw: string) => {
  const date = new Date(raw);
  return Number.isNaN(date.getTime())
    ? raw
    : date.toLocaleString('ru-RU', { dateStyle: 'short', timeStyle: 'medium' });
};

export function UsagePanel({ notify }: Props) {
  const [report, setReport] = useState<UsageReport | null>(null);
  const [days, setDays] = useState(7);
  const [loading, setLoading] = useState(true);

  const [page, setPage] = useState<UsageRecordPage | null>(null);
  const [operation, setOperation] = useState<string | null>(null);
  const [offset, setOffset] = useState(0);
  const [loadingPage, setLoadingPage] = useState(false);
  const [clearing, setClearing] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setReport(await api.usage(days));
    } catch (e) {
      notify('error', e instanceof Error ? e.message : 'Не удалось загрузить расход');
    } finally {
      setLoading(false);
    }
  }, [days, notify]);

  const loadPage = useCallback(async () => {
    setLoadingPage(true);
    try {
      setPage(await api.usageRecords({ days, operation, limit: PAGE_SIZE, offset }));
    } catch (e) {
      notify('error', e instanceof Error ? e.message : 'Не удалось загрузить список вызовов');
    } finally {
      setLoadingPage(false);
    }
  }, [days, operation, offset, notify]);

  const clear = useCallback(async () => {
    const period = PERIODS.find((item) => item.days === days)?.label.toLowerCase() ?? `${days} дн.`;
    if (!window.confirm(`Удалить записи о расходе за ${period}? Действие необратимо.`)) return;

    setClearing(true);
    try {
      const { removed } = await api.clearUsage(days);

      notify('success', removed === 0 ? 'Записей за период не было' : `Удалено записей: ${removed}`);

      setOffset(0);
      await Promise.all([load(), loadPage()]);
    } catch (e) {
      notify('error', e instanceof Error ? e.message : 'Не удалось очистить журнал');
    } finally {
      setClearing(false);
    }
  }, [days, load, loadPage, notify]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    void loadPage();
  }, [loadPage]);

  useEffect(() => {
    setOffset(0);
  }, [days, operation]);

  const rows = report?.rows ?? [];
  const records = page?.items ?? [];
  const total = page?.total ?? 0;
  const shownFrom = total === 0 ? 0 : offset + 1;
  const shownTo = Math.min(offset + PAGE_SIZE, total);

  return (
    <div className="letters">
      <div className="letters__inner">
        <header className="letters__head">
          <h1 className="letters__title">Расход токенов</h1>
          <p className="letters__sub">
            Сколько токенов и времени уходит на каждое действие агентов. Считается по ответам
            GigaChat, отдельной строкой — списание с баланса.
          </p>
        </header>

        <section className="letters__block">
          <div className="assignments__filters">
            {PERIODS.map((period) => (
              <button
                key={period.days}
                className={`btn btn--ghost${days === period.days ? ' btn--active' : ''}`}
                onClick={() => setDays(period.days)}
              >
                {period.label}
              </button>
            ))}
            <button className="btn btn--ghost" onClick={() => void load()} disabled={loading}>
              Обновить
            </button>
            <button
              className="btn btn--ghost"
              onClick={() => void clear()}
              disabled={clearing || loading}
            >
              {clearing ? 'Очищаю…' : 'Очистить'}
            </button>
          </div>
        </section>

        {loading ? (
          <span className="note">Считаю…</span>
        ) : rows.length === 0 ? (
          <span className="note">
            За выбранный период запросов к модели не было — считать пока нечего.
          </span>
        ) : (
          <>
            <section className="letters__block">
              <div className="usage__totals">
                <span className="chip">
                  Всего токенов: {formatNumber(report?.totalTokens ?? 0)}
                  {report?.hasPartial ? ' и более' : ''}
                </span>
                <span className="chip">Время: {formatDuration(report?.totalDuration ?? '')}</span>
                {(report?.balanceSpent ?? 0) > 0 && (
                  <span className="chip">
                    Списано с баланса: {formatNumber(Math.round(report?.balanceSpent ?? 0))}
                  </span>
                )}
              </div>
            </section>

            <div className="usage__scroll">
              <table className="usage">
                <thead>
                  <tr>
                    <th>Действие</th>
                    <th>Модель</th>
                    <th>Запросов</th>
                    <th>Промпт</th>
                    <th>Ответ</th>
                    <th>Всего</th>
                    <th>Время</th>
                    <th>Самый долгий</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((row) => (
                    <tr key={`${row.operation}:${row.model}`}>
                      <td>
                        {operationLabel(row.operation)}
                        {row.failedCalls > 0 && (
                          <span className="note"> — с ошибкой: {row.failedCalls}</span>
                        )}
                      </td>
                      <td>{row.model}</td>
                      <td>{formatNumber(row.calls)}</td>
                      <td>{formatNumber(row.promptTokens)}</td>
                      <td>{formatNumber(row.completionTokens)}</td>
                      <td>
                        {row.balanceSpent > 0
                          ? `${formatNumber(Math.round(row.balanceSpent))} с баланса`
                          : `${formatNumber(row.totalTokens)}${row.partialCalls > 0 ? '+' : ''}`}
                      </td>
                      <td>{formatDuration(row.duration)}</td>
                      <td>{formatDuration(row.slowest)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {report?.hasPartial && (
              <span className="note">
                Знак «+» значит, что цифра неполная: в действиях с инструментами библиотека отдаёт
                расход только по последнему запросу, промежуточные вызовы в него не входят.
                Строка «Разбор заявки целиком» показывает реальное списание с баланса — по ней и
                сверяйтесь.
              </span>
            )}
          </>
        )}

        <header className="letters__head">
          <h2 className="letters__title">Отдельные вызовы</h2>
          <p className="letters__sub">
            {total === 0
              ? 'За выбранный период вызовов нет.'
              : `Показаны ${shownFrom}–${shownTo} из ${total}.`}
          </p>
        </header>

        <section className="letters__block">
          <div className="assignments__filters">
            <button
              className={`btn btn--ghost${operation === null ? ' btn--active' : ''}`}
              onClick={() => setOperation(null)}
            >
              Все действия
            </button>
            {rows.map((row) => row.operation).filter((value, index, all) => all.indexOf(value) === index).map((value) => (
              <button
                key={value}
                className={`btn btn--ghost${operation === value ? ' btn--active' : ''}`}
                onClick={() => setOperation(value)}
              >
                {operationLabel(value)}
              </button>
            ))}
          </div>
        </section>

        {records.length > 0 && (
          <div className="usage__scroll">
            <table className="usage">
              <thead>
                <tr>
                  <th>Время</th>
                  <th>Действие</th>
                  <th>Модель</th>
                  <th>Промпт</th>
                  <th>Ответ</th>
                  <th>Всего</th>
                  <th>Вызовов</th>
                  <th>Длительность</th>
                </tr>
              </thead>
              <tbody>
                {records.map((record) => (
                  <tr key={`${record.startedAt}:${record.operation}:${record.model}`}>
                    <td>{formatTime(record.startedAt)}</td>
                    <td>
                      {operationLabel(record.operation)}
                      {record.failed && <span className="note"> — ошибка</span>}
                    </td>
                    <td>{record.model}</td>
                    <td>{formatNumber(record.promptTokens)}</td>
                    <td>{formatNumber(record.completionTokens)}</td>
                    <td>
                      {record.balanceSpent
                        ? `${formatNumber(Math.round(record.balanceSpent))} с баланса`
                        : `${formatNumber(record.totalTokens)}${record.partial ? '+' : ''}`}
                    </td>
                    <td>{record.toolCalls}</td>
                    <td>{formatDuration(record.duration)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {total > PAGE_SIZE && (
          <div className="assignments__filters">
            <button
              className="btn btn--ghost"
              disabled={offset === 0 || loadingPage}
              onClick={() => setOffset((current) => Math.max(current - PAGE_SIZE, 0))}
            >
              Назад
            </button>
            <button
              className="btn btn--ghost"
              disabled={offset + PAGE_SIZE >= total || loadingPage}
              onClick={() => setOffset((current) => current + PAGE_SIZE)}
            >
              Дальше
            </button>
            {loadingPage && <span className="note">Загружаю…</span>}
          </div>
        )}
      </div>
    </div>
  );
}

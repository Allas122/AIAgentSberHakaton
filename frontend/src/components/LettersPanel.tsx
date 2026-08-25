import { useCallback, useEffect, useRef, useState } from 'react';
import { api } from '../api/client';
import type { ComposedAssignment, LetterTemplate } from '../api/types';
import { IconClose, IconFile, IconSend, IconUpload } from './Icons';

const TEMPLATE_ACCEPT = '.txt,.md,.docx';
import { Markdown } from './Markdown';

const ACCEPT = '.docx';
const MAX_MB = 50;
const MAX_TEXT = 20000;
const MAX_INTENT = 1000;

interface Props {
  notify: (kind: 'info' | 'success' | 'error', text: string) => void;
}

export function LettersPanel({ notify }: Props) {
  const [file, setFile] = useState<File | null>(null);
  const [letter, setLetter] = useState('');
  const [intent, setIntent] = useState('');
  const [reply, setReply] = useState<string | null>(null);
  const [assignments, setAssignments] = useState<ComposedAssignment[]>([]);
  const [templates, setTemplates] = useState<LetterTemplate[]>([]);
  const [templateId, setTemplateId] = useState<string | null>(null);
  const [managing, setManaging] = useState(false);
  const [newName, setNewName] = useState('');
  const [newContent, setNewContent] = useState('');
  const [newFile, setNewFile] = useState<File | null>(null);
  const [savingTemplate, setSavingTemplate] = useState(false);

  const templateInputRef = useRef<HTMLInputElement>(null);

  const loadTemplates = useCallback(async () => {
    try {
      setTemplates(await api.letterTemplates());
    } catch {
      setTemplates([]);
    }
  }, []);

  useEffect(() => {
    void loadTemplates();
  }, [loadTemplates]);

  const saveTemplate = async () => {
    if (!newName.trim() || savingTemplate) return;
    if (!newFile && newContent.trim().length < 20) {
      notify('error', 'Вставьте текст шаблона или приложите файл');
      return;
    }

    setSavingTemplate(true);
    try {
      await api.createLetterTemplate(newName.trim(), newContent, newFile);
      setNewName('');
      setNewContent('');
      setNewFile(null);
      notify('success', 'Шаблон сохранён');
      await loadTemplates();
    } catch (e) {
      notify('error', e instanceof Error ? e.message : 'Не удалось сохранить шаблон');
    } finally {
      setSavingTemplate(false);
    }
  };

  const removeTemplate = async (template: LetterTemplate) => {
    const previous = templates;
    setTemplates((list) => list.filter((t) => t.id !== template.id));
    if (templateId === template.id) setTemplateId(null);
    try {
      await api.deleteLetterTemplate(template.id);
      notify('success', 'Шаблон удалён');
    } catch (e) {
      setTemplates(previous);
      notify('error', e instanceof Error ? e.message : 'Не удалось удалить шаблон');
    }
  };
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const inputRef = useRef<HTMLInputElement>(null);

  const attach = (candidate: File | undefined | null) => {
    if (!candidate) return;
    if (!candidate.name.toLowerCase().endsWith('.docx')) {
      setError('Письмо принимается в формате .docx');
      return;
    }
    if (candidate.size > MAX_MB * 1024 * 1024) {
      setError(`Файл больше ${MAX_MB} МБ`);
      return;
    }
    setError(null);
    setFile(candidate);
  };

  const canSend = (file !== null || letter.trim().length > 0) && !busy;

  const submit = async () => {
    if (!canSend) return;
    if (letter.length > MAX_TEXT) {
      setError(`Текст письма длиннее ${MAX_TEXT} символов`);
      return;
    }

    setBusy(true);
    setError(null);
    try {
      const result = await api.composeLetterReply(file, letter, intent, templateId);
      setReply(result.reply);
      setAssignments(result.assignments);
      notify(
        'success',
        result.assignments.length > 0
          ? `Ответ подготовлен, заведено поручений: ${result.assignments.length}`
          : 'Ответ подготовлен',
      );
    } catch (e) {
      const text = e instanceof Error ? e.message : 'Не удалось подготовить ответ';
      setError(text);
      notify('error', text);
    } finally {
      setBusy(false);
    }
  };

  const copy = async () => {
    if (!reply) return;
    try {
      await navigator.clipboard.writeText(reply);
      notify('success', 'Ответ скопирован');
    } catch {
      notify('error', 'Не удалось скопировать');
    }
  };

  return (
    <div className="letters">
      <div className="letters__inner">
        <header className="letters__head">
          <h1 className="letters__title">Ответ на письмо</h1>
          <p className="letters__sub">
            Приложите входящее письмо или вставьте его текст, укажите, какой ответ нужен, — система
            подготовит официальный ответ. Персональные данные вырезаются перед обработкой и
            возвращаются в готовый текст.
          </p>
        </header>

        <section className="letters__block">
          <span className="label">Входящее письмо</span>

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

          {file ? (
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
          ) : (
            <button className="btn btn--ghost" onClick={() => inputRef.current?.click()}>
              <IconUpload size={15} />
              Приложить .docx
            </button>
          )}

          <textarea
            className="letters__area"
            rows={8}
            value={letter}
            disabled={busy || file !== null}
            placeholder={
              file
                ? 'Текст будет взят из приложенного файла'
                : 'Либо вставьте текст письма сюда…'
            }
            onChange={(e) => setLetter(e.target.value)}
            aria-label="Текст входящего письма"
          />
        </section>

        <section className="letters__block">
          <label className="label" htmlFor="letter-intent">
            Что ответить
          </label>
          <textarea
            id="letter-intent"
            className="letters__area letters__area--short"
            rows={3}
            value={intent}
            disabled={busy}
            maxLength={MAX_INTENT}
            placeholder="Например: согласиться с помещением, отказать в софинансировании, запросить смету"
            onChange={(e) => setIntent(e.target.value)}
          />
          <span className="note">
            {intent.length} / {MAX_INTENT}
          </span>
        </section>

        <section className="letters__block">
          <div className="assignments__head" style={{ width: '100%' }}>
            <span className="label">Шаблон ответа</span>
            <button className="btn btn--sm btn--ghost" onClick={() => setManaging((v) => !v)}>
              {managing ? 'Свернуть' : 'Управлять шаблонами'}
            </button>
          </div>

          <div className="assignments__filters">
            <button
              className={`btn btn--ghost${templateId === null ? ' btn--active' : ''}`}
              onClick={() => setTemplateId(null)}
            >
              Без шаблона
            </button>
            {templates.map((t) => (
              <button
                key={t.id}
                className={`btn btn--ghost${templateId === t.id ? ' btn--active' : ''}`}
                onClick={() => setTemplateId(t.id)}
                title={t.content.slice(0, 200)}
              >
                {t.name}
              </button>
            ))}
          </div>

          {templates.length === 0 && !managing && (
            <span className="note">
              Шаблонов пока нет. Добавьте свой — ответ будет строиться по нему.
            </span>
          )}

          {managing && (
            <>
              <input
                className="letters__area letters__area--short"
                value={newName}
                disabled={savingTemplate}
                placeholder="Название шаблона"
                onChange={(e) => setNewName(e.target.value)}
                aria-label="Название шаблона"
              />

              <input
                ref={templateInputRef}
                type="file"
                accept={TEMPLATE_ACCEPT}
                className="sr-only"
                tabIndex={-1}
                aria-hidden="true"
                onChange={(e) => {
                  setNewFile(e.target.files?.[0] ?? null);
                  e.target.value = '';
                }}
              />

              {newFile ? (
                <span className="attach-pill">
                  <IconFile size={14} />
                  <span title={newFile.name}>{newFile.name}</span>
                  <button
                    className="icon-btn icon-btn--sm"
                    onClick={() => setNewFile(null)}
                    aria-label="Убрать файл шаблона"
                  >
                    <IconClose size={13} />
                  </button>
                </span>
              ) : (
                <button className="btn btn--ghost" onClick={() => templateInputRef.current?.click()}>
                  <IconUpload size={15} />
                  Загрузить .txt или .docx
                </button>
              )}

              <textarea
                className="letters__area letters__area--short"
                rows={5}
                value={newContent}
                disabled={savingTemplate || newFile !== null}
                placeholder={
                  newFile ? 'Текст будет взят из файла' : 'Либо вставьте текст шаблона сюда…'
                }
                onChange={(e) => setNewContent(e.target.value)}
                aria-label="Текст шаблона"
              />

              <div className="letters__actions">
                <button
                  className="btn btn--primary"
                  onClick={saveTemplate}
                  disabled={savingTemplate || !newName.trim() || (!newFile && newContent.trim().length < 20)}
                >
                  <IconSend size={15} />
                  {savingTemplate ? 'Сохраняю…' : 'Сохранить шаблон'}
                </button>
              </div>

              {templates.length > 0 && (
                <ul className="assignments assignments--compact">
                  {templates.map((t) => (
                    <li key={t.id} className="assignments__item">
                      <div className="assignments__head">
                        <span className="assignments__title">{t.name}</span>
                        <button
                          className="icon-btn icon-btn--sm"
                          onClick={() => void removeTemplate(t)}
                          aria-label={`Удалить шаблон ${t.name}`}
                        >
                          <IconClose size={13} />
                        </button>
                      </div>
                      <div className="assignments__meta">
                        <span className="chip">{t.content.length} симв.</span>
                        {t.sourceFileName && <span className="chip">{t.sourceFileName}</span>}
                      </div>
                    </li>
                  ))}
                </ul>
              )}
            </>
          )}
        </section>

        {error && (
          <span className="chip chip--warn">
            <span className="chip__text">{error}</span>
          </span>
        )}

        <div className="letters__actions">
          <button className="btn btn--primary" onClick={submit} disabled={!canSend}>
            <IconSend size={15} />
            {busy ? 'Готовлю ответ…' : 'Подготовить ответ'}
          </button>
          {reply && (
            <button className="btn btn--ghost" onClick={copy}>
              Скопировать
            </button>
          )}
        </div>

        {assignments.length > 0 && (
          <section className="letters__block">
            <span className="label">Поручения на контроль</span>
            <ul className="assignments assignments--compact">
              {assignments.map((a) => (
                <li key={a.id} className="assignments__item">
                  <span className="assignments__title">{a.title}</span>
                  <div className="assignments__meta">
                    {a.assignee && <span className="chip">{a.assignee}</span>}
                    {a.dueDate && <span className="chip">до {a.dueDate}</span>}
                  </div>
                </li>
              ))}
            </ul>
            <span className="note">Открыть их можно в разделе «Поручения».</span>
          </section>
        )}

        {reply && (
          <section className="letters__reply">
            <span className="label">Проект ответа</span>
            <Markdown text={reply} />
          </section>
        )}
      </div>
    </div>
  );
}

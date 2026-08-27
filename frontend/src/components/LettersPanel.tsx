import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { api } from '../api/client';
import type { ComposedAssignment, LetterAddressee, LetterRequisites, LetterTemplate } from '../api/types';
import { EMPTY_ADDRESSEE, EMPTY_REQUISITES, PersonGender } from '../api/types';
import { IconClose, IconFile, IconSend, IconUpload } from './Icons';
import { Markdown } from './Markdown';

const TEMPLATE_ACCEPT = '.txt,.md,.docx';
const ACCEPT = '.docx,.pdf';
const ALLOWED = ['.docx', '.pdf'];

const GENDER_LABELS: Record<PersonGender, string> = {
  Unknown: 'Определить по фамилии',
  Male: 'Мужской — «Уважаемый»',
  Female: 'Женский — «Уважаемая»',
};
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
  const [documentId, setDocumentId] = useState<string | null>(null);
  const [requisites, setRequisites] = useState<LetterRequisites>(EMPTY_REQUISITES);
  const [downloading, setDownloading] = useState(false);
  const [checking, setChecking] = useState(false);
  const [assignments, setAssignments] = useState<ComposedAssignment[]>([]);
  const [warnings, setWarnings] = useState<string[]>([]);
  const [addressee, setAddressee] = useState<LetterAddressee>(EMPTY_ADDRESSEE);
  const [templates, setTemplates] = useState<LetterTemplate[]>([]);
  const [templateId, setTemplateId] = useState<string | null>(null);
  const [managing, setManaging] = useState(false);
  const [newName, setNewName] = useState('');
  const [newContent, setNewContent] = useState('');
  const [newFile, setNewFile] = useState<File | null>(null);
  const [savingTemplate, setSavingTemplate] = useState(false);

  const templateInputRef = useRef<HTMLInputElement>(null);

  const ownTemplates = useMemo(() => templates.filter((t) => !t.isPreset), [templates]);

  const activeTemplate = useMemo(
    () => templates.find((t) => t.id === templateId) ?? null,
    [templates, templateId],
  );

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
    if (!ALLOWED.some((ext) => candidate.name.toLowerCase().endsWith(ext))) {
      setError('Письмо принимается в формате .docx или .pdf');
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
      const result = await api.composeLetterReply(
        file,
        letter,
        intent,
        templateId,
        addressee,
        requisites,
      );
      setReply(result.reply);
      setDocumentId(result.documentId);
      setAssignments(result.assignments);
      setWarnings(result.warnings);
      notify(
        'success',
        result.assignments.length > 0
          ? `Ответ подготовлен, заведено поручений: ${result.assignments.length}`
          : 'Ответ подготовлен',
      );
    } catch (e) {
      const text = e instanceof Error ? e.message : 'Не удалось подготовить ответ';
      setWarnings([]);
      setDocumentId(null);
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
              Приложить .docx или .pdf
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

        {activeTemplate?.hasForm && (
          <section className="letters__block">
            <span className="label">Регистрация</span>
            <span className="note">
              Номер и дата печатаются на бланке дословно. Оставьте пустым — останутся прочерки.
            </span>

            <div className="org__grid org__grid--compact">
              <label className="org__field">
                <span className="label">Исх. №</span>
                <input
                  className="org__input"
                  value={requisites.outgoingNumber}
                  disabled={busy}
                  placeholder="01-16/1234"
                  onChange={(e) => setRequisites((r) => ({ ...r, outgoingNumber: e.target.value }))}
                />
              </label>
              <label className="org__field">
                <span className="label">от</span>
                <input
                  className="org__input"
                  value={requisites.outgoingDate}
                  disabled={busy}
                  placeholder="27.08.2026"
                  onChange={(e) => setRequisites((r) => ({ ...r, outgoingDate: e.target.value }))}
                />
              </label>
              <label className="org__field">
                <span className="label">На №</span>
                <input
                  className="org__input"
                  value={requisites.replyToNumber}
                  disabled={busy}
                  placeholder="МН-11/1234"
                  onChange={(e) => setRequisites((r) => ({ ...r, replyToNumber: e.target.value }))}
                />
              </label>
              <label className="org__field">
                <span className="label">от</span>
                <input
                  className="org__input"
                  value={requisites.replyToDate}
                  disabled={busy}
                  placeholder="20.08.2026"
                  onChange={(e) => setRequisites((r) => ({ ...r, replyToDate: e.target.value }))}
                />
              </label>
            </div>
          </section>
        )}

        <section className="letters__block">
          <span className="label">Кому пишем</span>
          <span className="note">
            Во входящем письме обычно стоят только инициалы, поэтому имя и отчество для
            обращения нужно указать здесь — из «Е.М.» они не восстанавливаются.
          </span>

          <input
            className="letters__area letters__area--short"
            value={addressee.position}
            disabled={busy}
            placeholder="Должность: Заместителю Министра науки и высшего образования РФ"
            onChange={(e) => setAddressee((a) => ({ ...a, position: e.target.value }))}
            aria-label="Должность адресата"
          />

          <input
            className="letters__area letters__area--short"
            value={addressee.name}
            disabled={busy}
            placeholder="ФИО: Е.М. Грудининой"
            onChange={(e) => setAddressee((a) => ({ ...a, name: e.target.value }))}
            aria-label="ФИО адресата"
          />

          <input
            className="letters__area letters__area--short"
            value={addressee.salutation}
            disabled={busy}
            placeholder="Имя и отчество для обращения: Елена Михайловна"
            onChange={(e) => setAddressee((a) => ({ ...a, salutation: e.target.value }))}
            aria-label="Имя и отчество для обращения"
          />

          <label className="label" htmlFor="letter-gender">
            Обращение
          </label>
          <select
            id="letter-gender"
            className="letters__area letters__area--short"
            value={addressee.gender}
            disabled={busy}
            onChange={(e) =>
              setAddressee((a) => ({ ...a, gender: e.target.value as PersonGender }))
            }
          >
            {Object.entries(GENDER_LABELS).map(([value, label]) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </select>
          <span className="note">
            Автоопределение работает по фамилии и отчеству. Фамилии вроде «Шмидт», «Коваль»,
            «Черных» одинаковы у мужчин и женщин — для них выберите обращение вручную.
          </span>
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
            <span className="label">Вид письма</span>
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
                title={
                  t.hasForm
                    ? `Бланк .docx. Метки: ${t.placeholders.join(', ')}`
                    : t.content.slice(0, 200)
                }
              >
                {t.name}
                {t.hasForm && <span className="letters__badge">бланк</span>}
              </button>
            ))}
          </div>

          {!managing && (
            <span className="note">
              Первые три — встроенные виды письма. Их можно выбрать сразу; свой шаблон
              добавляется рядом через «Управлять шаблонами». Чтобы получать ответ готовым
              файлом на бланке, загрузите своё исходящее письмо .docx, заменив в нём
              переменные куски на метки: &lt;ТЕЛО&gt;, &lt;АДРЕСАТ&gt;, &lt;ОБРАЩЕНИЕ&gt;,
              &lt;ПОДПИСЬ_ДОЛЖНОСТЬ&gt;, &lt;ПОДПИСЬ_ФИО&gt;, &lt;КОНТАКТНОЕ ЛИЦО&gt;,
              &lt;ИСХ_НОМЕР&gt;, &lt;ИСХ_ДАТА&gt;, &lt;НА_НОМЕР&gt;, &lt;НА_ДАТА&gt;,
              &lt;ИСПОЛНИТЕЛЬ_ФИО&gt;, &lt;ИСПОЛНИТЕЛЬ_ТЕЛЕФОН&gt;.
            </span>
          )}

          {activeTemplate?.hasForm && (
            <div className="letters__actions">
              <span className="note">
                Ответ придёт файлом на бланке. Метки в бланке:{' '}
                {activeTemplate.placeholders.join(', ')}.
              </span>
              <button
                className="btn btn--ghost btn--sm"
                disabled={checking}
                onClick={async () => {
                  setChecking(true);
                  try {
                    await api.downloadTemplatePreview(activeTemplate.id);
                  } catch (e) {
                    notify('error', e instanceof Error ? e.message : 'Не удалось проверить бланк');
                  } finally {
                    setChecking(false);
                  }
                }}
              >
                {checking ? 'Готовлю…' : 'Проверить бланк'}
              </button>
            </div>
          )}

          {activeTemplate?.hasForm && !activeTemplate.placeholders.includes('ТЕЛО') && (
            <span className="chip chip--warn">
              <span className="chip__text">
                В бланке нет метки &lt;ТЕЛО&gt; — тексту ответа некуда встать.
              </span>
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

              {ownTemplates.length > 0 && (
                <ul className="assignments assignments--compact">
                  {ownTemplates.map((t) => (
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
          {documentId && (
            <button
              className="btn btn--ghost"
              disabled={downloading}
              onClick={async () => {
                setDownloading(true);
                try {
                  await api.downloadDocument(documentId);
                } catch (e) {
                  notify('error', e instanceof Error ? e.message : 'Не удалось скачать письмо');
                } finally {
                  setDownloading(false);
                }
              }}
            >
              {downloading ? 'Готовлю файл…' : 'Скачать .docx'}
            </button>
          )}
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

        {warnings.length > 0 && (
          <section className="letters__block">
            <span className="label">Требуется внимание</span>
            {warnings.map((warning) => (
              <span key={warning} className="chip chip--warn">
                <span className="chip__text">{warning}</span>
              </span>
            ))}
          </section>
        )}

        {reply && (
          <section className="letters__reply">
            <span className="label">{documentId ? 'Текст письма (тело)' : 'Проект ответа'}</span>
            <Markdown text={reply} />
          </section>
        )}
      </div>
    </div>
  );
}

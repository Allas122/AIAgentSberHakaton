import { IconSparkle } from './Icons';

interface Props {
  manualTitle: string | null;
  staff?: boolean;
  onPick: (prompt: string) => void;
}

const STAFF_SUGGESTIONS = [
  {
    title: 'Что у меня висит',
    sub: 'Открытые поручения и сроки',
    prompt: 'Покажи мои поручения: что в работе и что просрочено.',
  },
  {
    title: 'Завести поручение',
    sub: 'Задача, исполнитель, срок',
    prompt: 'Заведи поручение: подготовить сводный отчёт. Срок — конец недели.',
  },
  {
    title: 'Итоги проверки заявки',
    sub: 'Баллы по критериям и замечания',
    prompt: 'Подними результат проверки заявки: баллы по критериям и основные замечания.',
  },
  {
    title: 'Сменить статус',
    sub: 'Отметить поручение выполненным',
    prompt: 'Покажи поручения в работе, я скажу, какое отметить выполненным.',
  },
];

const SUGGESTIONS = [
  {
    title: 'Разобрать письмо',
    sub: 'Определю адресата, срок и поручение',
    prompt: 'Разбери входящее письмо: кто адресат, какой срок и что нужно сделать.',
  },
  {
    title: 'Подготовить ответ',
    sub: 'Соберу проект по шаблону и регламенту',
    prompt: 'Подготовь проект ответа на письмо по шаблону и регламенту.',
  },
  {
    title: 'Проверить мониторинг',
    sub: 'Сверю показатели, сроки и обязательные поля',
    prompt: 'Проверь выгрузку мониторинга: сверь показатели, сроки и обязательные поля.',
  },
  {
    title: 'Свести показатели',
    sub: 'Посчитаю цифры по выгрузке',
    prompt: 'Посчитай по выгрузке мониторинга: сколько строк по каждому подразделению.',
  },
];

export function EmptyState({ manualTitle, staff = false, onPick }: Props) {
  const cards = staff ? STAFF_SUGGESTIONS : SUGGESTIONS;

  return (
    <div className="thread">
      <div className="empty">
        <span className="empty__mark">
          <IconSparkle size={26} />
        </span>

        <div>
          <h2 className="empty__title">{staff ? 'Служебный чат' : 'Чем помочь с мониторингом?'}</h2>
          <p className="empty__sub">
            {staff
              ? 'Веду поручения: завожу, показываю сроки и меняю статусы. База знаний сюда не подключена — за требованиями к заявке откройте чат с документом.'
              : manualTitle
                ? `Отвечаю по документу «${manualTitle}». Спросите про сроки или приложите документ на проверку.`
                : 'Разбираю письма, поручения и мониторинги. Спросите про сроки или приложите документ.'}
          </p>
        </div>

        <div className="suggest-grid">
          {cards.map((s) => (
            <button key={s.title} className="suggest-card" onClick={() => onPick(s.prompt)}>
              <span className="suggest-card__title">{s.title}</span>
              <span className="suggest-card__sub">{s.sub}</span>
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}

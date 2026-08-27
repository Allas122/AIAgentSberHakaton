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
    title: 'С чего начать заявку',
    sub: 'Разбор структуры и обязательных разделов',
    prompt: 'С чего начать подготовку заявки? Перечисли обязательные разделы по порядку.',
  },
  {
    title: 'Проверить мою заявку',
    sub: 'Приложите .docx или .pdf — агент даст рецензию',
    prompt: 'Проверь мою заявку на соответствие положению и укажи слабые места.',
  },
  {
    title: 'Критерии оценки',
    sub: 'На что смотрит экспертная комиссия',
    prompt: 'По каким критериям комиссия оценивает заявки и какой вес у каждого?',
  },
  {
    title: 'Обосновать бюджет',
    sub: 'Как расписать смету, чтобы её приняли',
    prompt: 'Как правильно обосновать бюджет проекта в заявке? Приведи пример структуры сметы.',
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
          <h2 className="empty__title">{staff ? 'Служебный чат' : 'Чем помочь с заявкой?'}</h2>
          <p className="empty__sub">
            {staff
              ? 'Веду поручения: завожу, показываю сроки и меняю статусы. База знаний сюда не подключена — за требованиями к заявке откройте чат с документом.'
              : manualTitle
                ? `Отвечаю по документу «${manualTitle}». Спросите про требования или приложите заявку на проверку.`
                : 'Загрузите документ в базу знаний в панели слева — по нему я буду отвечать и сверять заявки.'}
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

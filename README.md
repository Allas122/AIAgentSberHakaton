# AI-агент грантового консультанта

RAG-ассистент для работы с грантовыми заявками, собранный под хакатон Сбера.

Пользователь загружает **методичку** (`.md`), она разбирается агентом на части и индексируется в
Valkey как векторное хранилище. Дальше с этой методичкой можно:

- **общаться** — консультирующий агент отвечает **строго по методичке**, а не по своим знаниям;
- **проверять заявку** — загрузить `.docx` и получить вердикт: что не соответствует методичке,
  какие критерии оценки закрыты, а какие нет.

Перед тем как текст уходит в GigaChat, из него вычищаются персональные данные; в ответе агента они
восстанавливаются обратно.

---

## Два сервиса

### `ChatNode` — .NET 10, ASP.NET Core

Всё приложение: REST API, SignalR-хаб, агенты, работа с Valkey и S3. Слушает `8080` в контейнере,
наружу проброшен на `5064`.

Внутри одного проекта три слоя папками, у каждого свой DI-extension, вызываемый в `Program.cs`:
`Infrastructure` → `Application` → `Api`. `Domain` — отдельная библиотека с записями и интерфейсами
репозиториев, без зависимостей. Направление зависимостей Api → Application → Infrastructure → Domain,
но `Application` напрямую использует типы `Infrastructure`, так что это не строгий onion.

Три агента поверх GigaChat, все с function calling:

| Агент | Что делает |
|---|---|
| `ManualParserAgent` | Режет методичку на части и наполняет RAG-индекс |
| `ConsultingAgent` | Отвечает на вопросы по методичке, читает находки проверки заявки |
| `ApplicationReviewAgent` | Проверяет `.docx`-заявку по методичке и выдаёт вердикт |

Эндпоинты GigaChat требуют российских корневых сертификатов: `Resources/*.cer` встроены в сборку и
подключаются ко всем HTTP-хендлерам GigaChat.

### `AnonimyzeService` — Python 3.11, gRPC

Убирает персональные данные из текста до того, как он уйдёт в GigaChat. Слушает `50051`.
Мультиязычная NER-модель `Wismut/nym-pii-multilingual` плюс regex с проверкой контрольных сумм для
ИНН и СНИЛС. Найденное заменяется тегами `[TYPE_XXXX]`, соответствие «тег → оригинал» кладётся в
Valkey под ключ `anon:{sessionId}` на 24 часа, чтобы ответ агента можно было деанонимизировать.

Контракт лежит **в двух копиях**: `ChatNode/Protos/anonymize_service.proto` и
`AnonimyzeService/protos/anonymize_service.proto` — править нужно оба. C#-клиент генерируется на
сборке через `Grpc.Tools`, Python-стабы в `AnonimyzeService/generated/` закоммичены и после правки
proto перегенерируются руками (`grpcio-tools` нужен только для этого и в `requirements.txt` намеренно
не входит):

```powershell
pip install grpcio-tools
python -m grpc_tools.protoc -I AnonimyzeService/protos --python_out=AnonimyzeService/generated --grpc_python_out=AnonimyzeService/generated AnonimyzeService/protos/anonymize_service.proto
```

### Инфраструктура, которую они требуют

| Сервис | Зачем | Порт |
|---|---|---|
| `valkey` | Единственное хранилище состояния. Нужен **модуль поиска** (`FT.CREATE`/`FT.SEARCH`) | `6379` |
| `seaweedfs` | S3-совместимое хранилище файлов: методички, анонимизированные заявки | `8333` (S3), `8888` (filer), `9333` (master) |
| `seaweedfs-init` | Одноразовый контейнер: создаёт бакет и завершается | — |

Реляционной базы нет вообще: пользователи, чаты, сообщения (Redis Stream), части методичек, находки
проверки и векторные индексы живут в Valkey с TTL. Отсюда важное следствие — **данные протухают через
сутки** (настраивается блоком `EXPIRATION_*`).

---

## Быстрый старт в Docker

Нужен Docker Desktop / Docker Engine с Compose v2.

```powershell
copy .env.example .env
```

Затем в `.env` обязательно заполнить два значения — без них `docker compose` откажется стартовать:

```dotenv
GIGACHAT_AUTHORIZATION_KEY=<ключ авторизации GigaChat в base64>
JWT_SECRET_KEY=<случайная строка от 32 символов>
```

Сгенерировать секрет JWT:

```powershell
[Convert]::ToBase64String((1..48 | % { Get-Random -Max 256 }))
```

Запуск:

```powershell
docker compose up -d --build
docker compose logs -f chatnode
```

Проверить, что поднялось:

- Scalar UI — http://localhost:5064/scalar
- OpenAPI — http://localhost:5064/openapi/v1.json
- SeaweedFS filer — http://localhost:8888
- `docker compose ps` — все сервисы `healthy`/`running`, `seaweedfs-init` в статусе `exited (0)`

Остановка и полная очистка данных:

```powershell
docker compose down
docker compose down -v
```

### Первый запуск долгий

`anonymizer` при старте скачивает NER-модель (~1–2 ГБ) с Hugging Face. `chatnode` ждёт, пока
анонимайзер пройдёт healthcheck, поэтому первый `up` может занять несколько минут. Модель кешируется
в volume `hf-cache`, повторные запуски — быстрые. Запас времени на скачивание задаётся
`ANONYMIZER_START_PERIOD` (по умолчанию `600s`), а `HF_HUB_OFFLINE=1` заставит сервис работать только
из кеша.

### Что попадает в образы

Контекст сборки `chatnode` — корень репозитория, но `.dockerignore` устроен как «запретить всё, потом
разрешить `ChatNode/` и `Domain/`». Больше в образ не уходит ничего. Контекст `anonymizer` —
`AnonimyzeService/`, из него в образ попадают только `main.py`, `requirements.txt` и `generated/`;
`protos/` и локальный `.env` отрезаны, чтобы `load_dotenv()` в контейнере не перекрывал переменные
окружения из compose.

---

## Конфигурация через `.env`

Внутри контейнера конфиг ASP.NET Core берётся **только из переменных окружения** —
`ASPNETCORE_ENVIRONMENT=Production`, а `appsettings.Development.json` в этом режиме не читается.
`docker-compose.yml` разворачивает человекочитаемые имена из `.env` в имена секций .NET
(`GIGACHAT_SECTION_CHARS` → `GigaChat__ApplicationSectionChars`). У каждой переменной есть значение
по умолчанию в самом compose-файле, так что пустой `.env` с двумя обязательными ключами уже рабочий.

### Обязательные

| Переменная | Назначение |
|---|---|
| `GIGACHAT_AUTHORIZATION_KEY` | Ключ авторизации GigaChat. Без него **всё AI-поведение падает в рантайме, а не на старте** |
| `JWT_SECRET_KEY` | Ключ подписи JWT, минимум 32 символа (HS256) |

### Инфраструктура

| Переменная | По умолчанию | Комментарий |
|---|---|---|
| `CHATNODE_HTTP_PORT` | `5064` | Внешний порт API |
| `VALKEY_PORT` | `6379` | |
| `VALKEY_CONNECTION` | `valkey:6379` | Строка подключения изнутри сети compose |
| `SEAWEEDFS_S3_PORT` | `8333` | |
| `S3_SERVICE_URL` | `http://seaweedfs:8333` | |
| `S3_BUCKET` | `grants` | Создаётся контейнером `seaweedfs-init` |
| `S3_ACCESS_KEY` / `S3_SECRET_KEY` | `any` | SeaweedFS в этой сборке пускает анонимно, ключи не проверяются |
| `ANONYMIZER_PORT` | `50051` | |
| `ANONYMIZER_CONNECTION` | `http://anonymizer:50051` | gRPC поверх незашифрованного HTTP/2 |

### Время жизни данных

| Переменная | По умолчанию |
|---|---|
| `EXPIRATION_USER_SECONDS` | `86400` |
| `EXPIRATION_CHAT_SECONDS` | `86400` |
| `EXPIRATION_MESSAGE_STREAM_SECONDS` | `86400` |
| `EXPIRATION_PIN_SECONDS` | `86400` |
| `WS_TICKET_EXPIRATION_SECONDS` | `30` |
| `JWT_ACCESS_TOKEN_MINUTES` | `15` |
| `JWT_REFRESH_TOKEN_DAYS` | `1` |

### GigaChat

| Переменная | По умолчанию | Комментарий |
|---|---|---|
| `GIGACHAT_SCOPE` | `GIGACHAT_API_B2B` | Для физлиц — `GIGACHAT_API_PERS` |
| `GIGACHAT_AUTH_URL` | `https://ngw.devices.sberbank.ru:9443/api/v2/oauth` | |
| `GIGACHAT_BASE_URL` | `https://api.giga.chat/v1` | |
| `GIGACHAT_MANUAL_PARSER_MODEL` | `GigaChat-2-Max` | Разбор методички |
| `GIGACHAT_CONSULTING_MODEL` | `GigaChat-2-Pro` | Диалог с пользователем |
| `GIGACHAT_REVIEW_MODEL` | `GigaChat-2-Max` | Проверка заявки |
| `GIGACHAT_EMBEDDING_MODEL` | `Embeddings` | |
| `GIGACHAT_EMBEDDING_DIM` | `1024` | **Менять только вместе с пересозданием индексов**, см. ниже |
| `GIGACHAT_SECTION_CHARS` | `4000` | Размер фрагмента заявки на один проход LLM |
| `GIGACHAT_SECTION_TOOL_CALLS` | `12` | Бюджет вызовов инструментов на фрагмент |
| `GIGACHAT_PIN_DUPLICATE_DISTANCE` | `0.15` | Косинусное расстояние, ниже которого находка считается дублем |
| `GIGACHAT_PIN_SEARCH_DISTANCE` | `0.55` | Порог выдачи для поиска по находкам |
| `GIGACHAT_TIMEOUT_MINUTES` | `10` | |
| `GIGACHAT_MAX_RETRIES` | `4` | Ретраи внутри HTTP-клиента библиотеки |
| `GIGACHAT_TRANSPORT_RETRIES` | `3` | Ретраи на уровне транспорта (оборванный TLS) |
| `GIGACHAT_MAX_OPERATION_ATTEMPTS` | `2` | Повтор целого шага агента |
| `GIGACHAT_RETRY_BACKOFF_FACTOR` | `1.0` | Задержка = фактор × 2^(попытка−1) |
| `GIGACHAT_OPERATION_RETRY_DELAY_SECONDS` | `5` | |

Список ретраимых кодов (`RetryOnStatusCodes`) через `.env` не пробрасывается — он остаётся значением
по умолчанию из кода (`408, 429, 500, 502, 503, 504`). Если нужно переопределить, задайте
индексированные переменные `GigaChat__RetryOnStatusCodes__0`, `__1` и так далее. **429 и 5xx лечатся
повтором, а 413 — нет**: его добавлять в этот список нельзя, он чинится уменьшением запроса, и у
агента проверки заявок для этого своя логика.

### Анонимайзер

| Переменная | По умолчанию | Комментарий |
|---|---|---|
| `PII_MODEL_ID` | `Wismut/nym-pii-multilingual` | |
| `PII_MODEL_REVISION` | пусто | Пин на конкретный коммит модели |
| `REDACT_TYPES` | пусто → встроенный список | Пустое значение = дефолтный набор, а не «ничего не скрывать» |
| `MAX_TEXT_LENGTH` | `20000` | Больше — gRPC вернёт `INVALID_ARGUMENT` |
| `CHUNK_MAX_TOKENS` / `CHUNK_OVERLAP_TOKENS` | `400` / `50` | Нарезка текста под окно модели |
| `GRPC_MAX_WORKERS` | `10` | |
| `GRPC_MAX_MESSAGE_MB` | `50` | |
| `ANONYMIZER_LOG_LEVEL` | `INFO` | |

`REDACT_TYPES` расширять стоит осторожно. Модель выдаёт 41 тип, а скрываются только те, что реально
идентифицируют человека: имена, контакты, адреса, номера документов и счетов, учётные данные.
`DATE`, `TIME`, `AGE`, `COMPANY_NAME`, `CITY`, `COUNTRY`, `URL` намеренно **не** скрываются — в
грантовой заявке это не персданные, а её содержание (сроки проекта, площадки, ссылки, опыт команды).
Когда их скрывали, номер пункта «5.» превращался в `[DATE_010B]`, а название конференции — в
`[COMPANY_NAME_0115]`, и агент честно помечал корректные разделы как дефектные.

---

## Локальный запуск без Docker

Нужны .NET 10 SDK, Python 3.11+ и поднятые Valkey (с модулем поиска) и SeaweedFS на `localhost`.
Локальные значения по умолчанию уже лежат в `ChatNode/appsettings.Development.json`.

```powershell
dotnet build AIAgentSberHakaton.sln
dotnet run --project ChatNode                 # http://localhost:5064, Scalar UI на /scalar
```

Ключ GigaChat в `appsettings` намеренно отсутствует — кладите его в user secrets:

```powershell
dotnet user-secrets --project ChatNode set "GigaChat:AuthorizationKey" "<ключ>"
```

Анонимайзер:

```powershell
pip install -r AnonimyzeService/requirements.txt
python AnonimyzeService/main.py
```

Гибридный вариант — инфраструктуру поднять в Docker, а `ChatNode` запускать из IDE:

```powershell
docker compose up -d valkey seaweedfs seaweedfs-init anonymizer
dotnet run --project ChatNode
```

Порты `6379`, `8333`, `50051` проброшены наружу, поэтому `appsettings.Development.json`
с его `localhost` заработает без правок.

### Как собирать проект

Только `dotnet build` или `dotnet publish`. **Никогда не проверяйте сборку через
`dotnet msbuild -t:Compile`**: цель `PrepareResources` висит на `Build`, а не на `CompileDependsOn`,
поэтому `-t:Compile` соберёт сборку с нулём встроенных ресурсов. Компилируется чисто, а в рантайме
падает на `Embedded resource 'ChatNode.Resources.russian_ca.cer' not found`. Если `dotnet build`
падает из-за того, что запущенное приложение держит `ChatNode.exe` — остановите приложение или
соберите в отдельный каталог через `-o`.

Тестового проекта в репозитории нет, `dotnet test` ничего не найдёт. Проверка изменений — ручной
прогон через Scalar UI на `/scalar`.

---

## API

### REST

| Метод | Путь | Авторизация | Описание |
|---|---|---|---|
| `POST` | `/api/auth/guest` | — | Гостевая пара токенов |
| `POST` | `/api/auth/refresh` | — | Обновление пары по refresh-токену |
| `GET` | `/api/auth/ws-ticket` | JWT | Одноразовый тикет для WebSocket (TTL 30 с) |
| `POST` | `/api/manuals/upload` | — | Загрузка методички (`multipart`: `Title`, `File` — `.md`) |
| `GET` | `/api/manuals` | — | Список методичек |
| `POST` | `/api/chat` | JWT | Создать чат |
| `GET` | `/api/chat?lastChatId=&limit=10` | JWT | Список чатов, курсорная пагинация |
| `POST` | `/api/chat/{chatId}/files` | JWT | Проверка заявки (`multipart`: `File` — `.docx`, `ManualId`, `Content`) |

### SignalR — `/chat-hub?ticket={guid}`

| Метод хаба | Описание |
|---|---|
| `CreateChat` | Создать чат |
| `GetChatList(lastChatId, limit)` | Список чатов |
| `GetMessages(chatId, lastMessageId, limit)` | История сообщений |
| `SendMessageToConsultingAgent(chatId, content, manualId)` | Сообщение консультанту |

Клиентские события: `AiStatusUpdate` (прогресс агента) и `FileStatusChanged` (`Uploaded` /
`Reviewed`).

### Две схемы аутентификации

REST работает на обычном JWT Bearer. SignalR заголовки отправить не может, поэтому у хаба своя схема
`WebSocketScheme`: клиент с JWT дёргает `GET /api/auth/ws-ticket`, получает одноразовый тикет с TTL
30 секунд и подключается на `/chat-hub?ticket={guid}`. Каждое соединение вступает в группу
`user:{userId}` — туда и приходят статусы агента.

---

## Как это работает

### Загрузка методички

`ManualParserAgent` режет файл на куски ~4000 символов по структурным границам с перекрытием 500
символов и скармливает модели по одному, передавая перекрытие как «уже обработанный» контекст. Модель
вызывает `add_manual_part` и `add_manual_navigation_header`, из чего и собирается RAG-индекс. Куски,
которые упали или исчерпали бюджет вызовов, собираются в список и проходят второй раз половинным
размером — один сбойный кусок больше не роняет всю методичку.

### Диалог

`ConsultingAgent` отвечает **только** по результатам KNN-поиска `search_in_manual` и обязан звать
`send_status_of_processing` перед каждым поиском, чтобы пользователь видел прогресс. Дополнительно он
получает read-only доступ к находкам проверки заявки в том же чате (`get_pins`, `search_pins`), так
что после проверки можно спросить «что было не так».

В историю сохраняются только текстовые ответы, результаты вызовов инструментов — нет. Системный
промпт поэтому явно требует искать заново, а не пересказывать собственный прошлый ответ.

### Проверка заявки

`POST /api/chat/{chatId}/files` → PII вычищается **прямо внутри docx** (ключ сессии = `chatId`) →
анонимизированная копия кладётся в S3 и является **единственной сохранённой**, оригинал с персданными
не хранится нигде → документ режется на секции → `ApplicationReviewAgent` идёт по ним **строго по
одной** → вердикт деанонимизируется и добавляется в чат сообщением агента.

Порядок «сначала анонимизация, потом нарезка» не случаен, и параллелить фрагменты нельзя: контекст
фрагмента об остальном документе — это хранилище находок и накопительная сводка, и при конкурентной
обработке двенадцатый фрагмент читает и то и другое наполовину заполненным.

Фрагменты связаны накопительной сводкой: после каждого в неё сливается всё, что агент записал,
изменил или удалил, а следующий получает её хвост в промпте. Сводку ведёт код, инструментов для её
правки у модели нет — поэтому удалить находку она может, а «забыть» прочитанный раздел нет. Полная
сводка уходит в сверку с критериями и в вердикт, то есть туда, где решается, чего в заявке не
хватает. Детали по прошлым находкам агент достаёт сам поиском `search_pins`, а не получает их
рассылкой в каждый запрос.

Отдельный проход `ExtractCriteriaAsync` вытаскивает из методички критерии оценки; их названия уходят
в промпт каждой секции, а полный список — в вердикт. Если критериев в методичке нет, извлечение
возвращает пусто и промпты откатываются на обычную структуру — функция деградирует, а не выдумывает
критерии.

**Проход по секции не имеет права заявлять, что чего-то нет.** Он видит один фрагмент из многих, и
«бюджет не указан» почти всегда ложь — бюджет в соседнем фрагменте. Реальные пропуски ищет
`CheckCoverageAsync`, единственное место с обзором всего документа. Формулировки отсутствия в находках
принудительно понижаются до «проверить вручную».

Теги вида `[PER_0001]` в тексте — **не дефект заявки**: значение есть, оно просто скрыто от
проверяющего. Это зафиксировано и в промптах, и в коде.

---

## Грабли

**Valkey нужен с модулем поиска.** Обычный `redis:alpine` или `valkey/valkey` не подойдут — код
использует `FT.CREATE`/`FT.SEARCH`. В compose стоит `valkey/valkey-bundle`, где модуль уже есть.
Поэтому же у сервиса `valkey` намеренно не переопределён `command`: свой `valkey-server` без конфига
образа отключит загрузку модулей, и приложение упадёт на создании индексов.

**Смена модели эмбеддингов ломает индексы молча.** `ValkeyIndexInitializer` создаёт
`idx:manual_parts` и `idx:pins` только если их нет. Если поменять `GIGACHAT_EMBEDDING_DIM`, `FT.CREATE`
просто не выполнится, а старая размерность останется. Нужно удалить индексы или тома:

```powershell
docker compose exec valkey valkey-cli FT.DROPINDEX idx:manual_parts
docker compose exec valkey valkey-cli FT.DROPINDEX idx:pins
docker compose restart chatnode
```

**Бакет создаётся отдельным контейнером.** SeaweedFS не создаёт бакет сам при первом `PUT`, поэтому
`seaweedfs-init` дожидается S3-порта и делает `PUT /{bucket}`. Если переименовали `S3_BUCKET` — нужен
повторный `docker compose up seaweedfs-init`.

**`UseHttpsRedirection` в контейнере.** HTTPS-порт не настроен, поэтому в логах будет предупреждение
`Failed to determine the https port for redirect`. Запросы при этом проходят; TLS терминируйте на
внешнем прокси.

**Ограничение — размер промпта, а не качество модели.** Русский текст стоит GigaChat примерно один
токен на 3 символа, и в каждый запрос уезжает системный промпт плюс полная JSON-схема всех
инструментов — причём заново на каждом витке цикла вызовов, тогда как текст фрагмента отправляется
один раз. Окно забивают **накопленные результаты вызовов инструментов**, а не входной текст: каждый
вызов дописывает найденные части методички и находки обратно в тот же диалог. Поэтому при 413
уменьшать входной фрагмент обычно бесполезно — сначала посчитайте, сколько возвращают инструменты.
Если в логах появились строки `[REVIEW TOO LARGE]`, снижайте `GIGACHAT_SECTION_TOOL_CALLS`, а если и
это не помогло — `GIGACHAT_SECTION_CHARS` до 3000.

Строка `[SEARCHING]` в логах печатает суммарный размер выдачи поиска по методичке — это самая
дорогая позиция в цикле, и смотреть при разборе 413 надо в первую очередь на неё.

**Известные недоделки** (хакатон, не считайте это задумкой):
`ChatService.GetMessagesAsync` собирает `MessageDisplayDto`, но не кладёт его в возвращаемый список;
`ManualController` без `[Authorize]`; у `ChatHub` нет входа в пайплайн проверки заявки, поэтому
docx-путь доступен только по REST, хотя статусы всё равно приходят по хабу.

---

## Структура репозитория

```
ChatNode/                  ASP.NET Core: Api → Application → Infrastructure
  Api/                     контроллеры, SignalR-хаб, валидаторы, мапперы
  Application/             сервисы уровня приложения и свои DTO
  Infrastructure/          агенты, function tools, репозитории Valkey, S3, gRPC-клиент
  Protos/                  контракт анонимайзера (копия)
  Resources/               российские CA-сертификаты, встроенные в сборку
  Dockerfile
Domain/                    записи и интерфейсы репозиториев, без зависимостей
AnonimyzeService/          Python gRPC-сервис анонимизации
  generated/               сгенерированные Python-стабы, закоммичены
  protos/                  контракт анонимайзера (копия)
  Dockerfile
docker-compose.yml
.env.example
```

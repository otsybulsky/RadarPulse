# Розділ 26: Чорний ящик RadarPulse

У складній системі збій майже завжди приходить як фінальний симптом, а не як акуратно підписана причина. Тому потрібен чорний ящик: не красива історія, а послідовність фактів, яка показує момент першої аномалії, стан системи і рішення, яке вона прийняла.

У програмних системах роль чорного ящика часто помилково віддають логам. Розробник додає `Console.WriteLine`, потім детальний режим (verbose mode), а після першої аварії production отримує або тишу, або лавину тексту, в якій неможливо знайти першу причину. Лог стає шумом, якщо за ним немає моделі.

RadarPulse підходить до цієї теми інакше. Поточна система ще **не заявляє продукційний стек логування (production logging stack)**: у production-контурі немає власного structured logging adapter-а, OpenTelemetry pipeline, розподіленого трасування (distributed tracing) чи централізованої доставки логів (centralized log shipping). CLI-вивід і тестове `AddLogging` не є таким стеком. Це не приховується і не продається як готова платформа спостережуваності (observability platform). Зате в системі вже є те, без чого справжнє логування все одно було б шумом: стабільна мова діагностики, готовність (readiness), перша причина блокування (first blocking reason), доказ місткості (capacity evidence), утриманий тиск пам'яті (retained pressure), стан durable envelope і діагностика для продуктової поверхні (product-facing diagnostics).

Цей розділ пояснює, чому це не дрібниця. Логування починається не з бібліотеки. Воно починається з рішення: **які факти система зобов'язана сказати про себе, коли вона працює, сповільнюється або відмовляється продовжувати**.

---

## 26.1. Лог не є доказом, якщо він не має моделі

Найпростіший шлях виглядав привабливо: поставити лог у кожен важливий метод. Пакет прийнято. Батч створено. Payload скопійовано. Worker стартував. Worker завершив. Delta готова. Commit пройшов. Envelope змінив стан. UI оновився.

На демо це виглядає переконливо. На реальному hot path це швидко стає проблемою.

RadarPulse працює з радарними архівами, де один файл може розпадатися на тисячі структурованих подій, а продуктивність вимірюється сотнями мільйонів payload-значень/с (payload values/s) у локальному benchmark-контурі. Якщо логувати кожен event або кожну payload-операцію, ми вже не спостерігаємо систему. Ми змінюємо її фізику. Диск, форматування рядків, lock-и всередині sink-а, backpressure логера і pressure на GC стають частиною workload-а.

Тому перше правило чорного ящика RadarPulse звучить жорстко:

```text
hot path не повинен доводити свою коректність потоком тексту
```

Hot path має доводити її контрактами даних, підсумковими метриками, станом готовності (readiness state) і обмеженим діагностичним доказом (bounded diagnostic evidence). Саме це видно в поточному коді.

[`RadarProcessingRunDiagnosticsReadModel`](../../../src/Application/Processing/ReadModels/RadarProcessingRunDiagnosticsReadModel.cs) збирає не випадкові повідомлення, а компактний стан запуску:

```csharp
public bool ProcessingCompletenessPassed { get; }
public RadarProcessingProviderQueueTelemetrySummary QueueTelemetry { get; }
public RadarProcessingDurableRuntimeReadinessSummary Readiness { get; }
public bool IsReady => ProcessingCompletenessPassed &&
                       Readiness.IsReady &&
                       !HandlerOutputBlocked;
public string BlockingReason => ...
public IReadOnlyList<string> Warnings => warnings;
```

Це не приймач логів (log sink). Це ще важливіше: словник фактів, які потім можуть бути записані в структуровані логи (structured logs), метричні підсумки, трасування (traces), API responses або UI. Якщо цей словник неправильний, жоден OpenTelemetry collector не врятує систему від самообману.

---

## 26.2. П'ять різних мов спостереження

У книзі RadarPulse вже є performance gates, BFF diagnostics, readiness, CLI output і demo verification. Їх легко звалити в одну корзину й назвати “логуванням”. Це було б помилкою.

Система має говорити кількома різними мовами:

| Мова | Для чого потрібна | Поточний стан RadarPulse |
| :--- | :--- | :--- |
| **Діагностика (diagnostics)** | Пояснити стан одного run-а: ready/blocked, first blocking reason, warnings, режим handler-а (handler posture) | Реалізовано через read models і product API |
| **Метричні підсумки (metric-like summaries)** | Показати лічильники, верхні межі (high-watermarks), allocation/capacity evidence і retained pressure | Реалізовано як типізовані телеметричні підсумки (telemetry summaries) і capacity evidence; production metrics exporter не заявлено |
| **Структуровані логи (structured logs)** | Дати дописувану хронологію важливих transitions із correlation fields | Не заявлено як готовий шар |
| **Траси (traces)** | Зв'язати операцію через компоненти, процеси й майбутні зовнішні адаптери (external adapters) | Не заявлено як готовий шар |
| **Аудиторський слід (audit trail)** | Зберегти product history і рішення оператора для пізнішого перегляду | Частково є в локальній run history/demo surface |

Це розділення важливе для чесності книги. `Console.WriteLine` у CLI — це не продукційне логування (production logging). HTTP endpoint `/product/pipeline/runs/{runId}/diagnostics` — це не розподілена траса (distributed trace). `FirstBlockingReason` — це не повний стек-трейс (stack trace). Але всі вони можуть бути частинами одного дорослого контракту спостережуваності (observability contract), якщо не плутати їхні ролі.

RadarPulse вже зробив правильний перший крок: він не почав із зовнішнього інструмента. Він почав із внутрішньої мови стану.

---

## 26.3. First Blocking Reason: одна причина замість хору симптомів

У складній системі збій майже ніколи не приходить один. Якщо durable envelope завис у `Claimed`, retained payload лишився у пам'яті, обробник не видав handler output, а UI показав заблокований стан (blocked state), можна легко отримати чотири різні повідомлення про одну проблему.

Для оператора це погано. Для рецензента теж. Сильна система має вміти сказати: ось перша причина, з якої я не готова.

Цей принцип проходить через кілька шарів RadarPulse.

[`RadarProcessingDurableRuntimeReadinessSummary`](../../../src/Domain/Processing/Durable/Models/RadarProcessingDurableRuntimeReadinessSummary.cs) зводить durable queue і retained-resource evidence до readiness:

```csharp
public bool IsReady =>
    !HasUncommittedEnvelope &&
    !HasBlockingEnvelope &&
    !HasReleaseFailures &&
    !HasTerminalRetainedPressure;

public string BlockingReason
{
    get
    {
        if (HasBlockingEnvelope)
        {
            return FirstBlockingReason;
        }
        ...
    }
}
```

[`RadarProcessingProductionPipelineOperatorSummary`](../../../src/Infrastructure/Processing/ProductPipeline/Models/RadarProcessingProductionPipelineOperatorSummary/RadarProcessingProductionPipelineOperatorSummary.cs) робить ще один крок: перетворює перший blocker на рекомендацію fallback (резервної дії). Якщо конфігурація невалідна, дія одна. Якщо durable envelope застряг у `Claimed`, дія інша. Якщо retained pressure не відпущено, оператор не має гадати, чи це проблема UI, брокера або GC.

```csharp
if (!configuration.IsValid)
{
    return new Blocker(
        $"invalid configuration: {configuration.FirstInvalidOption}",
        RadarProcessingProductionPipelineFallbackRecommendation.FixConfiguration);
}

if (!durableReadiness.IsReady)
{
    return new Blocker(
        durableReadiness.BlockingReason,
        MapDurableFallback(durableReadiness));
}
```

Це і є зародок structured logging. У production-версії кожен такий blocker міг би ставати структурованою подією (structured event):

```text
event=run.blocked
runId=product-blocked
reason="invalid configuration: WorkerCount"
fallback=FixConfiguration
component=product-pipeline
```

Але важливо, що поточна книга не видає цей майбутній event за вже реалізований конвеєр логування (log pipeline). Поточний доказ інший: **система вже має стабільну модель причин**, яку можна логувати без вигадування семантики на льоту.

---

## 26.4. Bounded diagnostics: пам'ять важливіша за балакучість

У продуктивній системі небезпечно не лише мовчати. Небезпечно говорити без ліміту.

Уявімо, що під час шторму черга починає давати backpressure. Якщо ми збережемо кожну дрібну подію enqueue/dequeue, diagnostic layer сам стане причиною retained pressure. RadarPulse не має права боротися з GC-кризою в розділах 11-13, а потім повертати її через безмежне логування.

Тому телеметричний підсумок (telemetry summary) у поточній системі вже має форму обмеженого доказу (bounded evidence):

```csharp
public long EnqueueAttemptCount { get; }
public long EnqueuedBatchCount { get; }
public long EnqueueFullCount { get; }
public long EnqueueTimedOutCount { get; }
public int QueueDepthHighWatermark { get; }
public long CurrentCombinedRetainedPayloadBytes { get; }
public IReadOnlyList<RadarProcessingProviderQueueRecentDetail> RecentDetails { get; }
public long DroppedRecentDetailCount { get; }
```

Цей набір говорить дві важливі речі.

По-перше, основний доказ живе в агрегатах: лічильниках (counts), верхніх межах (high-watermarks), сумарному часі очікування (total wait time) і утриманому тиску пам'яті (retained pressure). Вони не ростуть пропорційно кожному payload value.

По-друге, якщо потрібні останні деталі (recent details), вони мають межу утримання (retention boundary). [`DroppedRecentDetailCount`](../../../src/Domain/Processing/Queueing/Telemetry/RadarProcessingProviderQueueTelemetrySummary.cs) не приховує, що частину detail-рівня відкинуто. Це чесніше, ніж удавати нескінченний бюджет пам'яті (memory budget).

Саме тут logging-питання стає інженерним, а не декоративним. Production structured logs для RadarPulse мають наслідувати цей принцип:

* логувати transitions, не кожен байт;
* зберігати counters для масових подій;
* мати sampling/rate limits (вибірку та обмеження частоти) для повторюваних відмов (repetitive failures);
* явно рахувати відкинуту діагностику (dropped diagnostics);
* не дозволяти observability шару створювати нову retained-payload кризу.

Якщо майбутній `ILogger`-адаптер порушить ці правила, він буде регресією, навіть якщо виглядатиме “enterprise” (корпоративно).

---

## 26.5. Чому ми не почали з OpenTelemetry

OpenTelemetry, structured logging, експортери метрик (metrics exporters) і збирачі трас (trace collectors) — правильні інструменти для production. Але неправильний момент їх введення може створити красиву обгортку навколо неготового змісту.

Для RadarPulse було три можливі шляхи.

**Шлях 1: логувати все відразу.**

Перевага очевидна: у демо багато тексту, рецензент бачить “активність”. Недолік серйозніший: hot path починає платити за спостереження, а більшість рядків не відповідають на питання “що перше зламалося?”.

**Шлях 2: поставити OpenTelemetry перед тим, як стабілізувати runtime semantics.**

Це дало б знайомі слова: span-и, метрики й експортери (spans, metrics, exporters). Але tracing без чіткого `runId`, provider sequence, topology version, envelope state і first blocking reason не пояснює систему. Він лише малює траєкторію невідомих об'єктів.

**Шлях 3: спочатку зробити діагностичний контракт (diagnostic contract).**

Цей шлях менш ефектний, зате сильніший. Спочатку система вчиться називати свої інваріанти: скільки прийнято, скільки оброблено, чи всі зафіксовані (committed), чи є retained pressure, який envelope блокує readiness, який режим handler-а (handler posture) використано, яка перша причина блокування. Лише після цього structured logging стає простим адаптерним шаром (adapter layer), а не спробою вигадати сенс у sink-у.

RadarPulse обрав третій шлях.

Ціна вибору: у поточній книзі не можна чесно сказати “production observability implemented” (продукційну спостережуваність реалізовано).

Виграш: коли цей шар з'явиться, він матиме що логувати.

---

## 26.6. Яким має бути контракт продукційного логування (production logging contract)

Якщо завтра RadarPulse треба переносити з lab-table стенда в production, logging/observability має бути першим hardening-кроком перед broker/database/live ingestion (брокер/база даних/live-приймання). Але цей крок має бути точним.

Для майбутнього продукційного logging-adapter-а мінімальна структурована подія (structured event) для важливих переходів рантайму (runtime transitions) має спиратися на такі поля:

| Поле | Навіщо |
| :--- | :--- |
| `runId` | Зв'язує CLI/API/UI/history/capacity evidence |
| [`providerSequence`](../../../src/Domain/Processing/Queueing/Models/RadarProcessingQueuedBatchSequence.cs) | Показує місце батча у впорядкованому конвеєрі (ordered pipeline) |
| `sourceId` | Дає локалізацію по джерелу радарних подій |
| `partitionId` / `shardId` | Пояснює routing і rebalance контекст |
| [`topologyVersion`](../../../src/Domain/Processing/Topology/Models/RadarProcessingTopologyVersion.cs) | Відрізняє застаріле обчислення (stale compute) від актуального commit |
| `envelopeId` / `envelopeState` | Зв'язує стійке відновлення (durable recovery) із runtime transition |
| `handlerMode` | Пояснює mergeable fast path, sequential fallback або unsupported block |
| `retainedPayloadBytes` | Дає memory-pressure контекст |
| `firstBlockingReason` | Називає першу причину зупинки без ручного archaeology |

Не кожен event має містити всі поля. І це не поточний API логування RadarPulse, а цільова карта для production-hardening кроку. Але схема продукційного логу (production log schema) має знати ці осі, інакше при першому складному інциденті команда знову опиниться перед купою рядків без карти.

Архітектурно це не повинно затягувати логування у Domain. Domain має й надалі повертати типізовані результати, підсумки й готовність (typed results, summaries and readiness). Шар Application/Product може збирати діагностичні моделі читання (diagnostic read models). Шар Infrastructure/Presentation може перетворювати ці факти на `ILogger` scopes, metrics instruments або OpenTelemetry spans.

Правильний напрям залежностей виглядає так:

```text
Domain result/summary
    -> Application diagnostics/read model
        -> Product API / CLI / UI
        -> Infrastructure logging/metrics/tracing adapter
```

Неправильний напрям:

```text
Domain hot path -> direct log sink -> production semantics hidden in strings
```

Другий варіант здається швидшим, але він порушує те, що книга будувала з перших розділів: домен не повинен залежати від інфраструктурної кухні, а evidence не повинен жити тільки в текстовому виводі.

---

## 26.7. Як це має читати рецензент

Сильний reviewer може атакувати цей розділ дуже просто:

> “Де ваші логи?”

Слабка відповідь була б: “Ми можемо легко додати `ILogger`”. Це нічого не доводить.

Сильна відповідь інша:

```text
У поточній версії ми не заявляємо продукційне логування (production logging).
Ми вже маємо діагностичний контракт (diagnostic contract): readiness, blocker, queue telemetry,
durable state, retained pressure, доказ місткості (capacity evidence) і явно названі межі відповідальності.
Наступний production-hardening крок — адаптер structured logging/metrics/tracing,
який експортує ці типізовані факти без зміни гарячого доменного шляху (Domain hot path).
```

Це саме той тип відповіді, який відрізняє зрілу інженерну роботу від тексту, що просто перелічує назви інструментів. Інструмент можна підключити за день. Правильну модель подій, межі шуму, correlation vocabulary і first-blocker discipline треба спроектувати.

У RadarPulse ця модель уже почала існувати. Її ще треба винести в справжній production observability stack, але книга тепер не залишає це питання за кадром.

---

## Матеріали справи (Investigation Case Files)

### 1. Вердикт детективів (Decision Trace & Rationale)

Поточна система не заявляє продукційний стек логування (production logging stack). Її доведене рішення інше: спершу сформувати типізований diagnostic/readiness contract, який пояснює стан run-а без залежності від текстового приймача логів (log sink). Це робить майбутній `ILogger`/OpenTelemetry шар адаптером над готовою семантикою, а не місцем, де семантика вигадується.

#### Чому observability починається з діагностичної моделі

Можна було додати `ILogger` у кожен сервіс і отримати знайомий вигляд production-системи. Але це дало б хибний сигнал: система начебто спостережувана, хоча перша причина блокування, retained pressure, режим handler-а (handler posture) і durable state лишалися б розкиданими по рядках. Ми обрали спочатку типізовану діагностику (typed diagnostics first). Ціна вибору — немає заяви “production logging ready” (продукційне логування готове); виграш — кожен майбутній log/metric/trace має джерело правди.

### 2. Закони фізики рантайму (System Invariants)

* **Hot path не логують як proof:** події високої щільності мають агрегуватися в counters/high-watermarks, а не перетворюватися на нескінченний текстовий stream.
* **First blocker має пріоритет:** оператор і API повинні бачити першу причину блокування, а не випадковий список симптомів.
* **Діагностика обмежена за задумом:** recent detail може бути обмежений, але dropped detail має рахуватися явно.
* **Логування не розширює заяви:** наявність приймача логів (log sink) не робить систему production-ready без gates, retention policy, correlation schema і incident workflow.

### 3. Патологоанатомічний звіт (Failure Modes & Recovery)

* **Шторм логів (log storm):** без rate limit/sampling масовий збій може створити більше I/O та GC pressure, ніж сам runtime.
* **Доказ тільки в рядках (string-only evidence):** якщо blocker існує тільки як текстовий рядок у логах, UI/API/recovery не можуть надійно використати його як контракт.
* **Втрачена кореляція (lost correlation):** без `runId`, sequence і durable envelope context production incident перетворюється на ручну археологію.
* **Хибна впевненість (false confidence):** OpenTelemetry без стабільної внутрішньої diagnostic model дає гарні dashboards, але не пояснює коректність.

### 4. Слід доказової бази (Implementation & Tests)

* Run diagnostics model: [RadarProcessingRunDiagnosticsReadModel.cs](../../../src/Application/Processing/ReadModels/RadarProcessingRunDiagnosticsReadModel.cs)
* Run read-model builder: [RadarProcessingRunReadModelBuilder.cs](../../../src/Application/Processing/Services/RadarProcessingRunReadModelBuilder.cs)
* Queue telemetry summary: [RadarProcessingProviderQueueTelemetrySummary.cs](../../../src/Domain/Processing/Queueing/Telemetry/RadarProcessingProviderQueueTelemetrySummary.cs)
* Durable readiness summary: [RadarProcessingDurableRuntimeReadinessSummary.cs](../../../src/Domain/Processing/Durable/Models/RadarProcessingDurableRuntimeReadinessSummary.cs)
* Operator blocker mapping: [RadarProcessingProductionPipelineOperatorSummary.Blocking.cs](../../../src/Infrastructure/Processing/ProductPipeline/Models/RadarProcessingProductionPipelineOperatorSummary/RadarProcessingProductionPipelineOperatorSummary.Blocking.cs)
* Capacity evidence: [RadarProcessingProductionPipelineCapacityEvidence.cs](../../../src/Infrastructure/Processing/ProductPipeline/Telemetry/RadarProcessingProductionPipelineCapacityEvidence.cs)
* Product demo readiness: [RadarPulseProductDemoReadiness.cs](../../../src/Presentation/RadarPulse.Http/Product/Readiness/RadarPulseProductDemoReadiness.cs)
* Diagnostics tests: [RadarProcessingRunReadModelTests.cs](../../../tests/RadarPulse.Tests/Processing/ReadModels/RadarProcessingRunReadModelTests.cs)
* Operator summary tests: [RadarProcessingProductionPipelineSummaryTests.cs](../../../tests/RadarPulse.Tests/Processing/ProductPipeline/RadarProcessingProductionPipelineSummaryTests.cs)
* Product DTO tests: [RadarPulseProductPipelineDtoTests.cs](../../../tests/RadarPulse.Tests/Product/Pipeline/RadarPulseProductPipelineDtoTests.cs)

### 5. Протокол допиту процесу (Verification Commands)

Запуск focused-тестів, які перевіряють diagnostic/readiness/output mapping:

```bash
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~RadarProcessingRunReadModelTests|FullyQualifiedName~RadarProcessingProductionPipelineSummaryTests|FullyQualifiedName~RadarPulseProductPipelineDtoTests"
```

Окремий production-hardening gate для майбутнього logging layer має з'явитися тільки після додавання structured log/metric/trace adapter-а. Для поточної редакції книги чесна межа твердження така: діагностичний контракт (diagnostic contract), readiness і capacity evidence.

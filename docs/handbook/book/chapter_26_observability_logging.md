# Розділ 26: Чорний ящик RadarPulse

Коли літак падає, слідчі не починають із пресрелізу. Вони шукають чорний ящик. Їм потрібна не красива історія екіпажу, а послідовність фактів: висота, швидкість, команди, попередження, момент першої аномалії. Без цього будь-яке пояснення звучить переконливо рівно до першого серйозного питання.

У програмних системах роль чорного ящика часто помилково віддають логам. Розробник додає `Console.WriteLine`, потім ще кілька повідомлень, потім verbose mode, а після першої аварії production отримує або тишу, або лавину тексту, в якій неможливо знайти першу причину. Лог стає щоденником нервової людини, а не доказом.

RadarPulse підходить до цієї теми інакше. Поточна система ще **не заявляє production logging stack**: у коді немає окремого шару `ILogger`, OpenTelemetry, distributed tracing чи centralized log shipping. Це не приховується і не продається як готова observability-платформа. Зате в системі вже є те, без чого справжнє логування все одно було б шумом: стабільна мова діагностики, readiness, first blocking reason, capacity evidence, retained pressure, durable envelope state і product-facing diagnostics.

Цей розділ пояснює, чому це не дрібниця. Логування починається не з бібліотеки. Воно починається з рішення: **які факти система зобов'язана сказати про себе, коли вона працює, сповільнюється або відмовляється продовжувати**.

---

## 26.1. Лог не є доказом, якщо він не має моделі

Найпростіший шлях виглядав привабливо: поставити лог у кожен важливий метод. Пакет прийнято. Батч створено. Payload скопійовано. Worker стартував. Worker завершив. Delta готова. Commit пройшов. Envelope змінив стан. UI оновився.

На демо це виглядає переконливо. На реальному hot path це швидко стає проблемою.

RadarPulse працює з радарними архівами, де один файл може розпадатися на тисячі структурованих подій, а продуктивність вимірюється сотнями мільйонів payload values/s у локальному benchmark-контурі. Якщо логувати кожен event або кожну payload-операцію, ми вже не спостерігаємо систему. Ми змінюємо її фізику. Диск, форматування рядків, lock-и всередині sink-а, backpressure логера і pressure на GC стають частиною workload-а.

Тому перше правило чорного ящика RadarPulse звучить жорстко:

```text
hot path не повинен доводити свою коректність потоком тексту
```

Hot path має доводити її контрактами даних, підсумковими метриками, readiness state і bounded diagnostic evidence. Саме це видно в поточному коді.

`RadarProcessingRunDiagnosticsReadModel` збирає не випадкові повідомлення, а компактний стан запуску:

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

Це не log sink. Це ще важливіше: словник фактів, які потім можуть бути записані в structured logs, metrics, traces, API responses або UI. Якщо цей словник неправильний, жоден OpenTelemetry collector не врятує систему від самообману.

---

## 26.2. П'ять різних мов спостереження

У книзі RadarPulse вже є performance gates, BFF diagnostics, readiness, CLI output і demo verification. Їх легко звалити в одну корзину й назвати “логуванням”. Це було б помилкою.

Система має говорити кількома різними мовами:

| Мова | Для чого потрібна | Поточний стан RadarPulse |
| :--- | :--- | :--- |
| **Diagnostics** | Пояснити стан одного run-а: ready/blocked, first blocking reason, warnings, handler posture | Реалізовано через read models і product API |
| **Metrics** | Показати лічильники, high-watermarks, allocation/capacity evidence, retained pressure | Реалізовано у telemetry summaries і capacity evidence |
| **Structured logs** | Дати append-only хронологію важливих transitions з correlation fields | Не заявлено як готовий шар |
| **Traces** | Зв'язати операцію через компоненти, процеси й майбутні external adapters | Не заявлено як готовий шар |
| **Audit trail** | Зберегти product history і рішення оператора для пізнішого перегляду | Частково є в local run history/demo surface |

Це розділення важливе для чесності книги. `Console.WriteLine` у CLI — це не production logging. HTTP endpoint `/product/pipeline/runs/{runId}/diagnostics` — це не distributed trace. `FirstBlockingReason` — це не повний stack trace. Але всі вони можуть бути частинами одного дорослого observability contract, якщо не плутати їхні ролі.

RadarPulse вже зробив правильний перший крок: він не почав із зовнішнього інструмента. Він почав із внутрішньої мови стану.

---

## 26.3. First Blocking Reason: одна причина замість хору симптомів

У складній системі збій майже ніколи не приходить один. Якщо durable envelope завис у `Claimed`, retained payload лишився у пам'яті, обробник не видав handler output, а UI показав blocked state, можна легко отримати чотири різні повідомлення про одну проблему.

Для оператора це погано. Для рецензента теж. Сильна система має вміти сказати: ось перша причина, з якої я не готова.

Цей принцип проходить через кілька шарів RadarPulse.

`RadarProcessingDurableRuntimeReadinessSummary` зводить durable queue і retained-resource evidence до readiness:

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

`RadarProcessingProductionPipelineOperatorSummary` робить ще один крок: перетворює перший blocker на fallback recommendation. Якщо конфігурація невалідна, дія одна. Якщо durable envelope застряг у `Claimed`, дія інша. Якщо retained pressure не відпущено, оператор не має гадати, чи це проблема UI, брокера або GC.

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

Це і є зародок structured logging. У production-версії кожен такий blocker міг би ставати structured event:

```text
event=run.blocked
runId=product-blocked
reason="invalid configuration: WorkerCount"
fallback=FixConfiguration
component=product-pipeline
```

Але важливо, що поточна книга не видає цей майбутній event за вже реалізований log pipeline. Поточний доказ інший: **система вже має стабільну модель причин**, яку можна логувати без вигадування семантики на льоту.

---

## 26.4. Bounded diagnostics: пам'ять важливіша за балакучість

У продуктивній системі небезпечно не лише мовчати. Небезпечно говорити без ліміту.

Уявімо, що під час шторму черга починає давати backpressure. Якщо ми збережемо кожну дрібну подію enqueue/dequeue, diagnostic layer сам стане причиною retained pressure. RadarPulse не має права боротися з GC-кризою в розділах 11-13, а потім повертати її через безмежне логування.

Тому telemetry summary у поточній системі вже має форму bounded evidence:

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

По-перше, основний доказ живе в агрегатах: counts, high-watermarks, total wait time, retained pressure. Вони не ростуть пропорційно кожному payload value.

По-друге, якщо потрібні recent details, вони мають retention boundary. `DroppedRecentDetailCount` не приховує, що частину detail-рівня відкинуто. Це чесніше, ніж удавати нескінченний memory budget.

Саме тут logging-питання стає інженерним, а не декоративним. Production structured logs для RadarPulse мають наслідувати цей принцип:

* логувати transitions, не кожен байт;
* зберігати counters для масових подій;
* мати sampling/rate limits для repetitive failures;
* явно рахувати dropped diagnostics;
* не дозволяти observability шару створювати нову retained-payload кризу.

Якщо майбутній `ILogger` adapter порушить ці правила, він буде регресією, навіть якщо виглядатиме “enterprise”.

---

## 26.5. Чому ми не почали з OpenTelemetry

OpenTelemetry, structured logging, metrics exporters і trace collectors — правильні інструменти для production. Але неправильний момент їх введення може створити красиву обгортку навколо неготового змісту.

Для RadarPulse було три можливі шляхи.

**Шлях 1: логувати все відразу.**

Перевага очевидна: у демо багато тексту, рецензент бачить “активність”. Недолік серйозніший: hot path починає платити за спостереження, а більшість рядків не відповідають на питання “що перше зламалося?”.

**Шлях 2: поставити OpenTelemetry перед тим, як стабілізувати runtime semantics.**

Це дало б знайомі слова: spans, metrics, exporters. Але tracing без чіткого `runId`, provider sequence, topology version, envelope state і first blocking reason не пояснює систему. Він лише малює траєкторію невідомих об'єктів.

**Шлях 3: спочатку зробити diagnostic contract.**

Цей шлях менш ефектний, зате сильніший. Спочатку система вчиться називати свої інваріанти: скільки прийнято, скільки оброблено, чи всі committed, чи є retained pressure, який envelope блокує readiness, який handler posture використано, яка перша причина блокування. Лише після цього structured logging стає простим adapter layer, а не спробою вигадати сенс у sink-у.

RadarPulse обрав третій шлях.

Ціна вибору: у поточній книзі не можна чесно сказати “production observability implemented”.

Виграш: коли цей шар з'явиться, він матиме що логувати.

---

## 26.6. Яким має бути production logging contract

Якщо завтра RadarPulse треба переносити з lab-table стенда в production, logging/observability має бути першим hardening-кроком перед broker/database/live ingestion. Але цей крок має бути точним.

Мінімальний structured event для важливих runtime transitions має нести такі поля:

| Поле | Навіщо |
| :--- | :--- |
| `runId` | Зв'язує CLI/API/UI/history/capacity evidence |
| `providerSequence` | Показує місце батча в ordered pipeline |
| `sourceId` | Дає локалізацію по джерелу радарних подій |
| `partitionId` / `shardId` | Пояснює routing і rebalance контекст |
| `topologyVersion` | Відрізняє stale compute від актуального commit |
| `envelopeId` / `envelopeState` | Зв'язує durable recovery із runtime transition |
| `handlerMode` | Пояснює mergeable fast path, sequential fallback або unsupported block |
| `retainedPayloadBytes` | Дає memory-pressure контекст |
| `firstBlockingReason` | Називає першу причину зупинки без ручного archaeology |

Не кожен event має містити всі поля. Але production log schema має знати ці осі, інакше при першому складному інциденті команда знову опиниться перед купою рядків без карти.

Архітектурно це не повинно затягувати logging у Domain. Domain має й надалі повертати typed results, summaries and readiness. Application/Product layer може збирати diagnostic read models. Infrastructure/Presentation layer може перетворювати ці факти на `ILogger` scopes, metrics instruments або OpenTelemetry spans.

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
У поточній версії ми не claim-имо production logging.
Ми вже маємо diagnostic contract: readiness, blocker, queue telemetry,
durable state, retained pressure, capacity evidence and non-claims.
Наступний production-hardening крок — structured logging/metrics/tracing
adapter, який експортує ці typed facts без зміни Domain hot path.
```

Це саме той тип відповіді, який відрізняє senior engineer-а від кандидата, що просто знає назви інструментів. Інструмент можна підключити за день. Правильну модель подій, межі шуму, correlation vocabulary і first-blocker discipline треба спроектувати.

У RadarPulse ця модель уже почала існувати. Її ще треба винести в справжній production observability stack, але книга тепер не залишає це питання за кадром.

---

## Матеріали справи (Investigation Case Files)

### 1. Вердикт детективів (Decision Trace & Rationale)

Поточна система не заявляє production logging stack. Її доведене рішення інше: спершу сформувати typed diagnostic/readiness contract, який пояснює стан run-а без залежності від текстового log sink-а. Це робить майбутній `ILogger`/OpenTelemetry шар адаптером над готовою семантикою, а не місцем, де семантика вигадується.

#### Чому observability починається з діагностичної моделі

Можна було додати `ILogger` у кожен сервіс і отримати знайомий production-looking вигляд. Але це дало б хибний сигнал: система начебто спостережувана, хоча перша причина блокування, retained pressure, handler posture і durable state лишалися б розкиданими по рядках. Ми обрали typed diagnostics first. Ціна вибору — немає claim-а “production logging ready”; виграш — кожен майбутній log/metric/trace має джерело правди.

### 2. Закони фізики рантайму (System Invariants)

* **Hot path не логують як proof:** події високої щільності мають агрегуватися в counters/high-watermarks, а не перетворюватися на нескінченний текстовий stream.
* **First blocker має пріоритет:** оператор і API повинні бачити першу причину блокування, а не випадковий список симптомів.
* **Diagnostics bounded by design:** recent detail може бути обмежений, але dropped detail має рахуватися явно.
* **Logging не розширює claims:** наявність log sink-а не робить систему production-ready без gates, retention policy, correlation schema і incident workflow.

### 3. Патологоанатомічний звіт (Failure Modes & Recovery)

* **Log storm:** без rate limit/sampling масовий збій може створити більше I/O та GC pressure, ніж сам runtime.
* **String-only evidence:** якщо blocker існує тільки як текстовий рядок у логах, UI/API/recovery не можуть надійно використати його як контракт.
* **Lost correlation:** без `runId`, sequence і durable envelope context production incident перетворюється на ручну археологію.
* **False confidence:** OpenTelemetry без стабільної внутрішньої diagnostic model дає гарні dashboards, але не пояснює correctness.

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

Окремий production-hardening gate для майбутнього logging layer має з'явитися тільки після додавання structured log/metric/trace adapter-а. Для поточної редакції книги чесний claim обмежується diagnostic contract, readiness і capacity evidence.

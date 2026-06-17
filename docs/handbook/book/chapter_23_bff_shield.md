# Розділ 23: Щит для фронтенду (бекенд для фронтенду, Backend-for-Frontend)

Внутрішній рантайм RadarPulse не є форматом для браузера. Там живуть [`DurableEnvelope`](../../../src/Infrastructure/Processing/Durable/Services/RadarProcessingDurableEnvelopeQueue/RadarProcessingDurableEnvelopeQueue.cs), утриманий тиск пам'яті (retained pressure), версії топології, стани worker-ів, pooled buffers і координатори комміту. Якщо віддати це напряму Angular SPA, фронтенд швидко стане заручником внутрішньої кухні ядра.

Тому під час віхи `028` з'явився **бекенд для фронтенду (Backend-for-Frontend, BFF)**: тонкий продуктовий шар, який перекладає стан системи на мову оператора. Він не приховує правду і не малює production-ілюзію; він віддає стабільні DTO/read models, readiness, diagnostics, warnings і [`NonClaims`](../../../src/Presentation/RadarPulse.Http/Product/Readiness/RadarPulseProductDemoReadiness.cs), не відкриваючи клієнту приватні моделі рантайму.

---

## 23.1. Конструкція щита: Мінімальний API ([`RadarPulseProductHttpEndpoints`](../../../src/Presentation/RadarPulse.Http/Product/Endpoints/RadarPulseProductHttpEndpoints.cs))

Технічним фундаментом BFF є Minimal API, розгорнутий у проекті [`RadarPulse.Http`](../../../src/Presentation/RadarPulse.Http/RadarPulse.Http.csproj). Він мапує групу ендпоінтів `/product/pipeline` і виступає тонким адаптером поверх внутрішнього інтерфейсу [`IRadarPulseProductPipelineApi`](../../../src/Application/Product/Pipeline/Contracts/RadarPulseProductPipelineContracts.cs):

```csharp
public static class RadarPulseProductHttpEndpoints
{
    public static IEndpointRouteBuilder MapRadarPulseProductPipeline(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/product/pipeline");

        // Керування запусками
        group.MapPost("/runs/demo", RunDemoAsync);
        group.MapPost("/runs/archive", RunArchiveAsync);
        group.MapGet("/runs", ListRuns);
        group.MapGet("/runs/latest", GetLatestRun);
        group.MapGet("/runs/{runId}", GetRun);

        // Доступ до product/read-model проекцій обробки
        group.MapGet("/runs/{runId}/batches", ListBatches);
        group.MapGet("/runs/{runId}/batches/{providerSequence:long}", GetBatch);
        group.MapGet("/runs/{runId}/sources", ListSources);
        group.MapGet("/runs/{runId}/sources/{sourceId:int}", GetSource);
        group.MapGet("/runs/{runId}/handlers/{sourceId:int}/{fieldName}", GetHandlerOutput);

        // Діагностика та готовність хосту
        group.MapGet("/runs/{runId}/diagnostics", GetDiagnostics);
        group.MapGet("/runs/{runId}/capacity", GetCapacityEvidence);
        group.MapGet("/host/readiness", GetHistoryReadiness);
        group.MapGet("/host/demo-readiness", GetDemoReadiness);

        // Пульт ручного керування (Product Controls)
        group.MapPost("/controls/stop-accepting", StopAcceptingAsync);
        group.MapPost("/controls/drain-accepted", DrainAcceptedAsync);
        group.MapPost("/controls/cancel-open-release", CancelOpenAndReleaseAsync);
        group.MapPost("/controls/reject-unsafe-fallback", RejectUnsafeFallbackAsync);

        return endpoints;
    }

    // Деталі реалізації методів...
}
```

Ця структура ендпоінтів — це панель приладів нашого слідчого. Зверніть увагу на групу `/controls/`. Це механізм активного втручання в «кримінальну справу» обробки даних. BFF дозволяє оператору вручну зупинити прийом нових пакетів (`stop-accepting`), дати системі допрацювати вже прийняті батчі (`drain-accepted`) або відхилити небезпечний режим роботи з послідовним падінням продуктивності (`reject-unsafe-fallback`). При цьому фронтенд не знає, як саме ці команди реалізовані всередині доменного ядра; він просто надсилає POST-запити з легким тілом DTO.

---

## 23.2. Сигналізація готовності: Карта дефектів ([`RadarPulseProductDemoReadiness`](../../../src/Presentation/RadarPulse.Http/Product/Readiness/RadarPulseProductDemoReadiness.cs))

Одним із ключових завдань BFF є розрахунок сумарного стану системи — **Readiness State**. Коли користувач завантажує інтерфейс, він повинен миттєво зрозуміти: чи готова система до запуску нового аналітичного розслідування?

Замість того, щоб змушувати фронтенд самостійно опитувати стан диска, перевіряти наявність Angular-дистрибутива та підключення до API, BFF об'єднує всі ці перевірки в один об'єкт [`RadarPulseProductDemoReadiness`](../../../src/Presentation/RadarPulse.Http/Product/Readiness/RadarPulseProductDemoReadiness.cs) за допомогою методу `From`:

```csharp
public sealed record RadarPulseProductDemoReadiness(
    bool IsReady,
    string FirstBlockingReason,
    RadarPulseProductDemoReadinessItem ProductApi,
    RadarPulseProductDemoReadinessItem History,
    RadarPulseProductDemoReadinessItem OperatorUi,
    string HistoryPath,
    string OperatorUiStaticAssetPath,
    string OperatorUiStaticAssetRoot,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> NonClaims)
{
    public static RadarPulseProductDemoReadiness From(
        RadarPulseProductRunHistoryReadiness historyReadiness,
        RadarPulseProductHttpOptions options)
    {
        ArgumentNullException.ThrowIfNull(historyReadiness);
        ArgumentNullException.ThrowIfNull(options);

        // 1. Стан API завжди готовий, якщо маршрути успішно змаповані в рантаймі
        var productApi = new RadarPulseProductDemoReadinessItem(
            "product-api",
            true,
            "ready",
            "Product pipeline HTTP routes are mapped under /product/pipeline.",
            string.Empty);

        // 2. Стан історії (бази даних на диску)
        var history = new RadarPulseProductDemoReadinessItem(
            "history",
            historyReadiness.IsReady,
            historyReadiness.IsReady ? "ready" : "blocked",
            historyReadiness.StorageIdentity,
            historyReadiness.FirstBlockingReason);

        // 3. Стан статичних файлів інтерфейсу оператора
        var operatorUi = CreateOperatorUiItem(options, out var operatorUiStaticAssetRoot);

        // 4. Пошук найпершої причини блокування
        var firstBlockingReason = ResolveFirstBlockingReason(history, operatorUi);

        var warnings = historyReadiness.Warnings
            .Concat(PackageWarnings)
            .ToArray();

        return new RadarPulseProductDemoReadiness(
            history.IsReady && operatorUi.IsReady,
            firstBlockingReason,
            productApi,
            history,
            operatorUi,
            options.HistoryPath,
            options.OperatorUiStaticAssetPath,
            operatorUiStaticAssetRoot,
            warnings,
            PackageNonClaims);
    }
}
```

Метод [`ResolveFirstBlockingReason`](../../../src/Presentation/RadarPulse.Http/Product/Readiness/RadarPulseProductDemoReadiness.cs) працює за принципом каскадного фільтра. Якщо історія заблокована (наприклад, файл історії [`radarpulse-product-history.json`](../../../src/Infrastructure/Product/History/Stores/RadarPulseProductFileRunHistoryStore/RadarPulseProductFileRunHistoryStore.cs) пошкоджений або недоступний для запису), це стає першою причиною блокування (`FirstBlockingReason`). Якщо з історією все гаразд, але адміністратор не зібрав або неправильно підклав дистрибутив Angular UI, перевірка [`TryResolveOperatorUiStaticAssetRoot`](../../../src/Presentation/RadarPulse.Http/Product/StaticDelivery/RadarPulseOperatorUiStaticDeliveryExtensions.cs) не знаходить `index.html` у зібраному static asset root, і першою причиною стане помилка інтерфейсу. Фронтенд просто зчитує поле `IsReady` та рядок `FirstBlockingReason`, виводячи на екран красивий червоний або зелений банер.

---

## 23.3. Оптимізація трафіку: поточна межа BFF і наступний gate

У реальних сценаріях кожен метеорологічний радар NEXRAD генерує сотні тисяч точок даних за один повний оберт антени. Тут легко написати красиву обіцянку про плитки, heatmap-и, WebSocket-и та стиснення. Ми так не робимо. У поточній реалізації BFF не є tile server і не містить окремого `ResponseCompression` pipeline. Його реальна робота інша: перетворити внутрішній runtime стан на стабільні product/readiness DTO, які операторський UI може показати без доступу до пам'яті ядра.

Це свідомий вибір черги доказів. Спочатку ми доводимо, що зовнішній контракт не бреше: run summary, detail, capacity evidence, diagnostics, handler output, readiness і [`NonClaims`](../../../src/Presentation/RadarPulse.Http/Product/Readiness/RadarPulseProductDemoReadiness.cs) узгоджені з тим, що справді відбулося в pipeline. Трафікова оптимізація без стабільного DTO-контракту була б косметикою поверх рухомої підлоги.

Наступний BFF gate уже має зрозумілий предмет, але поки це не claim:
* **Downsampling/rasterization path:** сервер може перетворювати radar/event detail у агрегований візуальний зріз, якщо UI перейде від табличного read-model до живої карти.
* **HTTP compression path:** ASP.NET Core `ResponseCompression` з Brotli/GZip може стати транспортним запобіжником для великих JSON DTO, але тільки після окремого traffic benchmark.
* **Traffic proof:** вимірювати треба не настрій, а розмір DTO, час серіалізації, latency доставки і поведінку браузера на реальному operator workflow.

Поточний BFF не вдає з себе production telemetry CDN. Він робить важчу для захисту роботи річ: тримає межу правди між runtime і UI.

---

## 23.4. Запобіжні декларації: `Warnings` та [`NonClaims`](../../../src/Presentation/RadarPulse.Http/Product/Readiness/RadarPulseProductDemoReadiness.cs)

Наш BFF грає роль чесного детектива. Він не повинен приписувати собі подвигів, яких не здійснював. Оскільки система розроблена в концепції «Лабораторного столу» на одній машині, BFF відкрито транслює список обмежень відповідальності ([`NonClaims`](../../../src/Presentation/RadarPulse.Http/Product/Readiness/RadarPulseProductDemoReadiness.cs)) та попереджень (`Warnings`).

Ці списки зашиті в коді [`RadarPulseProductDemoReadiness`](../../../src/Presentation/RadarPulse.Http/Product/Readiness/RadarPulseProductDemoReadiness.cs) і передаються кожному клієнту:

```csharp
private static readonly string[] PackageWarnings =
[
    "Local demo/readiness packaging covers deterministic demo/archive-shaped workflows only.",
    "The same-origin host is a local RadarPulse.Http delivery path, not public production hosting."
];

private static readonly string[] PackageNonClaims =
[
    "true live radar network ingestion",
    "external broker/cloud queue/database adapter certification",
    "public production deployment",
    "authentication or authorization",
    "TLS termination",
    "production CORS hardening",
    "deployment automation or autoscaling",
    "cross-machine throughput certification",
    "exactly-once end-to-end production delivery"
];
```

Навіщо це потрібно? Якщо сторонній аудитор або розробник запустить систему і спробує розгорнути її на публічному сервері AWS під великим навантаженням, ці попередження нагадають йому: **RadarPulse розроблений для локальної верифікації та тестування алгоритмів**. BFF захищає систему від неправильного використання, чітко розмежовуючи межі відповідальності: ми показуємо детермінований локальний контур і контроль алокацій у нашому бункері, але не претендуємо на роль хмарної системи реального часу без додаткової інфраструктурної обв'язки.

BFF виступає надійним щитом: він захищає внутрішні класи від хаотичних запитів з браузера, забезпечує фронтенд чистими даними та чесно попереджає про межі своїх можливостей.

---

## 🔍 Матеріали справи (Investigation Case Files)

### 1. Вердикт детективів (Decision Trace & Rationale)
Створення захисного щита бекенду для фронтенду (Backend-for-Frontend, BFF) і моделей читання (read models) (Віха `028`). Контролери та DTO-моделі BFF ізолюють складну внутрішню кухню рантайму та черг повідомлень від клієнтських Angular SPA запитів, віддаючи назовні лише агреговані легкі зрізи стану.

#### Чому фронтенд не бачить внутрішню кухню
SPA могла напряму читати runtime-моделі, але тоді фронтенд отримав би durable envelopes, pooled pressure, topology internals і випадкові зміни доменних типів. Можна було зробити універсальний generic API, але він став би тонким обгортанням внутрішньої кухні. BFF обрано як перекладача для оператора: він віддає не все, що знає система, а те, що потрібно для рішення. Ціна вибору — додаткові DTO і read-model store; виграш — стабільний UI-контракт і захист семантики ядра (core semantics) від presentation-запитів.

### 2. Закони фізики рантайму (System Invariants)
* **Публікація read-моделі**: BFF-сховище [`RadarProcessingBffReadModelStore`](../../../src/Application/Processing/Services/RadarProcessingBffReadModelStore.cs) отримує готовий [`RadarProcessingRunReadModel`](../../../src/Application/Processing/ReadModels/RadarProcessingRunReadModel.cs) після того, як pipeline сформував результат виконання; воно не читає внутрішню durable queue напряму.
* **Ізоляція інтерфейсу**: Прямі запити з UI до внутрішніх об'єктів черги [`DurableEnvelopeQueue`](../../../src/Infrastructure/Processing/Durable/Services/RadarProcessingDurableEnvelopeQueue/RadarProcessingDurableEnvelopeQueue.cs) суворо заборонені.

### 3. Патологоанатомічний звіт (Failure Modes & Recovery)
* **Неопублікована або застаріла read-модель**: Якщо read model ще не опублікована або оператор дивиться на старий знімок, UI може показувати не найсвіжіший стан, але це не впливає на стабільність обробки радарних батчів.

### 4. Слід доказової бази (Implementation & Tests)
* BFF сховище: [RadarProcessingBffReadModelStore.cs](../../../src/Application/Processing/Services/RadarProcessingBffReadModelStore.cs)
* Тести BFF read-models: [RadarProcessingBffReadModelStoreTests.cs](../../../tests/RadarPulse.Tests/Processing/ReadModels/RadarProcessingBffReadModelStoreTests.cs)

### 5. Протокол допиту процесу (Verification Commands)
Запуск тестів цілісності BFF моделей представлення:
```bash
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~ReadModel"
```

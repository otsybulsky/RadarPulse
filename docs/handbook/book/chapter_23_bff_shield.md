# Розділ 23: Щит для фронтенду (Backend-for-Frontend)

Жоден розумний шериф не допустить журналістів у кімнату зберігання речових доказів чи до журналів прослуховування підозрюваних. У цих кімнатах панує робочий хаос: сирі папки з протоколами, відмічені номерами гільзи, технічні записи про тривалість зміни воркерів та складні логістичні індекси. Журналістам — як і широкому загалу — потрібен структурований, чіткий прес-реліз. Їм потрібен речник, який вийде на трибуну, відфільтрує шум і скаже людською мовою: «Ми заарештували трьох підозрюваних, докази зібрано, справа готова до передачі в суд. Ось перша причина затримки, якщо вона виникне».

У нашому програмному комплексі **RadarPulse** роль такого речника виконує шар **Backend-for-Frontend (BFF)**, реалізований у вісі `028`. Він виступає захисним щитом для нашого Angular-інтерфейсу (Presentation). Замість того, щоб навантажувати SPA-додаток обробкою складних графів внутрішнього рантайму, структурою черг `DurableEnvelope`, логікою пул-буферів чи станом координаторів, BFF згладжує ці кути. Він фільтрує сирі метрики, перетворюючи їх на прості, стабільні та строго типізовані об'єкти передачі даних (DTO — Read Models).

---

## 23.1. Конструкція щита: Мінімальний API (`RadarPulseProductHttpEndpoints`)

Технічним фундаментом BFF є Minimal API, розгорнутий у проекті `RadarPulse.Http`. Він мапує групу ендпоінтів `/product/pipeline` і виступає тонким адаптером поверх внутрішнього інтерфейсу `IRadarPulseProductPipelineApi`:

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

        // Доступ до внутрішнього стану обробки
        group.MapGet("/runs/{runId}/batches", ListBatches);
        group.MapGet("/runs/{runId}/sources", ListSources);
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

Ця структура ендпоінтів — це панель приладів нашого слідчого. Зверніть увагу на групу `/controls/`. Це механізм активного втручання в «кримінальну справу» обробки даних. BFF дозволяє оператору вручну зупинити прийом нових пакетів (`stop-accepting`), очистити чергу обробленого матеріалу (`drain-accepted`) або відхилити небезпечний режим роботи з послідовним падінням продуктивності (`reject-unsafe-fallback`). При цьому фронтенд не знає, як саме ці команди реалізовані всередині доменного ядра; він просто надсилає POST-запити з легким тілом DTO.

---

## 23.2. Сигналізація готовності: Карта дефектів (`RadarPulseProductDemoReadiness`)

Одним із ключових завдань BFF є розрахунок сумарного стану системи — **Readiness State**. Коли користувач завантажує інтерфейс, він повинен миттєво зрозуміти: чи готова система до запуску нового аналітичного розслідування?

Замість того, щоб змушувати фронтенд самостійно опитувати стан диска, перевіряти наявність Angular-дистрибутива та підключення до API, BFF об'єднує всі ці перевірки в один об'єкт `RadarPulseProductDemoReadiness` за допомогою методу `From`:

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

Метод `ResolveFirstBlockingReason` працює за принципом каскадного фільтра. Якщо історія заблокована (наприклад, файл історії `radarpulse-product-history.json` пошкоджений або недоступний для запису), це стає першою причиною блокування (`FirstBlockingReason`). Якщо з історією все гаразд, але адміністратор забув зібрати дистрибутив Angular UI (`index.html` відсутній у папці `dist`), першою причиною стане помилка інтерфейсу. Фронтенд просто зчитує поле `IsReady` та рядок `FirstBlockingReason`, виводячи на екран красивий червоний або зелений банер.

---

## 23.3. Запобіжні декларації: `Warnings` та `NonClaims`

Наш BFF грає роль чесного детектива. Він ніколи не приписує собі подвигів, яких не здійснював. Оскільки система розроблена в концепції «Лабораторного столу» на одній машині, BFF відкрито транслює список обмежень відповідальності (`NonClaims`) та попереджень (`Warnings`).

Ці списки зашиті в коді `RadarPulseProductDemoReadiness` і передаються кожному клієнту:

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

Навіщо це потрібно? Якщо сторонній аудитор або розробник запустить систему і спробує розгорнути її на публічному сервері AWS під великим навантаженням, ці попередження нагадають йому: **RadarPulse розроблений для локальної верифікації та тестування алгоритмів**. BFF захищає систему від неправильного використання, чітко розмежовуючи межі відповідальності: ми гарантуємо детерміновану роботу та нуль алокацій у нашому локальному бункері, але ми не претендуємо на роль хмарної системи реального часу без додаткової інфраструктурної обв'язки.

BFF виступає надійним щитом: він захищає внутрішні класи від хаотичних запитів з браузера, забезпечує фронтенд чистими даними та чесно попереджає про межі своїх можливостей.
---

## 🔍 Матеріали справи (Investigation Case Files)

### 1. Вердикт детективів (Decision Trace & Rationale)
Створення захисного щита Backend-for-Frontend (BFF) read-models (Віха `028`). Контролери та DTO-моделі BFF ізолюють складну внутрішню кухню рантайму та черг повідомлень від клієнтських Angular SPA запитів, віддаючи назовні лише агреговані легкі зрізи стану.

### 2. Закони фізики рантайму (System Invariants)
* **Незмінність read-моделі**: BFF-сховище `RadarProcessingBffReadModelStore` оновлюється виключно після фіксації Ordered Commit.
* **Ізоляція інтерфейсу**: Прямі запити з UI до внутрішніх об'єктів черги `DurableEnvelopeQueue` суворо заборонені.

### 3. Патологоанатомічний звіт (Failure Modes & Recovery)
* **Збій оновлення read-моделі**: У разі помилки оновлення BFF-сховища, оператор може бачити застарілі дані на екрані, але це жодним чином не впливає на стабільність обробки радарних батчів.

### 4. Слід доказової бази (Implementation & Tests)
* BFF сховище: [RadarProcessingBffReadModelStore.cs](../../../src/Application/Processing/Services/RadarProcessingBffReadModelStore.cs)
* Тести BFF read-models: [RadarProcessingBffReadModelStoreTests.cs](../../../tests/RadarPulse.Tests/Processing/ReadModels/RadarProcessingBffReadModelStoreTests.cs)

### 5. Протокол допиту процесу (Verification Commands)
Запуск тестів цілісності BFF моделей представлення:
```bash
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~ReadModels"
```

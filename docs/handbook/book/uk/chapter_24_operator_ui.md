# Розділ 24: Кабінет оператора

У кабінеті досвідченого детектива завжди є дошка стану. На ній видно, які справи відкриті, де бракує доказів, який протокол заблоковано і хто чекає рішення. Слідчий не повинен щоразу спускатися в архів або читати сирий JSON, щоб зрозуміти, чи готова експертиза. Йому потрібне єдине табло, яке показує стан системи без прикрас.

У віхах `030` та `031` ми створили таке табло для нашої системи — **Operator UI**. Це Single Page Application (SPA), побудоване на базі Angular, яке хоститься безпосередньо з нашого Minimal API. Воно надає оператору можливість візуалізувати стан рантайму, керувати запусками обробки радарних даних NEXRAD та реагувати на заблоковані або небезпечні стани.

---

## 24.1. Що саме вже реалізовано

Поточний Operator UI — це не макет майбутньої консолі, а робочий операторський інтерфейс поверх product API. Його функціональність можна розкласти на кілька практичних контурів.

* **Підключення до product host.** Верхня панель дозволяє задати адресу HTTP-хоста, зберегти її в локальному сховищі браузера і вручну оновити стан. За замовчуванням SPA працює як same-origin клієнт, але під час локальної розробки оператор може переключити її на інший host.
* **Readiness і останній запуск.** UI перевіряє готовність історії запусків, показує тип сховища, кількість завантажених і відхилених run-ів, а також `First Blocking Reason`, якщо product host не готовий до читання.
* **Оновлення за подією, а не live telemetry.** Показники не надходять потоково в реальному часі. UI завантажує їх при першому відкритті сторінки, після натискання `Refresh`, після запуску demo/archive run, після runtime control і під час вибору run-а або handler output. Фонового polling, WebSocket або SSE у поточній реалізації немає.
* **Створення запусків.** З екрана можна запустити детермінований demo run із параметрами `runId`, кількість sources, batches і events per batch, або archive run за шляхом до NEXRAD-файлу на диску.
* **Ручний навантажувальний прогін, але не load-test harness.** Оператор може збільшити параметри demo run і створити важчий синтетичний прогін, після чого подивитися capacity evidence. Але UI не є повноцінним інструментом навантажувального тестування: він не запускає серії повторів, не керує warm/cold cache режимами, не відкриває worker/queue/shard/partition профілі, не перемикає важкі обробники і не формує benchmark-протокол.
* **Історія run-ів.** Ліва панель показує persisted runs, дозволяє вибрати конкретний run і відкрити його read-model без читання сирого JSON.
* **Деталі run-а.** Для вибраного запуску реалізовані вкладки `Summary`, `Batches`, `Sources`, `Handlers`, `Diagnostics` і `Capacity`. Вони показують прогрес batch-ів, групування source-ів, handler contract, output окремого handler field, діагностичні прапорці, allocation/capacity evidence і blocking reasons.
* **Lookup результатів handler-ів.** Оператор може вказати `sourceId` і назву поля, наприклад `benchmark.events`, та отримати конкретний handler output через typed API-клієнт.
* **Runtime controls.** UI відправляє чотири керівні команди: `Stop accepting`, `Drain accepted`, `Cancel and release` та `Reject unsafe fallback`. Вони доступні тільки тоді, коли host readiness успішно завантажений, є target run і вказаний durable store path.
* **URL-відновлення стану.** Вибраний `runId` і активна вкладка зберігаються в query string, тому посилання на конкретний стан розслідування можна передати іншому члену команди.
* **Помилки як частина інтерфейсу.** Network error, blocked response, validation error і rejected control outcome не зникають у console log, а відображаються на екрані як операторські стани.

Тобто реалізована функціональність — це read-model cockpit: оператор бачить, що запускалося, в якому стані перебуває конвеєр, де перша причина блокування, які batches і sources були оброблені, що повернули обробники, і які runtime-команди можна безпечно застосувати. Live-карта радара, streaming через WebSocket/SSE і Canvas/WebGL-візуалізація високочастотного потоку ще не входять у цей UI; вони свідомо залишені за межею поточної версії.

---

## 24.2. Механіка сигналів: реактивний стан

При проєктуванні архітектури клієнта ми відмовилися від складних і важких бібліотек управління станом (як-от NgRx). Замість цього ми задіяли сучасну реактивну модель **Angular Signals**. Кожен signal зберігає конкретний фрагмент стану, а залежні елементи DOM оновлюються після його зміни. Але це реактивність усередині браузера, а не real-time підписка на сервер: сигнал оновлює DOM після того, як HTTP-відповідь уже прийшла в клієнт.

Давайте зазирнемо у серце нашого контролера [`App`](../../../../src/Presentation/OperatorUi/src/app/app.ts) у файлі [`app.ts`](../../../../src/Presentation/OperatorUi/src/app/app.ts):

```typescript
@Component({
  selector: 'app-root',
  templateUrl: './app.html',
  styleUrl: './app.css',
  encapsulation: ViewEncapsulation.None,
})
export class App implements OnInit {
  // Сигнали стану запитів до API
  protected readonly readiness = signal<ProductRequestState<ProductRunHistoryReadiness> | null>(null);
  protected readonly runs = signal<ProductRequestState<readonly ProductRunSummary[]> | null>(null);
  protected readonly latestRun = signal<ProductRequestState<ProductRunDetail> | null>(null);
  protected readonly selectedRun = signal<ProductRequestState<ProductRunDetail> | null>(null);
  protected readonly runCommand = signal<ProductRequestState<ProductRunDetail> | null>(null);
  protected readonly handlerOutput = signal<ProductRequestState<ProductHandlerOutput> | null>(null);
  protected readonly controlOutcome = signal<ProductRequestState<ProductControlSummary> | null>(null);

  // Сигнали стану завантаження (індикатори прогресу)
  protected readonly readinessLoading = signal(false);
  protected readonly runsLoading = signal(false);
  protected readonly latestLoading = signal(false);
  protected readonly selectedRunLoading = signal(false);
  protected readonly runLoading = signal(false);
  protected readonly handlerOutputLoading = signal(false);
  protected readonly controlLoading = signal(false);

  // Обчислюваний сигнал зайнятості (Global Busy State)
  protected readonly isBusy = computed(() =>
    this.readinessLoading() ||
    this.runsLoading() ||
    this.latestLoading() ||
    this.selectedRunLoading() ||
    this.runLoading() ||
    this.handlerOutputLoading() ||
    this.controlLoading(),
  );

  constructor(private readonly api: RadarPulseProductApiClient) {}

  ngOnInit(): void {
    this.applyInitialUrlState();
    this.refreshAll();
  }

  // Додаткові методи...
}
```

Використання обчислюваного сигналу `isBusy` (Computed Signal) — це простий запобіжник на фронтенді. Він об'єднує стани завантаження всіх асинхронних операцій і керує загальними діями верхньої панелі, такими як зміна product host або ручне оновлення. Для запусків, handler lookup і runtime controls є окремі loading-стани та доменні guards: валідний run id, доступний host readiness, непорожній durable store path. Це не замінює idempotency на API, але прибирає клас випадкових повторних дій у cockpit.

---

## 24.3. Пульт активного керування: Команди оператора

Кабінет оператора — це не просто пасивний монітор. Це пульт активного втручання. Оператор може ініціювати два типи запусків обробки даних:
1. **Demo Run:** Запуск синтетичного тесту з налаштовуваними параметрами (кількість джерел, батчів та подій).
2. **Archive Run:** Запуск обробки реального NEXRAD-файлу шляхом передачі шляху до файлу на диску.

Це дозволяє руками дати системі більший обсяг роботи, але не треба плутати такий прогін із формальним load test. Формальне тестування продуктивності потребує контрольованого профілю, повторюваності, окремого режиму прогріву, фіксації середовища, порівняння серій і чітких acceptance gates. Operator UI наразі дає операторський запуск і перегляд доказів після нього; benchmark-контур залишається за CLI, тестами і окремими performance-gates.

Окрім цього, у разі виникнення затримок, оператор може відправляти критичні сигнали керування (Product Controls). Якщо retained pressure, blocking reason або операторський стан показують, що run треба стабілізувати, він може спершу викликати метод [`stopAccepting()`](../../../../src/Presentation/OperatorUi/src/app/app.ts), який блокує прийом нових конвертів. Після цього [`drainAccepted()`](../../../../src/Presentation/OperatorUi/src/app/app.ts) допомагає довести вже прийняту роботу до контрольованого стану:

```typescript
private applyControl(action: 'stop' | 'drain' | 'cancel' | 'reject'): void {
  const runId = this.controlTargetRunId().trim();
  const durableStorePath = this.durableStorePath.trim();

  if (!runId || !durableStorePath) {
    return;
  }

  const request = {
    runId,
    durableStorePath,
    sourceCount: this.demoSourceCount,
    handlerSet: ProductHandlerSet.counterChecksum,
  };

  const controlCall = action === 'stop'
    ? this.api.stopAccepting(request)
    : action === 'drain'
      ? this.api.drainAccepted(request)
      : action === 'cancel'
        ? this.api.cancelOpenAndRelease(request)
        : this.api.rejectUnsafeFallback(request);

  this.controlLoading.set(true);
  controlCall.subscribe({
    next: response => {
      this.controlOutcome.set(mapProductApiResponse(response));
      this.controlLoading.set(false);
      this.refreshAll();
    },
    error: error => {
      this.controlOutcome.set(mapProductHttpError(error) as ProductRequestState<ProductControlSummary>);
      this.controlLoading.set(false);
    },
  });
}
```

Команди керування дають оператору важелі, але самі по собі вони ще не роблять інтерфейс придатним для розслідування. Якщо після натискання кнопки неможливо повернутися до того самого запуску, вкладки й діагностичного контексту, кабінет швидко перетворюється на одноразову панель. Тому наступний крок — зробити стан екрана відтворюваним.

---

## 24.4. Хроніки розслідувань та синхронізація URL

Ще однією важливою властивістю Operator UI є синхронізація ключового стану екрана з URL-адресою браузера: вибраного run-а і активної вкладки. Уявіть ситуацію: детектив знайшов критичний збій на батчі №48 у запуску `demo-20260530`. Він хоче надіслати посилання на цю проблему колезі. Якщо інтерфейс не вміє зберігати цей мінімальний контекст в адресному рядку, колега відкриє просто стартовий порожній екран і буде змушений самостійно шукати потрібний запуск.

Ми реалізували детерміновану синхронізацію через стандартний механізм `history.replaceState`. При виборі будь-якого запуску чи перемиканні вкладок діагностики (`summary`, `capacity`, `diagnostics`), адреса оновлюється без перезавантаження сторінки:

```typescript
private updateUrlState(): void {
  const url = readCurrentUrl();

  if (!url || !globalThis.history?.replaceState) {
    return;
  }

  const runId = this.selectedRunId().trim();
  if (runId) {
    url.searchParams.set('runId', runId);
  } else {
    url.searchParams.delete('runId');
  }

  const tab = this.activeTab();
  if (tab === 'summary') {
    url.searchParams.delete('tab');
  } else {
    url.searchParams.set('tab', tab);
  }

  globalThis.history.replaceState(
    globalThis.history.state,
    '',
    `${url.pathname}${url.search}${url.hash}`,
  );
}
```

Коли сторінка завантажується вперше, метод [`applyInitialUrlState()`](../../../../src/Presentation/OperatorUi/src/app/app.ts) зчитує параметри `runId` та `tab` безпосередньо з адресного рядка та автоматично відновлює кабінет детектива у тому ж вигляді, в якому він був залишений.

---

## 24.5. Поточна межа UI: операторська модель читання, а не полотно радара в реальному часі

Обробка NEXRAD-даних у реальному часі справді ставить перед фронтендом важке питання: як показувати потік, який для DOM занадто щільний? Але поточний Operator UI не робить вигляд, що він уже є live-radar cockpit. Він побудований як операторський кабінет розслідування: Angular компоненти запитують product API через `HttpClient`, показують список запусків, деталі run-а, batches, sources, handler outputs, diagnostics, capacity і readiness.

Це не поразка візуалізації, а правильна межа першого продуктового інтерфейсу. Ми спочатку дали оператору не красиву картинку без доказів, а контроль над фактом: що запускалося, з якими параметрами, де pipeline заблокувався, який fallback рекомендовано і чи готовий стенд до демонстрації.

Live-візуалізація має окрему інженерну ціну і не повинна ховатися між рядками:
* BFF мусить видати агрегований візуальний DTO (visual DTO) або модель плиток/растру (tile/raster model), а не внутрішні структури рантайму (runtime structures).
* Транспорт має бути обраний окремо: polling може вистачити для demo history, а WebSocket або SSE потрібні тільки тоді, коли є справжній live push.
* Canvas/WebGL render loop з `requestAnimationFrame` має пройти власний browser performance gate, інакше `60 FPS` буде не доказом, а прикрасою.

Тому ця книга фіксує чесний стан: реалізований UI є read-model cockpit для перевірки системи. High-frequency radar visualization лишається названим наступним напрямком, а не прихованою вигадкою в поточному описі проекту.

---

## 24.6. Утриманий тиск та перша причина блокування

Коли конвеєр обробки радарних даних стикається із перешкодою (наприклад, некоректним вхідним станом, конфліктом обробника або проблемою history storage), run може перейти в заблокований стан, де подальша дія має бути явною. У класичних інтерфейсах користувач бачить лише нескінченний індикатор завантаження і не розуміє, що сталося.

В Operator UI ми реалізували два унікальних діагностичних блоки:
1. **First Blocking Reason:** Інтерфейс витягує з BFF детальну інформацію про перший заблокований елемент. Наприклад: `"History storage folder is readonly"` або `"Handler delta provider sequence mismatch at batch 42"`. Це відразу дає детективу головну зачіпку.
2. **Retained Pressure (утриманий тиск):** Інтерфейс показує не загальний heap і не графік GC, а конкретні числа з runtime evidence: скільки batch-ів, envelopes і payload bytes залишаються утриманими в поточному або фінальному стані run-а. Нульові terminal retained values означають, що release-протокол відпрацював і система не тримає зайві конверти після завершення.

Таким чином, Operator UI стає надійним кабінетом для розслідування архітектурних аномалій, перетворюючи складну паралельну систему на зрозумілу, реактивну та прозору дошку доказів.

---

## Матеріали справи

### 1. Вердикт детективів
Побудова Angular SPA інтерфейсу оператора з інтегрованою локальною доставкою (Віхи `030`-`031`). UI надає оператору можливість контролювати рантайм, відображаючи readiness, історію запусків, read-model деталей та критичний діагностичний маркер — `First Blocking Reason` (першопричину зупинки конвеєра).

#### Чому оператору потрібен пульт, а не dump
Можна було лишити все в CLI: текстові звіти прості, швидкі й достатні для розробника. Але оператору потрібен не dump, а картина стану: що працює, що заблоковано, де першопричина. Можна було зробити server-rendered сторінку, але SPA краще тримає інтерактивний стан, історію запусків і локальні команди. Angular UI став диспетчерським столом над BFF, а не новим власником бізнес-логіки. Ціна вибору — build pipeline і frontend-тести; виграш — система стає зрозумілою не лише автору коду.

### 2. Закони фізики рантайму
* **Доставка з того самого джерела (Same-Origin Delivery)**: Збірка Angular SPA інтегрується прямо в ресурси [`RadarPulse.Http`](../../../../src/Presentation/RadarPulse.Http/RadarPulse.Http.csproj) та віддається з тієї ж адреси, усуваючи потребу в CORS конфігураціях.
* **Стан, відновлюваний з URL (URL-Restorable State)**: Вибраний `runId` і активна вкладка мають відновлюватися при перезавантаженні сторінки з URL.

### 3. Патологоанатомічний звіт
* **Втрата зв'язку з API**: При відключенні бекенду інтерфейс переходить у режим очікування з візуальним блокуванням дій оператора, запобігаючи надсиланню некоректних команд керування.

### 4. Слід доказової бази
* Код Angular SPA: [src/Presentation/OperatorUi/](../../../../src/Presentation/OperatorUi)
* Typed API-клієнт Operator UI: [product-api.client.ts](../../../../src/Presentation/OperatorUi/src/app/product/product-api.client.ts)
* Browser smoke-тести Operator UI: [operator-ui.smoke.spec.ts](../../../../src/Presentation/OperatorUi/smoke/operator-ui.smoke.spec.ts)
* Тести доставки HTTP-контролера: [RadarPulseProductHttpControlTests.cs](../../../../tests/RadarPulse.Tests/Product/Http/RadarPulseProductHttpControlTests.cs)

### 5. Протокол допиту процесу
Запуск тестів HTTP-хостингу та доставки API:
```bash
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~ProductHttp"
```

Запуск unit і browser smoke-тестів Operator UI:
```bash
cd src/Presentation/OperatorUi
npm test -- --watch=false
npm run smoke
```

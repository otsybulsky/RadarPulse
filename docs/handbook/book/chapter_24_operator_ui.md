# Розділ 24: Кабінет оператора (Angular SPA)

У кабінеті досвідченого детектива завжди є дошка стану. На ній видно, які справи відкриті, де бракує доказів, який протокол заблоковано і хто чекає рішення. Слідчий не повинен щоразу спускатися в архів або читати сирий JSON, щоб зрозуміти, чи готова експертиза. Йому потрібне єдине табло, яке показує стан системи без прикрас.

У віхах `030` та `031` ми створили таке табло для нашої системи — **Operator UI**. Це Single Page Application (SPA), побудоване на базі Angular, яке хоститься безпосередньо з нашого Minimal API. Воно надає оператору можливість візуалізувати стан рантайму, керувати запусками обробки радарних даних NEXRAD та оперативно реагувати на критичні збої.

---

## 24.1. Механіка сигналів: Реактивні агенти

При проєктуванні архітектури клієнта ми відмовилися від складних і важких бібліотек управління станом (як-от NgRx). Замість цього ми задіяли сучасну реактивну модель **Angular Signals**. Вони працюють як чутливі датчики: щойно змінюється значення в одному місці, всі залежні елементи інтерфейсу оновлюються автоматично.

Давайте зазирнемо у серце нашого контролера [`App`](../../../src/Presentation/OperatorUi/src/app/app.ts) у файлі [`app.ts`](../../../src/Presentation/OperatorUi/src/app/app.ts):

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

Використання обчислюваного сигналу `isBusy` (Computed Signal) — це простий запобіжник на фронтенді. Він об'єднує стани завантаження всіх асинхронних операцій. Якщо хоча б один фоновий процес (наприклад, зчитування історії або запуск симуляції) активний, `isBusy` приймає значення `true`, а кнопки запуску та керування стають недоступними. Це не замінює idempotency на API, але прибирає клас випадкових подвійних кліків у cockpit.

---

## 24.2. Пульт активного керування: Команди оператора

Кабінет оператора — це не просто пасивний монітор. Це пульт активного втручання. Оператор може ініціювати два типи запусків обробки даних:
1. **Demo Run:** Запуск синтетичного тесту з налаштовуваними параметрами (кількість джерел, батчів та подій).
2. **Archive Run:** Запуск обробки реального NEXRAD-файлу шляхом передачі абсолютного шляху на диску.

Окрім цього, у разі виникнення затримок, оператор може відправляти критичні сигнали керування (Product Controls). Наприклад, якщо пам'ять переповнена, він викликає метод [`stopAccepting()`](../../../src/Presentation/OperatorUi/src/app/app.ts), який блокує прийом нових конвертів, або [`drainAccepted()`](../../../src/Presentation/OperatorUi/src/app/app.ts), щоб очистити чергу:

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

---

## 24.3. Хроніки розслідувань та синхронізація URL

Ще однією важливою фішкою Operator UI є повна синхронізація стану екрана з URL-адресою браузера. Уявіть ситуацію: детектив знайшов критичний збій на батчі №48 у запуску `demo-20260530`. Він хоче надіслати посилання на цю проблему колезі. Якщо інтерфейс не вміє зберігати свій стан в адресному рядку, колега відкриє просто стартовий порожній екран і буде змушений самостійно шукати потрібний запуск.

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

Коли сторінка завантажується вперше, метод [`applyInitialUrlState()`](../../../src/Presentation/OperatorUi/src/app/app.ts) зчитує параметри `runId` та `tab` безпосередньо з адресного рядка та автоматично відновлює кабінет детектива у тому ж вигляді, в якому він був залишений.

---

## 24.4. Поточна межа UI: операторський read-model, а не live-radar canvas

Обробка NEXRAD-даних у реальному часі справді ставить перед фронтендом важке питання: як показувати потік, який для DOM занадто щільний? Але поточний Operator UI не робить вигляд, що він уже є live-radar cockpit. Він побудований як операторський кабінет розслідування: Angular компоненти запитують product API через `HttpClient`, показують список запусків, деталі run-а, batches, sources, handler outputs, diagnostics, capacity і readiness.

Це не поразка візуалізації, а правильна межа першого продуктового інтерфейсу. Ми спочатку дали оператору не красиву картинку без доказів, а контроль над фактом: що запускалося, з якими параметрами, де pipeline заблокувався, який fallback рекомендовано і чи готовий стенд до демонстрації.

Live-візуалізація має окрему інженерну ціну і не повинна ховатися між рядками:
* BFF мусить видати агрегований візуальний DTO (visual DTO) або модель плиток/растру (tile/raster model), а не внутрішні структури рантайму (runtime structures).
* Транспорт має бути обраний окремо: polling може вистачити для demo history, а WebSocket або SSE потрібні тільки тоді, коли є справжній live push.
* Canvas/WebGL render loop з `requestAnimationFrame` має пройти власний browser performance gate, інакше `60 FPS` буде не доказом, а прикрасою.

Тому ця книга фіксує чесний стан: реалізований UI є read-model cockpit для перевірки системи. High-frequency radar visualization лишається названим наступним напрямком, а не прихованою вигадкою в поточному описі проекту.

---

## 24.5. Retained Pressure та First Blocking Reason

Коли конвеєр обробки радарних даних стикається із перешкодою (наприклад, пошкодженим пакетом), робота воркерів зупиняється. У класичних інтерфейсах користувач бачить лише нескінченний Spinner і не розуміє, що сталося.

В Operator UI ми реалізували два унікальних діагностичних блоки:
1. **First Blocking Reason:** Інтерфейс витягує з BFF детальну інформацію про перший заблокований елемент. Наприклад: `"History storage folder is readonly"` або `"Handler delta provider sequence mismatch at batch 42"`. Це відразу дає детективу головну зачіпку.
2. **Retained Pressure (Тиск пам'яті):** Графічний індикатор, який відображає кількість виділеної пам'яті в пулах обробника та обсяг черги воркерів. Якщо тиск наближається до критичної позначки, оператор бачить жовту або червону шкалу і може вчасно застосувати екстрене гальмування.

Таким чином, Operator UI стає надійним кабінетом для розслідування архітектурних аномалій, перетворюючи складну паралельну систему на зрозумілу, реактивну та прозору дошку доказів.
---

## 🔍 Матеріали справи (Investigation Case Files)

### 1. Вердикт детективів (Decision Trace & Rationale)
Побудова Angular SPA інтерфейсу оператора з інтегрованою локальною доставкою (Віхи `030`-`031`). UI надає оператору можливість контролювати рантайм, відображаючи статус черг, історію запусків та критичний діагностичний маркер — `First Blocking Reason` (першопричину зупинки конвеєра).

#### Чому оператору потрібен пульт, а не dump
Можна було лишити все в CLI: текстові звіти прості, швидкі й достатні для розробника. Але оператору потрібен не dump, а картина стану: що працює, що заблоковано, де першопричина. Можна було зробити server-rendered сторінку, але SPA краще тримає інтерактивний стан, історію запусків і локальні команди. Angular UI став диспетчерським столом над BFF, а не новим власником бізнес-логіки. Ціна вибору — build pipeline і frontend-тести; виграш — система стає зрозумілою не лише автору коду.

### 2. Закони фізики рантайму (System Invariants)
* **Доставка з того самого джерела (Same-Origin Delivery)**: Збірка Angular SPA інтегрується прямо в ресурси [`RadarPulse.Http`](../../../src/Presentation/RadarPulse.Http/RadarPulse.Http.csproj) та віддається з тієї ж адреси, усуваючи потребу в CORS конфігураціях.
* **Стан, відновлюваний з URL (URL-Restorable State)**: Всі важливі параметри фільтрації та обраного запуску мають відновлюватися при перезавантаженні сторінки з URL.

### 3. Патологоанатомічний звіт (Failure Modes & Recovery)
* **Втрата зв'язку з API**: При відключенні бекенду інтерфейс переходить у режим очікування з візуальним блокуванням дій оператора, запобігаючи надсиланню некоректних команд керування.

### 4. Слід доказової бази (Implementation & Tests)
* Код Angular SPA: [src/Presentation/OperatorUi/](../../../src/Presentation/OperatorUi)
* Тести доставки HTTP-контролера: [RadarPulseProductHttpControlTests.cs](../../../tests/RadarPulse.Tests/Product/Http/RadarPulseProductHttpControlTests.cs)

### 5. Протокол допиту процесу (Verification Commands)
Запуск тестів HTTP-хостингу та доставки API:
```bash
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~ProductHttp"
```

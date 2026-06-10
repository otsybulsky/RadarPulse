# Структурний план книги «Документація як інженерний детектив»

Цей документ фіксує узгоджену структуру частин та розділів книги, які детально відображають життєвий шлях, архітектуру, кризи продуктивності, бенчмарки, observability-рішення та технологічні рішення системи **RadarPulse** (від Milestone 001 до 037).

---

## Передмова. Executive Engineering Verdict
*   **Мета:** Дати зайнятому CTO/Principal Engineer короткий маршрут оцінки книги як hiring artifact.
*   **Технічне підґрунтя:** [preface_executive_verdict.md](book/preface_executive_verdict.md), [appendix_b_claim_evidence_matrix.md](book/appendix_b_claim_evidence_matrix.md), [appendix_c_production_hardening.md](book/appendix_c_production_hardening.md), [appendix_d_reviewer_attack_pack.md](book/appendix_d_reviewer_attack_pack.md), [appendix_e_simulated_hostile_reviewer_transcript.md](book/appendix_e_simulated_hostile_reviewer_transcript.md).
*   **Сюжет:** Книга не просить довіри, а дає карту доказів: що доведено, де межі claims, які production gaps лишаються і як рецензент може атакувати найсильніші твердження в режимі hostile review.

---

## Частина І. Запуск турбіни (Орбіта та вихідні дані)
*   **Мета:** Окреслити масштаб системи, дослідити структуру бінарних даних NEXRAD та принципи їх зчитування.
*   **Бенчмарк-фокус:** Визначення базової продуктивності зчитування та парсингу файлів архіву.
*   **Розділи:**
    *   **Розділ 1: Двигун на верстаку (Концепція «Лабораторного столу»)**
        *   *Технічне підґрунтя:* [system-overview.md](system-overview.md), [README.md](../../README.md).
        *   *Сюжет:* Чому локальний макет 1:10 кращий за розгортання хмарного монстра на старті розробки. Зворотний бік хмарного оверінжинірингу.
    *   **Розділ 2: Бінарні лабіринти NEXRAD Level II**
        *   *Технічне підґрунтя:* Віхи `001` (Historical Loader) та `002` (Nexrad Archive Inspection).
        *   *Сюжет:* Особливості бінарного метеорологічного формату. Чому радарний файл — це послідовність стиснутих блоків із 24-байтним заголовком, і чому стандартна декомпресія «в лоб» вбиває продуктивність.
    *   **Розділ 3: Контракт радарного батча**
        *   *Технічне підґрунтя:* Віхи `003` (Historical Replay Publisher) та `004` (Processing Core Input Contract).
        *   *Сюжет:* Еволюція від сирого потоку подій до структурованого `RadarEventBatch`. Як заміна об'єктів з текстовими метаданими на 64-байтні некеровані (unmanaged) структури `RadarStreamEvent` дала Milestone 004 benchmark-рівень 500M+ payload-значень/сек і близько 0.20 allocated bytes/payload value на cache-wide replay.

## Частина ІІ. Обитель Чистоти (Clean Architecture та Доменні Контракти)
*   **Мета:** Розкрити принципи ізоляції бізнес-правил та автоматичного контролю кордонів проекту.
*   **Бенчмарк-фокус:** Доведення нульових витрат на інфраструктурні обгортки всередині доменного шару.
*   **Розділи:**
    *   **Розділ 4: Монастир Домену**
        *   *Технічне підґрунтя:* [architecture.md](architecture.md).
        *   *Сюжет:* Чому Domain — це «монастир», куди закритий доступ базам даних чи HTTP-контролерам. Правило одностороннього потоку залежностей Clean Architecture.
    *   **Розділ 5: Вартові монастиря (Аналіз тестів архітектури)**
        *   *Технічне підґрунтя:* [RadarPulseArchitectureTests.cs](../../tests/RadarPulse.Tests/Architecture/RadarPulseArchitectureTests.cs) (Віха `036`).
        *   *Сюжет:* Як написати тести, що автоматично перевіряють посилання `.csproj` та забороняють використання `InternalsVisibleTo` для захисту доступу до внутрішніх класів.
    *   **Розділ 6: Залізні контракти домену**
        *   *Технічне підґрунтя:* Віха `035` (Code Contract Documentation Pass).
        *   *Сюжет:* Проектування інтерфейсів доменних сервісів та валідаторів. Створення непорушних контрактів для обробки сутностей.

## Частина ІІІ. Динамічний баланс (Ребалансування та Шардування)
*   **Мета:** Пояснити динамічне load-leveling балансування під час штормового навантаження.
*   **Бенчмарк-фокус:** Порівняння статичного розподілу навантаження та динамічного ребалансування (зменшення часу обробки на гарячих шардах).
*   **Розділи:**
    *   **Розділ 7: Гроза над шардами**
        *   *Технічне підґрунтя:* Віха `005` (Processing Core Architecture).
        *   *Сюжет:* Чому статичний розподіл джерел по шардах падає під час реального шторму (intrinsically hot partitions). Проблема перевантаження одного потоку.
    *   **Розділ 8: Велика міграція топології**
        *   *Технічне підґрунтя:* Віха `006` (Partition-Level Shard Rebalance).
        *   *Сюжет:* Міграція обробника з шарда на шард «на льоту». Чому перенесення можливе лише на межах батчів (batch boundaries) та як працює безпечне версіонування топологій.
    *   **Розділ 9: Математика анти-серфінгу (Cooldown & Hysteresis)**
        *   *Технічне підґрунтя:* Віха `007` (Rebalance Production Hardening).
        *   *Сюжет:* Боротьба з панічним перекиданням джерел туди-сюди при випадкових сплесках шуму. Реалізація вікон згладжування, лімітів міграційного бюджету та часу охолодження (cooldown).
    *   **Розділ 10: Поштові скриньки воркерів (Async Shard Transport)**
        *   *Технічне підґрунтя:* Віха `008` (Retained Async Shard Transport).
        *   *Сюжет:* Створення ізольованих воркерів з обмеженими поштовими скриньками (Bounded Worker Mailboxes). Керування місткості черг воркерів.

## Частина IV. Війна з Garbage Collector (Керування пам'яттю та алокаціями)
*   **Мета:** Хроніка боротьби з алокаційними кризами та оптимізації GC-навантаження.
*   **Бенчмарк-фокус:** Аналіз гейтів Milestone 010/017. Порівняння Snapshot-Copy (9.95 GB алокацій) та Pooled-Copy (102 MB алокацій).
*   **Розділи:**
    *   **Розділ 11: Аномалія 9.95 ГБ (Криза асинхронної черги)**
        *   *Технічне підґрунтя:* `009-owned-payload-provider-decoupling-performance-gate.md`, `010-owned-provider-overlap-cost-reduction-performance-gate.md`.
        *   *Сюжет:* Як спроба повністю відв'язати зчитування файлів від процесингу за допомогою копіювання зліпків (`ToOwnedSnapshot`) мало не спалила Garbage Collector, згенерувавши 9,947,507,832 байт сміття на повному кеші з 198 файлів.
    *   **Розділ 12: Скарбниця буферів (Pooled-Copy)**
        *   *Технічне підґрунтя:* Віхи `010`, `011`, `012` (Owned Provider Overlap & Cost Reduction).
        *   *Сюжет:* Реалізація стратегії `pooled-copy retained payload`. Перехід на оренду байтових масивів із пулів та зниження GC-навантаження на 98.97% (зменшення виділення пам'яті до 102,811,264 байт).
    *   **Розділ 13: Пастка холодного старту та Prewarm**
        *   *Технічне підґрунтя:* Віхи `015` (Queued Owned Allocation Readiness) та `017` (Cold Retained Ownership Cost).
        *   *Сюжет:* Чому перший файл виділяв 138 МБ пам'яті через промахи пулу (pool misses). Впровадження концепту примусового прогріву пулів (`Prewarm Posture`) при старті, що знизило стартову алокацію до контрольованих 68 МБ.

## Частина V. Магія Паралельного Рантайму (Concurrency & Ordered Commit)
*   **Мета:** Розібрати логіку паралельних обчислень із детермінованим виведенням результатів.
*   **Бенчмарк-фокус:** Метрики безпечної паралельності: active-batch runtime, ordered commit tax, stale recompute cost та вплив на алокацію пам'яті.
*   **Розділи:**
    *   **Розділ 14: Хаос на іподромі (Паралельність воркерів)**
        *   *Технічне підґрунтя:* [processing-runtime.md](processing-runtime.md).
        *   *Сюжет:* Чому воркери завершують обробку абсолютно врозкид і як це загрожує хронологічній цілісності радарної карти.
    *   **Розділ 15: Старшина черги (`OrderedResultCoordinator`)**
        *   *Технічне підґрунтя:* [RadarProcessingOrderedResultCoordinator.cs](../../src/Infrastructure/Processing/Async/Services/RadarProcessingOrderedResultCoordinator.cs) (Віха `021`).
        *   *Сюжет:* Покроковий розбір алгоритму дренування тимчасового сейфу та випуску результатів строго за маркером `Provider Sequence`.
    *   **Розділ 16: Справа про мутабельне ядро (Блокер Slice 3)**
        *   *Технічне підґрунтя:* `021-ordered-concurrent-runtime-archive-processing-slice-3-blocker.md`.
        *   *Сюжет:* Архітектурна помилка, коли паралельні воркери перезаписували спільний мутабельний стан ядра. Розділення рантайму на дві ізольовані фази: розрахунок немутабельних `RadarProcessingBatchDelta` та строго послідовний Ordered Commit. Аналіз матриці продуктивності (active=4 vs active=1).
    *   **Розділ 17: Свіжі карти на ходу (Stale Topology Recompute)**
        *   *Технічне підґрунтя:* Віха `022` (Ordered Rebalance Topology Commit).
        *   *Сюжет:* Що робити, якщо під час паралельної обробки батча змінилася топологія. Реалізація скидання та перерахунку дельт на льоту перед коммітом. Аналіз ціни перерахунку: зростання кількості запусків воркерів з 32,000 до 39,292 та збільшення алокації на 1.137x.

## Частина VI. Сейф у вогні (Стійкість та Локальна Персистентність)
*   **Мета:** Дослідити механізми виживання системи при аваріях без зовнішніх баз даних та брокерів.
*   **Бенчмарк-фокус:** Метрики ідемпотентності та надійності при падінні під навантаженням.
*   **Розділи:**
    *   **Розділ 18: Дипломатичний конверт (`DurableEnvelope`)**
        *   *Технічне підґрунтя:* Віха `023` (Durable Cross-Process Runtime Readiness).
        *   *Сюжет:* Розробка незалежного скінченного автомата повідомлення (Pending, Claimed, Completed, Committed, Failed, Poison). Обробка повторних спроб (`RetryPolicy`) та утилізація отруйних пакетів.
    *   **Розділ 19: Слідство у файловій системі (`FileDurableEnvelopeStore`)**
        *   *Технічне підґрунтя:* Віха `026` (Persistent Durable Adapter Readiness).
        *   *Сюжет:* Розбір реалізації локального адаптера збереження стану черги в файлах JSON. Як забезпечити виживання черги після повного краху процесу.
    *   **Розділ 20: Принцип Fail-Closed**
        *   *Технічне підґрунтя:* `023-durable-cross-process-runtime-readiness-decision-trace.md`.
        *   *Сюжет:* Чому при виявленні першого зламаного пакету черга блокується, але воркери коректно допрацьовують поточні завдання та вивільняють ресурси пам'яті.

## Частина VII. Експерти в лабораторії (Аналітичні Обробники)
*   **Мета:** Описати розширення логіки системи за допомогою плагінів-обробників.
*   **Бенчмарк-фокус:** Накладні витрати на підключення користувацьких обробників (Sequential vs Mergeable Fast-path).
*   **Розділи:**
    *   **Розділ 21: Спеціальні аналітики (Custom Handlers)**
        *   *Технічне підґрунтя:* Віха `024` (Custom Handler Output Contract).
        *   *Сюжет:* Як підключити додаткові аналітичні модулі до рантайму обробки подій, не захаращуючи ядро системи.
    *   **Розділ 22: Контракт злиття дельт (`IMergeableHandler`)**
        *   *Технічне підґрунтя:* Віха `025` (Handler Delta Merge Contract).
        *   *Сюжет:* Як дозволити аналітикам обчислювати свої дельти паралельно й зливати їх за чергою провайдера, замість використання повільного sequential fallback.

## Частина VIII. Очі Диспетчера (Інтерфейс, BFF та Демо-пакет)
*   **Мета:** Описати проектування користувацького інтерфейсу та фінальну збірку проекту.
*   **Бенчмарк-фокус:** Наскрізні випробування інтегрованої системи (Angular + Hosted BFF).
*   **Розділи:**
    *   **Розділ 23: Щит для фронтенду (Backend-for-Frontend)**
        *   *Технічне підґрунтя:* Віха `028` (Product Facing Pipeline Console and API).
        *   *Сюжет:* Створення DTO-моделей представлення та BFF-контролерів, які захищають складну внутрішню кухню рантайму від клієнтських запитів.
    *   **Розділ 24: Кабінет оператора (Angular SPA)**
        *   *Технічне підґрунтя:* Віхи `030` (Product Operator Angular SPA) та `031` (Operator UI Hardening).
        *   *Сюжет:* Розробка інтерфейсу оператора. Як відобразити складні метрики (наприклад, `First Blocking Reason` або `Retained Pressure`) у простому та зрозумілому вигляді.
    *   **Розділ 25: Демо-пакет під ключ**
        *   *Технічне підґрунтя:* Віхи `032` (Product Demo Readiness Packaging) та `033` (Product Demo Polish).
        *   *Сюжет:* Фінальна стадія. Створення єдиного пакетного скрипта керування та тестування системи в freeze mode.

## Частина IX. Чорний ящик (Diagnostics, Logging та Observability)
*   **Мета:** Пояснити, як система говорить про себе під час нормальної роботи, деградації та блокування.
*   **Бенчмарк-фокус:** Не throughput, а доказовість: first blocking reason, readiness, queue telemetry, retained pressure і capacity evidence без production-logging overclaim.
*   **Розділи:**
    *   **Розділ 26: Чорний ящик RadarPulse**
        *   *Технічне підґрунтя:* [chapter_26_observability_logging.md](book/chapter_26_observability_logging.md), [RadarProcessingRunDiagnosticsReadModel.cs](../../src/Application/Processing/ReadModels/RadarProcessingRunDiagnosticsReadModel.cs), [RadarProcessingProviderQueueTelemetrySummary.cs](../../src/Domain/Processing/Queueing/Telemetry/RadarProcessingProviderQueueTelemetrySummary.cs), [RadarProcessingProductionPipelineOperatorSummary.Blocking.cs](../../src/Infrastructure/Processing/ProductPipeline/Models/RadarProcessingProductionPipelineOperatorSummary/RadarProcessingProductionPipelineOperatorSummary.Blocking.cs).
        *   *Сюжет:* Чому `Console.WriteLine` і “логи всюди” не є observability. Як RadarPulse уже має typed diagnostic/readiness contract, але чесно не заявляє готовий `ILogger`/OpenTelemetry production stack.

## Додатки. Лабораторні докази
*   **Додаток А: Апаратне профілювання системи у лабораторії**
    *   *Технічне підґрунтя:* [appendix_a_profiling.md](book/appendix_a_profiling.md), [Milestone 004 closeout](../milestones/004-processing-core-input-contract-closeout.md), [RadarPulse.Cli.csproj](../../src/Presentation/RadarPulse.Cli/RadarPulse.Cli.csproj).
    *   *Сюжет:* Як підтверджувати throughput, allocation rate і форму живої купи за допомогою `dotnet-trace`, `dotnet-counters`, `dotnet-gcdump` та benchmark-команд, не підміняючи performance-докази звичайними unit-тестами.
*   **Додаток Б: Матриця тверджень і доказів**
    *   *Технічне підґрунтя:* [appendix_b_claim_evidence_matrix.md](book/appendix_b_claim_evidence_matrix.md), milestone performance gates, source files і verification commands.
    *   *Сюжет:* Стисла таблиця `Claim -> Code -> Test -> Measurement -> Scope`, яка дозволяє рецензенту швидко відрізнити доведені твердження від навмисних non-claims.
*   **Додаток В: Production Hardening Plan**
    *   *Технічне підґрунтя:* [appendix_c_production_hardening.md](book/appendix_c_production_hardening.md), [system-overview.md](system-overview.md), [product-surface.md](product-surface.md), [processing-runtime.md](processing-runtime.md).
    *   *Сюжет:* Як переносити lab-table інваріанти в production: broker/database adapters, observability, public API security, live ingestion і multi-node processing без розширення поточних claims.
*   **Додаток Г: Reviewer Attack Pack**
    *   *Технічне підґрунтя:* [appendix_d_reviewer_attack_pack.md](book/appendix_d_reviewer_attack_pack.md), [appendix_b_claim_evidence_matrix.md](book/appendix_b_claim_evidence_matrix.md), source/test/milestone links.
    *   *Сюжет:* Набір найсильніших питань, якими principal-level reviewer може атакувати книгу, і маршрути до відповідей у коді, тестах та milestone evidence.
*   **Додаток Д: Simulated Hostile Reviewer Transcript**
    *   *Технічне підґрунтя:* [appendix_e_simulated_hostile_reviewer_transcript.md](book/appendix_e_simulated_hostile_reviewer_transcript.md), [appendix_d_reviewer_attack_pack.md](book/appendix_d_reviewer_attack_pack.md), [appendix_b_claim_evidence_matrix.md](book/appendix_b_claim_evidence_matrix.md).
    *   *Сюжет:* Детальна simulated-стенограма principal-level hostile review: атака, відповідь автора, follow-up, verdict і наступний proof для production claim.

# Розділ 21: Спеціальні аналітики та користувацькі обробники

Точка розширення (extension point) у продуктивному ядрі завжди небезпечна. Все починається невинно: ще одна формула, ще один сповіщувач (notifier), ще один запис у базу прямо всередині парсера або процесора. Через кілька ітерацій ядро вже не обробляє радарні події, а тягне чужі побічні ефекти.

У RadarPulse шар архіву і стрімінгу (archive/streaming) відповідає за демаршалізацію NEXRAD-архівів, а [`RadarProcessingCore`](../../../src/Domain/Processing/Core/Services/RadarProcessingCore/RadarProcessingCore.cs) має залишатися чистим, швидким і сфокусованим на базовій роботі з уже підготовленими батчами: застосуванні радарних подій до стану джерел та просуванні часової послідовності [`Provider Sequence`](../../../src/Domain/Processing/Queueing/Models/RadarProcessingQueuedBatchSequence.cs). Але споживачам результатів — кліматичним аналітикам, операторам, дослідникам — потрібні власні розрахунки поверх цих даних. Архітектурне питання звучить так: як впустити експертів у лабораторію, не віддавши їм ключі від ядра?

Демаршалізація тут — це не .NET `Marshal` і не просто десеріалізація JSON. Йдеться про переклад зовнішнього бінарного формату NEXRAD / Archive II у внутрішні структури RadarPulse. Archive-шар читає повідомлення та блоки моментів, [`ArchiveTwoRadarEventBatchProjector`](../../../src/Infrastructure/Archive/Archive2/Projectors/ArchiveTwoRadarEventBatchProjector/ArchiveTwoRadarEventBatchProjector.cs) витягує метадані радарної події, нормалізує ідентичність radar/moment/source і через [`RadarEventBatchBuilder.AddEvent()`](../../../src/Domain/Streaming/Batches/Services/RadarEventBatchBuilder/RadarEventBatchBuilder.AddEvent.cs) складає компактний [`RadarEventBatch`](../../../src/Domain/Streaming/Batches/Models/RadarEventBatch.cs).

У цьому батчі окремо лежать метадані подій [`RadarStreamEvent`](../../../src/Domain/Streaming/Streams/Models/RadarStreamEvent.cs), а окремо — байти корисного навантаження (payload bytes). Після цієї межі ядро вже не працює з сирими байтовими зміщеннями Archive II; воно отримує впорядковані події, payload metrics і sequence-докази.

Для розв'язання цього конфлікту ми розробили концепцію **користувацькі обробники (Custom Handlers)** (Віха `024`). Це незалежні аналітичні експерти, які підключаються до рантайму через чітко визначені порти (Ports) і працюють виключно з копіями доказів, не маючи права мутувати внутрішній стан самого ядра.

---

## 21.1. Детективне завдання: Свідки проти експертів

У класичних монолітних системах розробники часто припускаються помилки «все-в-одному». Вони беруть обробник радарної точки та безпосередньо в його тіло вбудовують підрахунок середньої температури, виявлення торнадо, надсилання повідомлень у Telegram та запис у базу даних. Як наслідок, будь-яка зміна у формулі розрахунку грозового фронту ламає базовий парсер бінарних даних.

Ми пішли шляхом чистої архітектури Ports and Adapters. Наше ядро обробки радарних даних декларує інтерфейс-порт [`IRadarSourceProcessingHandler`](../../../src/Domain/Processing/Handlers/Contracts/IRadarSourceProcessingHandler.cs). Будь-який аналітичний модуль — це зовнішній експерт, який погоджується грати за правилами ядра:

```csharp
namespace RadarPulse.Domain.Processing;

/// <summary>
/// Extension point for per-source processing logic executed while radar events are applied.
/// </summary>
public interface IRadarSourceProcessingHandler
{
    /// <summary>
    /// Declares the handler name, slot counts, and exported snapshot fields.
    /// </summary>
    RadarSourceProcessingHandlerDescriptor Descriptor { get; }

    /// <summary>
    /// Applies one stream event and payload to the source-local handler state.
    /// </summary>
    void Process(
        in RadarSourceProcessingHandlerContext context,
        RadarSourceProcessingState state);
}
```

Зверніть увагу на параметри методу `Process`. Контекст події ([`RadarSourceProcessingHandlerContext`](../../../src/Domain/Processing/Handlers/Models/RadarSourceProcessingHandlerContext.cs)) передається за модифікатором `in` (як read-only посилання), а стан обробника ([`RadarSourceProcessingState`](../../../src/Domain/Processing/Handlers/Models/RadarSourceProcessingState.cs)) передається як структура. Вони спроектовані як `ref struct`, тобто компілятор не дозволяє винести ці контексти в heap, захопити їх у lambda або зберегти після виклику. Це наш перший архітектурний запобіжник: аналітик отримує докази для аналізу, заповнює відведені slots і не може забрати вказівник на внутрішню пам'ять із собою.

---

## 21.2. Анатомія доменного сейфу: [`RadarSourceProcessingState`](../../../src/Domain/Processing/Handlers/Models/RadarSourceProcessingState.cs)

Для того щоб експерти не заважали один одному і не створювали конкуренції за пам'ять (Race Conditions), ядро виділяє кожному зареєстрованому обробнику його власний ізольований робочий простір. Замість того, щоб дозволити аналітикам виділяти довільні об'єкти в купі (Heap) та створювати навантаження на збирач сміття (Garbage Collector, GC), ми розробили модель **State Slots**.

Кожен обробник описує свої потреби в пам'яті через [`RadarSourceProcessingHandlerDescriptor`](../../../src/Domain/Processing/Handlers/Models/RadarSourceProcessingHandlerDescriptor.cs). Він заздалегідь декларує, скільки цілочисельних комірок (`Int64`) та комірок з плаваючою крапкою (`Double`) йому потрібно для збереження стану по кожному джерелу радарних даних (`Source`).

Ядро системи виділяє один безперервний масив пам'яті для всіх обробників разом, а в момент виклику конкретного аналітика надає йому індивідуальну проекцію у вигляді [`RadarSourceProcessingState`](../../../src/Domain/Processing/Handlers/Models/RadarSourceProcessingState.cs):

```csharp
public ref struct RadarSourceProcessingState
{
    private readonly Span<long> int64Slots;
    private readonly Span<double> doubleSlots;

    public RadarSourceProcessingState(
        Span<long> int64Slots,
        Span<double> doubleSlots)
    {
        this.int64Slots = int64Slots;
        this.doubleSlots = doubleSlots;
    }

    public void AddInt64(int slotIndex, long value)
    {
        EnsureInt64Slot(slotIndex);
        int64Slots[slotIndex] = checked(int64Slots[slotIndex] + value);
    }

    public void AddDouble(int slotIndex, double value)
    {
        EnsureDoubleSlot(slotIndex);
        doubleSlots[slotIndex] += value;
    }

    // Інші методи читання та запису...
}
```

Цей дизайн свідомо низькорівневий. Завдяки використанню `Span<T>` та `ref struct`, обробник оперує виділеним йому зрізом пам'яті без купи тимчасових об'єктів. Арифметичні операції виконуються із ключовим словом `checked`, тому переповнення даних (Overflow Detection) не маскується під “дивну метрику”. Якщо аналітик спробує вийти за межі відведених йому комірок, система викине `ArgumentOutOfRangeException`, локалізуючи помилку та запобігаючи пошкодженню сусідніх даних.

---

## 21.3. Практичний випадок: Аналітик-криміналіст [`CounterChecksum`](../../../src/Infrastructure/Processing/Benchmarks/Models/RadarProcessingBenchmarkHandlers/RadarProcessingBenchmarkHandlers.CounterChecksum.cs)

Давайте подивимося на реального аналітика, що використовується в наших тестах продуктивності та інтеграційних сценаріях — [`CounterChecksumBenchmarkHandler`](../../../src/Infrastructure/Processing/Benchmarks/Models/RadarProcessingBenchmarkHandlers/RadarProcessingBenchmarkHandlers.CounterChecksum.cs). Його завдання — фіксувати кількість оброблених подій, обсяг метаданих та контрольну суму корисного навантаження (Payload) для перевірки цілісності передачі.

```csharp
private sealed class CounterChecksumBenchmarkHandler :
    IRadarSourceProcessingHandler,
    IRadarSourceProcessingHandlerExecutionMetadata,
    IRadarProcessingHandlerDeltaMerger,
    IRadarProcessingHandlerDeltaAccumulatorFactory
{
    public RadarSourceProcessingHandlerDescriptor Descriptor { get; } =
        new(
            "benchmark.counter_checksum",
            int64SlotCount: 3,
            doubleSlotCount: 0,
            new[]
            {
                new RadarSourceProcessingSnapshotFieldDescriptor(
                    "benchmark.events",
                    RadarSourceProcessingSnapshotFieldType.Int64,
                    slotIndex: 0),
                new RadarSourceProcessingSnapshotFieldDescriptor(
                    "benchmark.payload_values",
                    RadarSourceProcessingSnapshotFieldType.Int64,
                    slotIndex: 1),
                new RadarSourceProcessingSnapshotFieldDescriptor(
                    "benchmark.raw_checksum",
                    RadarSourceProcessingSnapshotFieldType.Int64,
                    slotIndex: 2)
            });

    public RadarSourceProcessingHandlerExecutionClassification ExecutionClassification =>
        RadarSourceProcessingHandlerExecutionClassification.Mergeable;

    public string HandlerName => "benchmark.counter_checksum";
    public string HandlerContractVersion => "v1";

    public void Process(
        in RadarSourceProcessingHandlerContext context,
        RadarSourceProcessingState state)
    {
        // Інкрементуємо лічильник подій у першому слоті
        state.AddInt64(slotIndex: 0, value: 1);

        // Додаємо кількість значень у корисній події
        state.AddInt64(slotIndex: 1, context.PayloadMetrics.PayloadValueCount);

        // Накопичуємо контрольну суму
        state.AddInt64(slotIndex: 2, context.PayloadMetrics.RawValueChecksum);
    }

    // Реалізація інтерфейсів злиття буде детально розібрана в Розділі 22...
}
```

Цей обробник чітко розділяє свою роботу на дві складові. З одного боку, він є стандартним [`IRadarSourceProcessingHandler`](../../../src/Domain/Processing/Handlers/Contracts/IRadarSourceProcessingHandler.cs), що обробляє події через інтерфейс `Process`. З іншого боку, він декларує свою готовність до паралельної обробки через [`IRadarSourceProcessingHandlerExecutionMetadata`](../../../src/Domain/Processing/Handlers/Contracts/IRadarSourceProcessingHandlerExecutionMetadata.cs) з класифікацією `Mergeable`. Це сигналізує нашому рантайму, що аналітик вміє розраховувати проміжні результати незалежно для кожного батча, а отже, система може запускати обробку його даних паралельно на кількох ядрах процесора, не створюючи затримок для головної черги.

### Як це працює з дельтою

У послідовному режимі handler може писати одразу у свої slots всередині глобального [`RadarSourceProcessingStateStore`](../../../src/Domain/Processing/Handlers/Services/RadarSourceProcessingStateStore/RadarSourceProcessingStateStore.Handlers.cs). Але у впорядкованому конкурентному режимі це було б небезпечно: батч `42` може завершитися раніше за батч `41`, хоча застосовувати їх до фінального стану треба тільки в порядку provider sequence. Тому handler спершу працює не з глобальним станом, а з тимчасовим щільним станом батча: [`ApplyHandlersToDenseState`](../../../src/Infrastructure/Processing/Queueing/Services/RadarProcessingQueuedProcessingSession/RadarProcessingQueuedProcessingSession.OrderedConcurrent.DenseState.cs) викликає `Process` для кожної routed-події, накопичує локальні slots і тільки після цього перетворює їх на [`RadarProcessingHandlerDeltaValue`](../../../src/Domain/Processing/Handlers/Models/RadarProcessingHandlerDeltaValue.cs).

Для `CounterChecksum` дельта є компактним описом того, що цей конкретний батч додав до аналітичного стану. Наприклад, уявімо, що батч з provider sequence `58` містить три події для source `17`:

```text
event 1: payload values = 40, raw checksum = 110
event 2: payload values = 50, raw checksum = 120
event 3: payload values = 60, raw checksum = 130
```

Після трьох викликів `Process` локальні slots handler-а для цього source матимуть такий вигляд:

```text
benchmark.events         = 3
benchmark.payload_values = 150
benchmark.raw_checksum   = 360
```

Рантайм пакує ці значення в [`RadarProcessingHandlerDelta`](../../../src/Domain/Processing/Handlers/Models/RadarProcessingHandlerDelta.cs): додає назву handler-а `benchmark.counter_checksum`, версію контракту `v1`, provider sequence, durable batch id, кількість подій, кількість source-ів, payload value count і input checksum. Це не просто набір чисел; це дельта з ідентичністю. Якщо той самий батч буде повторно відтворений з тим самим payload-ом, координатор злиття впізнає еквівалентне повторне відтворення. Якщо під тим самим `DeltaId` прийде інший payload, [`RadarProcessingHandlerDeltaMergeCoordinator`](../../../src/Domain/Processing/Handlers/Services/RadarProcessingHandlerDeltaMergeCoordinator/RadarProcessingHandlerDeltaMergeCoordinator.Complete.cs) відхилить його як конфлікт.

Далі дельта не одразу пишеться у slots ядра. Вона проходить через впорядковане злиття. Якщо sequence `59` уже готовий, але sequence `58` ще ні, `59` чекає. Коли `58` стає наступним очікуваним sequence, merger `CounterChecksum` застосовує адитивну семантику: додає значення дельти до вже злитих підсумків. Якщо до цього для source `17` було `benchmark.events = 1000`, `benchmark.payload_values = 250000`, `benchmark.raw_checksum = 9000000`, після злиття sequence `58` координатор поверне оновлені значення:

```text
benchmark.events         = 1003
benchmark.payload_values = 250150
benchmark.raw_checksum   = 9000360
```

Лише ці вже впорядковано злиті значення записуються назад у state store через [`ApplyMergedHandlerValueGroups`](../../../src/Domain/Processing/Handlers/Services/RadarSourceProcessingStateStore/RadarSourceProcessingStateStore.Deltas.cs). Тобто custom handler залишається швидкою локальною формулою, а дельта-контур вирішує іншу задачу: як безпечно перенести результат цієї формули з паралельного worker-а у фінальний детермінований стан системи.

---

## 21.4. Компіляція та девіртуалізація JIT: Боротьба з віртуальним викликом

Коли ми викликаємо метод `Process` для кожного обробника у циклі:

```csharp
foreach (var assignment in handlerSlotLayout.Assignments)
{
    assignment.Handler.Process(context, CreateHandlerState(sourceId, assignment));
}
```

З точки зору архітектури C#, це виклик інтерфейсного методу. На рівні низькорівневого коду (ASM) це означає непрямий виклик (indirect call) через таблицю віртуальних методів (vtable). Процесору доводиться зчитувати адресу методу з пам'яті (що призводить до додаткової операції зчитування та потенційного cache-miss), а конвеєру CPU — виконувати передбачення переходів (branch prediction), що уповільнює гарячий шлях обробки NEXRAD-подій.

Оскільки через систему проходять мільйони подій на секунду на нашому Ryzen 9 процесорі, кожна наносекунда на рахунку. Рантайм .NET (починаючи з .NET 8 і далі) має кілька JIT-оптимізацій, які можуть зменшити цю ціну за відповідного профілю викликів:

### 1. Повна девіртуалізація
Якщо JIT бачить мономорфний або добре передбачуваний call site, він може перетворити частину інтерфейсних викликів на прямі виклики до конкретного типу. Після цього відкривається шанс на **інлайнінг** (inlining), коли код `Process` вбудовується в цикл обробки подій і hot path платить менше за абстракцію. Ключове слово тут — “може”: цю властивість не варто продавати як гарантію, її треба перевіряти профілем на конкретному runtime і handler-наборі.

### 2. Захищена девіртуалізація
Якщо в системі зареєстровано кілька різних обробників (наприклад, [`CounterChecksumBenchmarkHandler`](../../../src/Infrastructure/Processing/Benchmarks/Models/RadarProcessingBenchmarkHandlers/RadarProcessingBenchmarkHandlers.CounterChecksum.cs) та [`HeavySampledChecksumHandler`](../../../src/Infrastructure/Processing/Benchmarks/Models/RadarProcessingBenchmarkHandlers/RadarProcessingBenchmarkHandlers.HeavySampledChecksum.cs)), пряма девіртуалізація може бути недоступною. Тоді JIT за відповідного профілю викликів може використати механізм GDV: створити швидку перевірку типу перед викликом і залишити загальний інтерфейсний шлях як fallback:

```csharp
if (assignment.Handler is CounterChecksumBenchmarkHandler counterHandler)
{
    counterHandler.Process(context, ...); // Прямий виклик, потенційно придатний для inline
}
else if (assignment.Handler is HeavySampledChecksumHandler heavyHandler)
{
    heavyHandler.Process(context, ...); // Прямий виклик, потенційно придатний для inline
}
else
{
    assignment.Handler.Process(context, ...); // Стандартний віртуальний виклик
}
```

JIT збирає статистику викликів (tiered compilation), а ми, зі свого боку, можемо винести найчастіші типи обробників на явний fast path. Якщо production-профіль показує один домінантний handler, гарячий контур перестає платити повну ціну абстракції на кожному виклику: більшість роботи проходить через прямий код, а загальний віртуальний шлях лишається резервом для рідкісних або зовнішніх типів.

Оптимізаційний відступ: для випадків, де динамічний поліморфізм не потрібен, а швидкість має бути граничною, можна застосувати патерн **Generic Struct Handler** (узагальнені структури з обмеженням інтерфейсу). Коли метод обробки параметризується типом `T where T : struct, IRadarSourceProcessingHandler`, контур стає мономорфним для конкретної структури. Це дає компілятору найкращі умови для девіртуалізації та inline, але остаточну форму машинного коду ми все одно перевіряємо профілем, а не обіцянкою в документації.

Важлива межа: у поточному коді RadarPulse цей патерн не є окремим production-контуром. Реальний шлях виклику handler-а залишається інтерфейсним і проходить через [`RadarSourceProcessingStateStore.ApplyHandlers`](../../../src/Domain/Processing/Handlers/Services/RadarSourceProcessingStateStore/RadarSourceProcessingStateStore.Handlers.cs). `Generic Struct Handler` тут варто читати як оптимізаційну форму, до якої можна спеціалізувати вже наявний handler, якщо профіль покаже, що саме інтерфейсний виклик став вузьким місцем.

Наприклад, реальний [`CounterChecksumBenchmarkHandler`](../../../src/Infrastructure/Processing/Benchmarks/Models/RadarProcessingBenchmarkHandlers/RadarProcessingBenchmarkHandlers.CounterChecksum.cs) сьогодні є класом, але його гаряча частина має форму, яка легко переноситься у struct:

```csharp
private readonly struct CounterChecksumStructHandler :
    IRadarSourceProcessingHandler
{
    private static readonly RadarSourceProcessingHandlerDescriptor HandlerDescriptor =
        new(
            "benchmark.counter_checksum",
            int64SlotCount: 3,
            doubleSlotCount: 0,
            new[]
            {
                new RadarSourceProcessingSnapshotFieldDescriptor(
                    "benchmark.events",
                    RadarSourceProcessingSnapshotFieldType.Int64,
                    slotIndex: 0),
                new RadarSourceProcessingSnapshotFieldDescriptor(
                    "benchmark.payload_values",
                    RadarSourceProcessingSnapshotFieldType.Int64,
                    slotIndex: 1),
                new RadarSourceProcessingSnapshotFieldDescriptor(
                    "benchmark.raw_checksum",
                    RadarSourceProcessingSnapshotFieldType.Int64,
                    slotIndex: 2)
            });

    public RadarSourceProcessingHandlerDescriptor Descriptor => HandlerDescriptor;

    public void Process(
        in RadarSourceProcessingHandlerContext context,
        RadarSourceProcessingState state)
    {
        state.AddInt64(slotIndex: 0, value: 1);
        state.AddInt64(slotIndex: 1, context.PayloadMetrics.PayloadValueCount);
        state.AddInt64(slotIndex: 2, context.PayloadMetrics.RawValueChecksum);
    }
}
```

Тоді спеціалізований hot path міг би викликати його без інтерфейсного dispatch у самому циклі:

```csharp
private static void ProcessWith<THandler>(
    THandler handler,
    in RadarSourceProcessingHandlerContext context,
    RadarSourceProcessingState state)
    where THandler : struct, IRadarSourceProcessingHandler
{
    handler.Process(context, state);
}
```

Суть не в тому, що кожен custom handler треба робити структурою. Суть у trade-off: класовий handler простіший для каталогу розширень і динамічної реєстрації, а generic struct handler доречний лише там, де набір handler-ів відомий наперед і є виміряна потреба прибрати останню ціну віртуального виклику.

---

## 21.5. Порівняння підходів: Ізоляція проти швидкості

При проєктуванні архітектури розширень ми проаналізували три можливі сценарії інтеграції аналітичних модулів. Результати цього аналізу лягли в основу матриці вибору, якою керується наше ядро під час ініціалізації:

| Характеристика | Сценарій А: Пряма модифікація ядра | Сценарій Б: Події через чергу (Out-of-Process) | Наше рішення: користувацькі обробники (Custom Handlers) на State Slots |
| :--- | :--- | :--- | :--- |
| **Пропускна здатність** | Найкоротший шлях у коді, але з високим ризиком регресій | Нижча через серіалізацію та міжпроцесну взаємодію | Висока для гарячого шляху: `Span`/`ref struct`-контекст, фіксовані slots і профільований handler path |
| **Архітектурна ізоляція** | Відсутня (будь-який баг ламає ядро) | Повна (інший процес) | Висока (сендбокс через слоти стану, ref-контексти) |
| **Вплив на збирач сміття (Garbage Collector)** | Високий (неконтрольоване створення об'єктів) | Середній (серіалізація повідомлень) | Низький у гарячому шляху: handler-виклик без heap-виділень на кожну подію; delta/merge живе за межою події |
| **Складність тестування** | Екстремальна (треба мокати все ядро) | Висока (треба піднімати черги/мережу) | Низька (тестується як чиста функція `Process`) |

Ми відхилили Сценарій А через ризик регресій та порушення принципу єдиної відповідальності (Single Responsibility Principle). Сценарій Б був відкинутий як такий, що не відповідає концепції «Лабораторного столу» (занадто багато оверінжинірингу з мережевими чергами для локального рантайму). Наше рішення об'єднало швидкість прямого виклику в межах одного процесу з жорсткою ізоляцією пам'яті через стекові структури.

Таким чином, розробники аналітичних модулів отримали безпечний та простий майданчик для реалізації бізнес-формул. Вони працюють як експерти в лабораторії: отримують конверт із матеріалами дослідження, роблять записи у чітко відведених рядках бланку і повертають його назад. Ядро системи залишається єдиним господарем стану та координатором часу, тому стабільність не залежить від дисципліни кожного нового handler-а.

---

## Матеріали справи

### 1. Вердикт детективів
Створення інтерфейсу розширень `користувацькі обробники (Custom Handlers)` (Віха `024`). Будь-яка аналітична логіка (підрахунок середньої температури, грозові попередження) виноситься за межі ядра системи та підключається через контракти. Це зберегло чистоту ядра [`RadarProcessingCore`](../../../src/Domain/Processing/Core/Services/RadarProcessingCore/RadarProcessingCore.cs) та ізолювало його від побічних ефектів.

#### Чому аналітики працюють поруч із ядром, але не всередині нього
Можна було дозволити аналітикам писати прямо в ядро: швидко, без додаткових контрактів, але кожна нова метрика ставала б ризиком для стану ядра. Можна було винести аналітику в окремий процес через чергу, але тоді серіалізація й IPC з'їдали б гарячий шлях. Ми обрали користувацькі обробники на контрольованих state slots: розширення поряд із ядром, але не всередині його приватної пам'яті. Ціна вибору — суворий контракт життєвого циклу обробника; виграш — розширюваність без втрати локальності кешу і без розмиття доменного ядра.

### 2. Закони фізики рантайму
* **Ізоляція пам'яті обробника**: Обробники отримують лише немутабельні представлення подій та не мають права змінювати вхідний батч.
* **Реєстрація в каталозі**: Кожен кастомний обробник має проходити через явний контракт фабрики/каталогу, наприклад [`RadarProcessingBenchmarkHandlers`](../../../src/Infrastructure/Processing/Benchmarks/Models/RadarProcessingBenchmarkHandlers/RadarProcessingBenchmarkHandlers.cs) або продуктовий [`RadarPulseProductHandlerFactory`](../../../src/Infrastructure/Product/Pipeline/Handlers/RadarPulseProductHandlerFactory.cs).

### 3. Патологоанатомічний звіт
* **Зависання обробника**: Повільний обробник створює штучний утриманий тиск пам'яті (Retained Pressure), що автоматично сповільнює впорядковану фіксацію (Ordered Commit) для наступних батчів конвеєра.

### 4. Слід доказової бази
* Контракт обробника: [IRadarSourceProcessingHandler.cs](../../../src/Domain/Processing/Handlers/Contracts/IRadarSourceProcessingHandler.cs)
* Тести кастомних обробників: [src/Domain/Processing/Handlers/](../../../src/Domain/Processing/Handlers)

### 5. Протокол допиту процесу
Запуск тестування роботи кастомних аналітичних модулів:
```bash
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~Handlers"
```

# Розділ 21: Спеціальні аналітики (користувацькі обробники, Custom Handlers)

Extension point у продуктивному ядрі завжди небезпечний. Він починається невинно: ще одна формула, ще один notifier, ще один запис у базу прямо всередині parser-а або processor-а. Через кілька ітерацій ядро вже не обробляє радарні події, а тягне чужі побічні ефекти.

У RadarPulse `RadarProcessingCore` має залишатися чистим, швидким і сфокусованим на базовій роботі: демаршалізації NEXRAD-архівів та просуванні часової послідовності `Provider Sequence`. Але споживачам результатів — кліматичним аналітикам, операторам, дослідникам — потрібні власні розрахунки поверх цих даних. Архітектурне питання звучить так: як впустити експертів у лабораторію, не віддавши їм ключі від ядра?

Для розв'язання цього конфлікту ми розробили концепцію **користувацькі обробники (Custom Handlers)** (Віха `024`). Це незалежні аналітичні експерти, які підключаються до рантайму через чітко визначені порти (Ports) і працюють виключно з копіями доказів, не маючи права мутувати внутрішній стан самого ядра.

---

## 21.1. Детективне завдання: Свідки проти експертів

У класичних монолітних системах розробники часто припускаються помилки «все-в-одному». Вони беруть обробник радарної точки та безпосередньо в його тіло вбудовують підрахунок середньої температури, виявлення торнадо, надсилання повідомлень у Telegram та запис у базу даних. Як наслідок, будь-яка зміна у формулі розрахунку грозового фронту ламає базовий парсер бінарних даних.

Ми пішли шляхом чистої архітектури Ports and Adapters. Наше ядро обробки радарних даних декларує інтерфейс-порт `IRadarSourceProcessingHandler`. Будь-який аналітичний модуль — це зовнішній експерт, який погоджується грати за правилами ядра:

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

Зверніть увагу на параметри методу `Process`. Контекст події (`RadarSourceProcessingHandlerContext`) передається за модифікатором `in` (як read-only посилання), а стан обробника (`RadarSourceProcessingState`) передається як структура. Вони спроектовані як `ref struct`, тобто компілятор не дозволяє винести ці контексти в heap, захопити їх у lambda або зберегти після виклику. Це наш перший архітектурний запобіжник: аналітик отримує докази для аналізу, заповнює відведені slots і не може забрати вказівник на внутрішню пам'ять із собою.

---

## 21.2. Анатомія доменного сейфу: `RadarSourceProcessingState`

Для того щоб експерти не заважали один одному і не створювали конкуренції за пам'ять (Race Conditions), ядро виділяє кожному зареєстрованому обробнику його власний ізольований робочий простір. Замість того, щоб дозволити аналітикам виділяти довільні об'єкти в купі (Heap) та створювати навантаження на збирач сміття (Garbage Collector, GC), ми розробили модель **State Slots**.

Кожен обробник описує свої потреби в пам'яті через `RadarSourceProcessingHandlerDescriptor`. Він заздалегідь декларує, скільки цілочисельних комірок (`Int64`) та комірок з плаваючою крапкою (`Double`) йому потрібно для збереження стану по кожному джерелу радарних даних (`Source`).

Ядро системи виділяє один безперервний масив пам'яті для всіх обробників разом, а в момент виклику конкретного аналітика надає йому індивідуальну проекцію у вигляді `RadarSourceProcessingState`:

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

## 21.3. Практичний випадок: Аналітик-криміналіст `CounterChecksum`

Давайте подивимося на реального аналітика, що використовується в наших тестах продуктивності та інтеграційних сценаріях — `CounterChecksumBenchmarkHandler`. Його завдання — фіксувати кількість оброблених подій, обсяг метаданих та контрольну суму корисного навантаження (Payload) для перевірки цілісності передачі.

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

Цей обробник чітко розділяє свою роботу на дві складові. З одного боку, він є стандартним `IRadarSourceProcessingHandler`, що обробляє події через інтерфейс `Process`. З іншого боку, він декларує свою готовність до паралельної обробки через `IRadarSourceProcessingHandlerExecutionMetadata` з класифікацією `Mergeable`. Це сигналізує нашому рантайму, що аналітик вміє розраховувати проміжні результати незалежно для кожного батча, а отже, система може запускати обробку його даних паралельно на кількох ядрах процесора, не створюючи затримок для головної черги.

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

Оскільки через систему проходять мільйони подій на секунду на нашому Ryzen 9 процесорі, кожна наносекунда на рахунку. Рантайм .NET (починаючи з .NET 8 і далі) застосовує дві ключові JIT-оптимізації для вирішення цієї проблеми:

### 1. Повна девіртуалізація (Devirtualization)
Якщо JIT бачить мономорфний або добре передбачуваний call site, він може перетворити частину інтерфейсних викликів на прямі виклики до конкретного типу. Після цього відкривається шанс на **інлайнінг** (inlining), коли код `Process` вбудовується в цикл обробки подій і hot path платить менше за абстракцію. Ключове слово тут — “може”: цю властивість не варто продавати як гарантію, її треба перевіряти профілем на конкретному runtime і handler-наборі.

### 2. Захищена девіртуалізація (Guarded Devirtualization - GDV)
Якщо в системі зареєстровано кілька різних обробників (наприклад, `CounterChecksumBenchmarkHandler` та `HeavySampledChecksumHandler`), пряма девіртуалізація неможлива. Тоді JIT використовує механізм GDV. Він створює швидку перевірку типу перед викликом:

```csharp
if (assignment.Handler is CounterChecksumBenchmarkHandler counterHandler)
{
    counterHandler.Process(context, ...); // Прямий виклик (інлайнений!)
}
else if (assignment.Handler is HeavySampledChecksumHandler heavyHandler)
{
    heavyHandler.Process(context, ...); // Прямий виклик (інлайнений!)
}
else
{
    assignment.Handler.Process(context, ...); // Стандартний віртуальний виклик
}
```

JIT збирає статистику викликів (tiered compilation), а ми, зі свого боку, можемо винести найчастіші типи обробників на явний fast path. Якщо production-профіль показує один домінантний handler, гарячий контур перестає платити повну ціну абстракції на кожному виклику: більшість роботи проходить через прямий код, а загальний віртуальний шлях лишається резервом для рідкісних або зовнішніх типів.

Для випадків, де динамічний поліморфізм не потрібен, а швидкість має бути граничною, ми можемо застосувати патерн **Generic Struct Handler** (узагальнені структури з обмеженням інтерфейсу). Коли метод обробки параметризується типом `T where T : struct, IRadarSourceProcessingHandler`, контур стає мономорфним для конкретної структури. Це дає компілятору найкращі умови для девіртуалізації та inline, але остаточну форму машинного коду ми все одно перевіряємо профілем, а не обіцянкою в документації.

---

## 21.5. Порівняння підходів: Ізоляція проти швидкості

При проєктуванні архітектури розширень ми проаналізували три можливі сценарії інтеграції аналітичних модулів. Результати цього аналізу лягли в основу матриці вибору, якою керується наше ядро під час ініціалізації:

| Характеристика | Сценарій А: Пряма модифікація ядра | Сценарій Б: Події через чергу (Out-of-Process) | Наше рішення: користувацькі обробники (Custom Handlers) на State Slots |
| :--- | :--- | :--- | :--- |
| **Пропускна здатність** | Найкоротший шлях у коді, але з високим ризиком регресій | Нижча через серіалізацію та міжпроцесну взаємодію | Висока для hot path: `Span`/`ref struct`-контекст, фіксовані slots і профільований handler path |
| **Архітектурна ізоляція** | Відсутня (будь-який баг ламає ядро) | Повна (інший процес) | Висока (сендбокс через слоти стану, ref-контексти) |
| **Вплив на збирач сміття (Garbage Collector)**| Високий (неконтрольоване створення об'єктів) | Середній (серіалізація повідомлень) | Нульовий (стекова алокація та фіксовані масиви) |
| **Складність тестування** | Екстремальна (треба мокати все ядро) | Висока (треба піднімати черги/мережу) | Низька (тестується як чиста функція `Process`) |

Ми відхилили Сценарій А через ризик регресій та порушення принципу єдиної відповідальності (Single Responsibility Principle). Сценарій Б був відкинутий як такий, що не відповідає концепції «Лабораторного столу» (занадто багато оверінжинірингу з мережевими чергами для локального рантайму). Наше рішення об'єднало швидкість прямого виклику в межах одного процесу з жорсткою ізоляцією пам'яті через стекові структури.

Таким чином, розробники аналітичних модулів отримали безпечний та простий майданчик для реалізації бізнес-формул. Вони працюють як експерти в лабораторії: отримують конверт із матеріалами дослідження, роблять записи у чітко відведених рядках бланку і повертають його назад. Ядро системи залишається єдиним господарем стану та координатором часу, тому стабільність не залежить від дисципліни кожного нового handler-а.
---

## 🔍 Матеріали справи (Investigation Case Files)

### 1. Вердикт детективів (Decision Trace & Rationale)
Створення інтерфейсу розширень `користувацькі обробники (Custom Handlers)` (Віха `024`). Будь-яка аналітична логіка (підрахунок середньої температури, грозові попередження) виноситься за межі ядра системи та підключається через контракти. Це зберегло чистоту ядра `RadarProcessingCore` та ізолювало його від побічних ефектів.

#### Чому аналітики працюють поруч із ядром, але не всередині нього
Можна було дозволити аналітикам писати прямо в ядро: швидко, без додаткових контрактів, але кожна нова метрика ставала б ризиком для core state. Можна було винести аналітику в окремий процес через чергу, але тоді серіалізація й IPC з'їдали б hot path. Ми обрали custom handlers на контрольованих state slots: розширення поряд із ядром, але не всередині його приватної пам'яті. Ціна вибору — суворий контракт handler lifecycle; виграш — extensibility без втрати cache locality і без розмиття доменного ядра.

### 2. Закони фізики рантайму (System Invariants)
* **Ізоляція пам'яті обробника**: Обробники отримують лише немутабельні представлення подій та не мають права змінювати вхідний батч.
* **Реєстрація в каталозі**: Кожен кастомний обробник має бути зареєстрований у каталозі `HandlerCatalog`.

### 3. Патологоанатомічний звіт (Failure Modes & Recovery)
* **Зависання обробника**: Повільний обробник створює штучний утриманий тиск пам'яті (Retained Pressure), що автоматично сповільнює впорядковану фіксацію (Ordered Commit) для наступних батчів конвеєра.

### 4. Слід доказової бази (Implementation & Tests)
* Контракт обробника: [IRadarSourceProcessingHandler.cs](../../../src/Domain/Processing/Handlers/Contracts/IRadarSourceProcessingHandler.cs)
* Тести кастомних обробників: [src/Domain/Processing/Handlers/](../../../src/Domain/Processing/Handlers)

### 5. Протокол допиту процесу (Verification Commands)
Запуск тестування роботи кастомних аналітичних модулів:
```bash
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~Handlers"
```

# Розділ 21: Спеціальні аналітики (Custom Handlers)

Кожен слідчий знає: огляд місця злочину має здійснюватися за суворим протоколом. Офіцери огороджують територію жовтою стрічкою, криміналісти збирають гільзи та відбитки пальців, а фотограф фіксує положення тіла. Це — ядро розслідування. Якщо туди увійде натовп сторонніх консультантів (експертів із балістики, графологів, психоаналітиків) і почне пересувати докази або залишати власні сліди на брудній підлозі, справу буде розвалено в суді.

У нашому інженерному детективі ядро системи обробки радарних даних (`RadarProcessingCore`) виконує роль тієї самої обгородженої території. Воно має залишатися чистим, швидким і сфокусованим лише на базових метеорологічних подіях — демаршалізації NEXRAD-архівів та просуванні часової послідовності `Provider Sequence`. Але що робити, якщо споживачам результатів розслідування (наприклад, кліматичним аналітикам або військовим операторам) потрібні власні специфічні розрахунки на основі цих даних?

Для розв'язання цього конфлікту ми розробили концепцію **Custom Handlers** (Віха `024`). Це незалежні аналітичні експерти, які підключаються до рантайму через чітко визначені порти (Ports) і працюють виключно з копіями доказів, не маючи права мутувати внутрішній стан самого ядра.

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

Зверніть увагу на параметри методу `Process`. Контекст події (`RadarSourceProcessingHandlerContext`) передається за модифікатором `in` (як read-only посилання), а стан обробника (`RadarSourceProcessingState`) передається як структура. Вони спроектовані як `ref struct`, що робить їх повністю стековими об'єктами. Це наш перший архітектурний запобіжник: жоден аналітик не має права зберігати посилання на ці контексти поза межами виклику методу. Вони отримують докази для аналізу, заповнюють протокол і миттєво звільняють місце.

---

## 21.2. Анатомія доменного сейфу: `RadarSourceProcessingState`

Для того щоб експерти не заважали один одному і не створювали конкуренції за пам'ять (Race Conditions), ядро виділяє кожному зареєстрованому обробнику його власний ізольований робочий простір. Замість того, щоб дозволити аналітикам виділяти довільні об'єкти в купі (Heap) та створювати навантаження на Garbage Collector (GC), ми розробили модель **State Slots**.

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

Цей дизайн є шедевром низькорівневої оптимізації. Завдяки використанню `Span<T>` та `ref struct`, обробник оперує виділеним йому зрізом пам'яті з нульовими алокаціями в купі. Більш того, арифметичні операції виконуються із ключовим словом `checked`, що гарантує негайне виявлення переповнення даних (Overflow Detection). Якщо аналітик спробує вийти за межі відведених йому комірок, система миттєво викине `ArgumentOutOfRangeException`, локалізуючи помилку та запобігаючи пошкодженню сусідніх даних.

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

## 21.4. Порівняння підходів: Ізоляція проти швидкості

При проєктуванні архітектури розширень ми проаналізували три можливі сценарії інтеграції аналітичних модулів. Результати цього аналізу лягли в основу матриці вибору, якою керується наше ядро під час ініціалізації:

| Характеристика | Сценарій А: Пряма модифікація ядра | Сценарій Б: Події через чергу (Out-of-Process) | Наше рішення: Custom Handlers на State Slots |
| :--- | :--- | :--- | :--- |
| **Пропускна здатність** | Максимальна (без додаткових викликів) | Низька (витрати на міжпроцесну взаємодію) | Висока (нуль алокацій, прямі виклики через Span) |
| **Архітектурна ізоляція** | Відсутня (будь-який баг ламає ядро) | Повна (інший процес) | Висока (сендбокс через слоти стану, ref-контексти) |
| **Вплив на Garbage Collector**| Високий (неконтрольоване створення об'єктів) | Середній (серіалізація повідомлень) | Нульовий (стекова алокація та фіксовані масиви) |
| **Складність тестування** | Екстремальна (треба мокати все ядро) | Висока (треба піднімати черги/мережу) | Низька (тестується як чиста функція `Process`) |

Ми відхилили Сценарій А через ризик регресій та порушення принципу єдиної відповідальності (Single Responsibility Principle). Сценарій Б був відкинутий як такий, що не відповідає концепції «Лабораторного столу» (занадто багато оверінжинірингу з мережевими чергами для локального рантайму). Наше рішення об'єднало швидкість прямого виклику в межах одного процесу з жорсткою ізоляцією пам'яті через стекові структури.

Таким чином, розробники аналітичних модулів отримали безпечний та простий майданчик для реалізації бізнес-формул. Вони працюють як експерти в лабораторії: отримують конверт із матеріалами дослідження, роблять записи у чітко відведених рядках бланку і повертають його назад. Ядро системи залишається єдиним господарем стану та координатором часу, гарантуючи стабільність та передбачуваність роботи всієї системи RadarPulse.
---

## 🔍 Матеріали справи (Investigation Case Files)

### 1. Вердикт детективів (Decision Trace & Rationale)
Створення інтерфейсу розширень `Custom Handlers` (Віха `024`). Будь-яка аналітична логіка (підрахунок середньої температури, грозові попередження) виноситься за межі ядра системи та підключається через контракти. Це зберегло чистоту ядра `RadarProcessingCore` та ізолювало його від побічних ефектів.

### 2. Закони фізики рантайму (System Invariants)
* **Ізоляція пам'яті обробника**: Обробники отримують лише немутабельні представлення подій та не мають права змінювати вхідний батч.
* **Реєстрація в каталозі**: Кожен кастомний обробник має бути зареєстрований у каталозі `HandlerCatalog`.

### 3. Патологоанатомічний звіт (Failure Modes & Recovery)
* **Зависання обробника**: Повільний обробник створює штучне Retained Pressure, що автоматично сповільнює Ordered Commit для наступних батчів конвеєра.

### 4. Слід доказової бази (Implementation & Tests)
* Контракт обробника: [IRadarProcessingCustomHandler.cs](../../../src/Domain/Processing/Handlers/Contracts/IRadarSourceProcessingHandler.cs)
* Тести кастомних обробників: [src/Domain/Processing/Handlers/](../../../src/Domain/Processing/Handlers)

### 5. Протокол допиту процесу (Verification Commands)
Запуск тестування роботи кастомних аналітичних модулів:
```bash
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~Handlers"
```

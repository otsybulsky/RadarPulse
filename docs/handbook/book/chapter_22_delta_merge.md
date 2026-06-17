# Розділ 22: Контракт злиття дельт

Користувацькі обробники стають по-справжньому корисними лише тоді, коли не повертають систему назад до послідовного виконання. Воркер має право швидко порахувати локальні зміни для свого [`RadarEventBatch`](../../../src/Domain/Streaming/Batches/Models/RadarEventBatch.cs), але не має права самовільно оновлювати спільний стан аналітика. Інакше точка розширення знову відкриває двері до гонок, брудних читань і непередбачуваних метрик.

Протилежна крайність теж погана: змусити всі обробники чекати повністю послідовного шляху. Тоді паралельний рантайм начебто існує, але аналітика знову стає вузьким місцем. Нам потрібен середній шлях: воркери швидко створюють локальні звіти про зміни — **дельти (Delta)**, а рантайм зливає їх у правильному порядку.

Для вирішення цієї проблеми у віхах `024` та `025` ми реалізували архітектурний патерн **Delta/Merge Contract** на реальних інтерфейсах [`IRadarProcessingHandlerDeltaMerger`](../../../src/Domain/Processing/Handlers/Contracts/IRadarProcessingHandlerDeltaMerger.cs) та [`IRadarProcessingHandlerDeltaAccumulatorFactory`](../../../src/Domain/Processing/Handlers/Contracts/IRadarProcessingHandlerDeltaAccumulatorFactory.cs). Він дозволяє аналітикам обчислювати зміни паралельно, а координатору — зливати їх послідовно та детерміновано.

Простими словами, дельта — це не нова копія всього стану системи, а короткий запис у журналі змін: “після обробки цього батча ось такі поля для ось таких джерел змінилися ось на такі значення”. Воркер не бере глобальний lock і не пише напряму в спільну пам'ять аналітика. Він обробляє свій батч локально, пакує результат у [`RadarProcessingHandlerDelta`](../../../src/Domain/Processing/Handlers/Models/RadarProcessingHandlerDelta.cs) і віддає його координатору.

Для лічильника `CounterChecksum` це можна уявити так:

```text
batch sequence: 58
source: 17

benchmark.events         += 3
benchmark.payload_values += 150
benchmark.raw_checksum   += 360
```

Якщо глобальний стан до цього мав `benchmark.events = 1000`, то після злиття цієї дельти він стане `1003`. Але це станеться тільки тоді, коли sequence `58` дійде своєї черги. Якщо sequence `57` ще не застосований, дельта `58` чекає в буфері. Саме так система отримує дві речі одночасно: воркери рахують паралельно, але фінальний стан змінюється в одному зрозумілому порядку.

Важливо, що дельта — це не просто “плюс три”. У неї є ім'я обробника, версія контракту, provider sequence, durable batch id, кількість подій, payload count і checksum входу. Завдяки цьому координатор може відрізнити нормальне повторне відтворення того самого батча від конфлікту, де під тією самою ідентичністю прийшли інші дані.

---

## 22.1. Контракт дельти та злиття: Розділяй та володарюй

Суть контракту полягає у розподілі обчислень на дві фази:
1. **Паралельна фаза (stateless/source-local):** Воркери в окремих потоках обробляють свої батчі. Вони застосовують метод `Process` і створюють немутабельний пакет накопичених змін для конкретного батча — [`RadarProcessingHandlerDelta`](../../../src/Domain/Processing/Handlers/Models/RadarProcessingHandlerDelta.cs).
2. **Послідовна фаза (ordered merge):** Координатор збирає ці дельти і передає їх спеціальному об'єкту — [`IRadarProcessingHandlerDeltaMerger`](../../../src/Domain/Processing/Handlers/Contracts/IRadarProcessingHandlerDeltaMerger.cs) або [`IRadarProcessingHandlerDeltaAccumulator`](../../../src/Domain/Processing/Handlers/Contracts/IRadarProcessingHandlerDeltaAccumulator.cs), який об'єднує дельти у фінальний стан строго за порядком [`Provider Sequence`](../../../src/Domain/Processing/Queueing/Models/RadarProcessingQueuedBatchSequence.cs).

### Як обробник отримує право на злиття

Обробник не отримує право на паралельне злиття автоматично тільки тому, що в нього є метод `Process`. Він має довести системі три речі: його роботу можна порахувати локально для одного батча, результат можна описати як дельту, а кілька таких дельт можна безпечно злити у фінальний стан.

У коді це починається з класифікації виконання. Обробник має реалізувати [`IRadarSourceProcessingHandlerExecutionMetadata`](../../../src/Domain/Processing/Handlers/Contracts/IRadarSourceProcessingHandlerExecutionMetadata.cs) і явно сказати, що його результат можна представити через дельти:

```csharp
public RadarSourceProcessingHandlerExecutionClassification ExecutionClassification =>
    RadarSourceProcessingHandlerExecutionClassification.Mergeable;
```

Якщо обробник не реалізує цей контракт метаданих, [`RadarProcessingHandlerOutputContract`](../../../src/Application/Processing/Contracts/RadarProcessingHandlerOutputContract.cs) не робить оптимістичних припущень. Він класифікує такий обробник як `SnapshotOnly`: результат можна показати з уже зафіксованого snapshot, але не можна запускати ordered handler delta/merge.

Правило навмисно консервативне. Один `SnapshotOnly` обробник у наборі переводить увесь набір обробників у послідовний snapshot fallback. Якщо обробник позначений як `Unsupported`, рантайм взагалі блокує такий набір для MVP-поверхні. Це звучить суворо, але саме так система не продає паралельність там, де не доведено безпечне злиття.

Другий крок — стабільний контракт злиття. Обробник, придатний до злиття, має реалізувати [`IRadarProcessingHandlerDeltaMerger`](../../../src/Domain/Processing/Handlers/Contracts/IRadarProcessingHandlerDeltaMerger.cs): назвати себе тим самим іменем, що в descriptor, зафіксувати версію контракту і описати, як одна дельта додається до вже злитого стану.

```csharp
public string HandlerName => "benchmark.counter_checksum";
public string HandlerContractVersion => "v1";

public IReadOnlyList<RadarProcessingHandlerDeltaValue> Merge(
    IReadOnlyList<RadarProcessingHandlerDeltaValue> currentValues,
    RadarProcessingHandlerDelta delta) =>
    MergeInt64Values(currentValues, delta);
```

Третій крок — дисципліна самого `Process`. Він не повинен писати у глобальний стан, відкривати приховані shared counters або залежати від порядку завершення worker-ів. Його безпечна зона — `context` поточної події і виділені slots у [`RadarSourceProcessingState`](../../../src/Domain/Processing/Handlers/Models/RadarSourceProcessingState.cs). Саме тому runtime може прогнати handler по тимчасовому dense state батча, а потім зібрати з його snapshot fields набір [`RadarProcessingHandlerDeltaValue`](../../../src/Domain/Processing/Handlers/Models/RadarProcessingHandlerDeltaValue.cs) у [`CreateHandlerDeltaValues`](../../../src/Infrastructure/Processing/Queueing/Services/RadarProcessingQueuedProcessingSession/RadarProcessingQueuedProcessingSession.OrderedConcurrent.DenseState.cs).

Для простого лічильника це працює природно. `CounterChecksum` для кожної події робить тільки додавання:

```text
events         += 1
payload_values += payload_value_count
raw_checksum   += raw_value_checksum
```

Такі операції легко перетворити на дельту батча: якщо worker обробив 3 події й 150 payload-значень, він не мусить знати глобальний total. Він повертає “+3, +150, +checksum”, а координатор застосує це у правильному sequence.

Добрі кандидати на злиття — максимум, мінімум, сума, кількість, checksum або інша агрегована метрика з явно визначеним правилом накопичення. Погані кандидати — обробники, які залежать від “останнього побаченого” global state, пишуть у зовнішню базу або приймають рішення залежно від порядку прибуття worker-ів. Для них потрібен окремий протокол, інакше паралельність буде удаваною.

Окремо стоїть питання швидкості самого злиття. Якщо операція математично правильна, але базовий `Merge(currentValues, delta)` створює забагато проміжних списків, обробник може додати оптимізований accumulator. Це не нове право на паралельність, а пришвидшення вже доведеного контракту.

Давайте подивимося на інтерфейси, які складають основу цього контракту в системі:

```csharp
namespace RadarPulse.Domain.Processing;

/// <summary>
/// Merge contract for handlers that can combine per-batch deltas deterministically.
/// </summary>
public interface IRadarProcessingHandlerDeltaMerger
{
    string HandlerName { get; }
    string HandlerContractVersion { get; }

    /// <summary>
    /// Merges current exported values with one incoming delta.
    /// </summary>
    IReadOnlyList<RadarProcessingHandlerDeltaValue> Merge(
        IReadOnlyList<RadarProcessingHandlerDeltaValue> currentValues,
        RadarProcessingHandlerDelta delta);
}
```

Якщо обробник підтримує швидке злиття без зайвих копій знімка, він також може реалізувати фабрику акумуляторів [`IRadarProcessingHandlerDeltaAccumulatorFactory`](../../../src/Domain/Processing/Handlers/Contracts/IRadarProcessingHandlerDeltaAccumulatorFactory.cs) для створення спеціальних об'єктів стану.

### Навіщо потрібна фабрика акумуляторів

На перший погляд здається, що достатньо одного методу `Merge(currentValues, delta)`: передали поточний знімок значень, отримали новий знімок, пішли далі. Саме так працює базовий резервний шлях у [`RadarProcessingHandlerDeltaMergeCoordinator`](../../../src/Domain/Processing/Handlers/Services/RadarProcessingHandlerDeltaMergeCoordinator/RadarProcessingHandlerDeltaMergeCoordinator.Drain.cs): якщо акумулятора немає, координатор викликає `merger.Merge(...)`, копіює результат у `mergedValues` і зберігає його як новий стан.

Для великих потоків це може стати дорогим місцем. Навіть якщо сама математика злиття проста, на кожному кроці доводиться будувати або копіювати список значень. Тому обробник може сказати координатору: “для мене краще створити окремий акумулятор, який пам'ятає проміжний стан”. Це робиться через [`IRadarProcessingHandlerDeltaAccumulatorFactory`](../../../src/Domain/Processing/Handlers/Contracts/IRadarProcessingHandlerDeltaAccumulatorFactory.cs):

```csharp
public interface IRadarProcessingHandlerDeltaAccumulatorFactory
{
    IRadarProcessingHandlerDeltaAccumulator CreateAccumulator();
}
```

Координатор перевіряє це під час створення:

```csharp
accumulator = merger is IRadarProcessingHandlerDeltaAccumulatorFactory factory
    ? factory.CreateAccumulator()
    : null;
```

Важлива деталь: фабрика створює **новий акумулятор для одного координатора**, а не повертає глобальний singleton. Акумулятор має власний змінний стан: накопичені підсумки, буфери змінених значень і службові структури для злиття. Якщо поділити його між кількома runs або координаторами обробників, дельти різних потоків почнуть забруднювати одна одну. Саме тому контракт називається factory: кожен координатор отримує чистий, ізольований об'єкт злиття.

У реальному коді [`CounterChecksumBenchmarkHandler`](../../../src/Infrastructure/Processing/Benchmarks/Models/RadarProcessingBenchmarkHandlers/RadarProcessingBenchmarkHandlers.CounterChecksum.cs) і [`HeavySampledChecksumBenchmarkHandler`](../../../src/Infrastructure/Processing/Benchmarks/Models/RadarProcessingBenchmarkHandlers/RadarProcessingBenchmarkHandlers.HeavySampledChecksum.cs) реалізують цю фабрику однаково:

```csharp
public IRadarProcessingHandlerDeltaAccumulator CreateAccumulator() =>
    new Int64SumHandlerDeltaAccumulator();
```

[`Int64SumHandlerDeltaAccumulator`](../../../src/Infrastructure/Processing/Benchmarks/Models/RadarProcessingBenchmarkHandlers/RadarProcessingBenchmarkHandlers.Int64Merge.cs) тримає словник `(SourceId, FieldName) -> value`, додає до нього значення з кожної впорядкованої дельти і повертає тільки змінені значення для commit. Це не змінює семантику злиття: порядок усе ще контролює координатор, а fail-closed перевірки лишаються на місці. Фабрика лише дає швидший механізм виконання тієї самої детермінованої операції merge.

### Математика контракту: чому merge не може бути довільним
Коли конвеєр опрацьовує великі потоки даних, послідовне об'єднання дельт може стати вузьким місцем на етапі commit. Але перший обов'язок обробника не “бути швидким”, а довести, що його проміжні результати можна зливати детерміновано. Для цього [`IRadarProcessingHandlerDeltaMerger`](../../../src/Domain/Processing/Handlers/Contracts/IRadarProcessingHandlerDeltaMerger.cs) вимагає явного контракту імені, версії та операції merge.

Для простих сумарних метрик, таких як [`CounterChecksumBenchmarkHandler`](../../../src/Infrastructure/Processing/Benchmarks/Models/RadarProcessingBenchmarkHandlers/RadarProcessingBenchmarkHandlers.CounterChecksum.cs), злиття має властивості, близькі до асоціативності:
* **Стабільне групування:** різне групування однакових дельт має давати той самий фінальний підсумок:

```text
merge(merge(Delta A, Delta B), Delta C)
==
merge(Delta A, merge(Delta B, Delta C))
```

* **Контрольована черговість:** для сум порядок додавання не змінює результат, але рантайм усе одно застосовує дельти за [`Provider Sequence`](../../../src/Domain/Processing/Queueing/Models/RadarProcessingQueuedBatchSequence.cs), бо не всі майбутні обробники будуть комутативними.

Саме тут важлива чесність формулювання. Поточний [`RadarProcessingHandlerDeltaMergeCoordinator`](../../../src/Domain/Processing/Handlers/Services/RadarProcessingHandlerDeltaMergeCoordinator/RadarProcessingHandlerDeltaMergeCoordinator.cs) не будує parallel binary merge tree і не обіцяє повну утилізацію ядер на фазі консолідації.

Його реальний контракт вужчий, але сильніший як доказ: він приймає завершення out-of-order, буферизує їх у `SortedDictionary<long, RadarProcessingHandlerDelta>`, а зливає тільки contiguous sequence. Асоціативність лишається вимогою до mergeable-обробника і можливим наступним optimization gate, а не прихованим claim-ом поточної реалізації.

Майбутній паралельний merge-tree міг би виглядати так:

```text
[Delta 1] -----+
                +--> [Delta 1+2] -----+
[Delta 2] -----+                       |
                                        +--> [Delta 1+2+3+4] (фінальний знімок)
[Delta 3] -----+                       |
                +--> [Delta 3+4] ------+
[Delta 4] -----+
```

Такий підхід зменшив би глибину злиття для комутативних та асоціативних обробників. Але це вже окремий доказовий борг: benchmark, блокування для некомутативних обробників, snapshot semantics і тести еквівалентності. У поточній книзі ми не беремо цей кредит наперед.

---

## 22.2. Робота координатора: [`RadarProcessingHandlerDeltaMergeCoordinator`](../../../src/Domain/Processing/Handlers/Services/RadarProcessingHandlerDeltaMergeCoordinator/RadarProcessingHandlerDeltaMergeCoordinator.cs)

Хто стежить за тим, щоб дельти свідчень підшивалися до справи за порядком? Цю роль виконує [`RadarProcessingHandlerDeltaMergeCoordinator`](../../../src/Domain/Processing/Handlers/Services/RadarProcessingHandlerDeltaMergeCoordinator/RadarProcessingHandlerDeltaMergeCoordinator.cs). Його серце — метод [`CompleteForCommitCore`](../../../src/Domain/Processing/Handlers/Services/RadarProcessingHandlerDeltaMergeCoordinator/RadarProcessingHandlerDeltaMergeCoordinator.Commit.cs) та цикл злиття [`DrainReadyDeltas`](../../../src/Domain/Processing/Handlers/Services/RadarProcessingHandlerDeltaMergeCoordinator/RadarProcessingHandlerDeltaMergeCoordinator.Drain.cs).

Координатор приймає дельти від воркерів у будь-якому порядку (оскільки воркер, що обробляв складні радарні батчі, може закінчити роботу пізніше за того, що обробляв порожнє небо). Він складає їх у буфер `SortedDictionary<long, RadarProcessingHandlerDelta>`, де ключем є номер послідовності [`Provider Sequence`](../../../src/Domain/Processing/Queueing/Models/RadarProcessingQueuedBatchSequence.cs):

```csharp
private DrainReadyDeltasResult DrainReadyDeltas()
{
    var appliedCount = 0;
    IReadOnlyList<RadarProcessingHandlerDeltaValue> appliedValues =
        Array.Empty<RadarProcessingHandlerDeltaValue>();
    List<RadarProcessingHandlerDeltaValue>? combinedAppliedValues = null;

    // Витягуємо дельти, які відповідають наступному очікуваному кроку послідовності
    while (pendingBySequence.Remove(nextProviderSequence.Value, out var delta))
    {
        if (appliedCount > 0 && appliedValues.Count != 0 && combinedAppliedValues is null)
        {
            combinedAppliedValues = new List<RadarProcessingHandlerDeltaValue>(appliedValues.Count);
            combinedAppliedValues.AddRange(appliedValues);
            appliedValues = Array.Empty<RadarProcessingHandlerDeltaValue>();
        }

        IReadOnlyList<RadarProcessingHandlerDeltaValue> deltaAppliedValues;
        try
        {
            // Виконуємо злиття дельти із поточним станом
            deltaAppliedValues = MergeDelta(delta);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            // Фіксуємо блокування у разі помилки злиття
            Block(delta.ProviderSequence, exception.Message);
            break;
        }

        if (deltaAppliedValues.Count != 0)
        {
            if (combinedAppliedValues is null)
            {
                appliedValues = deltaAppliedValues;
            }
            else
            {
                combinedAppliedValues.AddRange(deltaAppliedValues);
            }
        }

        completedById.Add(delta.DeltaId, delta);
        appliedCount++;
        nextProviderSequence = nextProviderSequence.Next(); // Просуваємо межу послідовності
    }

    return new DrainReadyDeltasResult(
        appliedCount,
        combinedAppliedValues ?? appliedValues);
}
```

Цей алгоритм працює як конвеєр: як тільки «повільний» батч з низьким sequence-номером прибуває, координатор дренує крізь цикл злиття всі наступні батчі, які вже чекали в черзі.

---

## 22.3. Оптимізація пам'яті: повторне використання буфера ([`ReusableValueList`](../../../src/Infrastructure/Processing/Benchmarks/Models/RadarProcessingBenchmarkHandlers/RadarProcessingBenchmarkHandlers.Int64Merge.cs))

У високопродуктивних системах обробки великих даних збирач сміття (Garbage Collector) швидко стає частиною бюджету продуктивності. Якщо на кожен комміт батча створювати нові масиви для списку змінених значень, GC починає конкурувати з корисною роботою. Це схоже на ситуацію, коли детектив викидає блокнот і купує новий після кожного записаного речення.

У класі [`Int64SumHandlerDeltaAccumulator`](../../../src/Infrastructure/Processing/Benchmarks/Models/RadarProcessingBenchmarkHandlers/RadarProcessingBenchmarkHandlers.Int64Merge.cs) (який використовується нашим [`CounterChecksumBenchmarkHandler`](../../../src/Infrastructure/Processing/Benchmarks/Models/RadarProcessingBenchmarkHandlers/RadarProcessingBenchmarkHandlers.CounterChecksum.cs)) ми застосували архітектурну хитрість — вкладений клас [`ReusableValueList`](../../../src/Infrastructure/Processing/Benchmarks/Models/RadarProcessingBenchmarkHandlers/RadarProcessingBenchmarkHandlers.Int64Merge.cs):

```csharp
private sealed class ReusableValueList : IReadOnlyList<RadarProcessingHandlerDeltaValue>
{
    private RadarProcessingHandlerDeltaValue[] values = [];
    public int Count { get; private set; }

    public RadarProcessingHandlerDeltaValue this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return values[index];
        }
    }

    public void Reset(RadarProcessingHandlerDeltaValue[] nextValues, int count)
    {
        ArgumentNullException.ThrowIfNull(nextValues);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count > nextValues.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        values = nextValues;
        Count = count;
    }

    public IEnumerator<RadarProcessingHandlerDeltaValue> GetEnumerator()
    {
        for (var i = 0; i < Count; i++)
        {
            yield return values[i];
        }
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
        GetEnumerator();
}
```

Замість того, щоб будувати повний snapshot після кожного кроку злиття, акумулятор тримає словник накопичених значень, перевикористовуваний буфер `changedBuffer` та обгортку [`ReusableValueList`](../../../src/Infrastructure/Processing/Benchmarks/Models/RadarProcessingBenchmarkHandlers/RadarProcessingBenchmarkHandlers.Int64Merge.cs). Ми скидаємо внутрішній покажчик (`Reset`), підставляючи актуальний масив і лічильник елементів.

Це не безкоштовна підсистема: словник, snapshots і fallback-merger мають свою ціну. Але гарячий шлях злиття перестає створювати новий список змінених значень на кожен commit, і саме це підтверджується performance gate-ами для handler delta/merge.

---

## 22.4. Протокол безпеки: закриття за першої підозри

Координатор злиття — це не просто бухгалтер, який сліпо додає цифри. Це консервативний детектив. Якщо дельта не проходить контракт, координатор блокує подальше злиття для цього обробника й повертає fail-closed результат у pipeline.

У коді [`CompleteForCommitCore`](../../../src/Domain/Processing/Handlers/Services/RadarProcessingHandlerDeltaMergeCoordinator/RadarProcessingHandlerDeltaMergeCoordinator.Commit.cs) передбачено кілька рівнів перевірки:
1. **Перевірка схеми:** Якщо дельта має несумісну версію схеми або інше ім'я обробника, вона негайно відхиляється.
2. **Конфлікти дублікатів:** Якщо приходить дельта з ідентичним ID, але іншим вмістом, координатор переходить у стан блокування (`Block`), підозрюючи пошкодження пам'яті або збій передачі даних.
3. **Хронологічні аномалії:** Якщо воркер намагається надіслати дельту для кроку послідовності, який вже пройдений та зафіксований в історії (`sequence < nextProviderSequence.Value`), це трактується як порушення часової логіки.

```csharp
if (sequence < nextProviderSequence.Value)
{
    var message = "Handler delta provider sequence has already passed the merge boundary.";
    Block(delta.ProviderSequence, message);
    return new RadarProcessingHandlerDeltaCommitMergeResult(
        RadarProcessingHandlerDeltaMergeStatus.Rejected,
        appliedDeltaCount: 0,
        message: message);
}
```

У разі блокування, властивість `permanentBlockingSequence` фіксує номер «зламаного» батча, а система переходить у режим **зупинка без прихованої неправди (Fail-Closed)**. Доки адміністратор чи оператор не розбереться з інцидентом (або не перезапустить рантайм через скидання історії), подальше злиття для цього координатора не відбудеться. Так неточні чи скомпрометовані аналітичні метрики не отримують нормальний шлях до фінального звіту.

---

## Матеріали справи

### 1. Вердикт детективів
Створення контракту дельта/злиття (delta/merge) для паралельного обчислення та швидкого злиття дельт аналітики (Віхи `024`-`025`). Обробники, придатні до злиття (mergeable), реалізують [`IRadarProcessingHandlerDeltaMerger`](../../../src/Domain/Processing/Handlers/Contracts/IRadarProcessingHandlerDeltaMerger.cs); оптимізовані обробники можуть додати [`IRadarProcessingHandlerDeltaAccumulatorFactory`](../../../src/Domain/Processing/Handlers/Contracts/IRadarProcessingHandlerDeltaAccumulatorFactory.cs). Це дозволило обробникам вираховувати свої зміни паралельно в різних потоках і зливати їх в один фінальний стан під час впорядкованої фіксації (Ordered Commit) без переходу на повільний послідовний резервний шлях (sequential fallback).

#### Чому швидкий обробник має довести, що він mergeable
Перший безпечний варіант — виконувати всі користувацькі обробники послідовно, тоді порядок очевидний і злиття не потрібне. Але це зводить нанівець паралельний рантайм саме там, де аналітика стає важкою. Другий варіант — дозволити обробникам паралельно мутувати спільний стан, але це повторює кризу спільного стану. Контракт дельта/злиття став пропуском у швидкий коридор: обробник рахує власну дельту паралельно, а фіксація зливає її за provider sequence. Ціна вибору — не кожен обробник автоматично придатний до злиття; виграш — швидкість доступна лише тим розширенням, які довели свою детермінованість.

### 2. Закони фізики рантайму
* **Асоціативність злиття**: Результат злиття дельт обробників має бути детермінованим і не залежати від черговості завершення потоків.
* **Плоский час виконання (flat elapsed gate)**: Для зафіксованого full-cache сценарію handler delta/merge `active=4` має лишатися близьким до `active=1` за часом виконання; накладні виділення пам'яті фіксуються окремо й не маскуються.

### 3. Патологоанатомічний звіт
* **Непідтримуване злиття**: Обробник у режимі `SnapshotOnly` примушує послідовний резервний шлях, а набір з `Unsupported`-обробником блокує MVP processing до старту. Mergeable handler без потрібного merger contract не проходить ordered handler-delta шлях.

### 4. Слід доказової бази
* Контракт злиття: [IRadarProcessingHandlerDeltaMerger.cs](../../../src/Domain/Processing/Handlers/Contracts/IRadarProcessingHandlerDeltaMerger.cs)
* Тести координатора злиття: [RadarProcessingHandlerDeltaMergeCoordinatorTests.cs](../../../tests/RadarPulse.Tests/Processing/Handlers/RadarProcessingHandlerDeltaMergeCoordinatorTests.cs)
* Runtime-тести handler delta/merge: [RadarProcessingMvpHandlerDeltaRuntimeTests](../../../tests/RadarPulse.Tests/Processing/Handlers/RadarProcessingMvpHandlerDeltaRuntimeTests)
* Performance gate: [RadarProcessingHandlerDeltaPerformanceGateTests](../../../tests/RadarPulse.Tests/Processing/Handlers/RadarProcessingHandlerDeltaPerformanceGateTests)

### 5. Протокол допиту процесу
Запуск перевірки швидкості дельта-злиття для важких обробників:
```bash
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~HandlerDelta"
```

# Розділ 22: Контракт злиття дельт (Delta/Merge Contract)

Custom handlers стають по-справжньому корисними лише тоді, коли не повертають систему назад до послідовного виконання. Воркер має право швидко порахувати локальні зміни для свого `RadarEventBatch`, але не має права самовільно оновлювати спільний стан аналітика. Інакше extension point знову відкриває двері до гонок, dirty reads і непередбачуваних метрик.

Протилежна крайність теж погана: якщо всі handler-и примусово чекатимуть `sequential fallback`, паралельний runtime втратить сенс. Нам потрібен контракт, у якому воркери створюють локальні звіти про зміни — **дельти (Delta)**, а runtime зливає їх у правильному порядку.

Для вирішення цієї проблеми у віхах `024` та `025` ми реалізували архітектурний патерн **Delta/Merge Contract** на реальних інтерфейсах `IRadarProcessingHandlerDeltaMerger` та `IRadarProcessingHandlerDeltaAccumulatorFactory`. Він дозволяє аналітикам обчислювати зміни паралельно, а координатору — зливати їх послідовно та детерміновано.

---

## 22.1. Контракт дельти та злиття: Розділяй та володарюй

Суть контракту полягає у розподілі обчислень на дві фази:
1. **Паралельна фаза (stateless/source-local):** Воркери в окремих потоках обробляють свої батчі. Вони застосовують метод `Process` і створюють немутабельний пакет накопичених змін для конкретного батча — `RadarProcessingHandlerDelta`.
2. **Послідовна фаза (ordered merge):** Координатор збирає ці дельти і передає їх спеціальному об'єкту — `IRadarProcessingHandlerDeltaMerger` або `IRadarProcessingHandlerDeltaAccumulator`, який об'єднує дельти у фінальний стан строго за порядком `Provider Sequence`.

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

Якщо обробник підтримує високу швидкість обробки, він також може реалізувати фабрику акумуляторів `IRadarProcessingHandlerDeltaAccumulatorFactory` для створення спеціальних об'єктів стану, які мінімізують накладні витрати на копіювання масивів.

### Математика контракту: чому merge не може бути довільним
Коли конвеєр опрацьовує великі потоки даних, послідовне об'єднання дельт може стати вузьким місцем на етапі commit. Але перший обов'язок handler-а не “бути швидким”, а довести, що його проміжні результати можна зливати детерміновано. Для цього `IRadarProcessingHandlerDeltaMerger` вимагає явного контракту імені, версії та операції merge.

Для простих сумарних метрик, таких як `CounterChecksumBenchmarkHandler`, злиття має властивості, близькі до асоціативності:
* **Стабільне групування:** $(Delta_A \oplus Delta_B) \oplus Delta_C$ має давати той самий фінальний підсумок, що й $Delta_A \oplus (Delta_B \oplus Delta_C)$.
* **Контрольована черговість:** для сум порядок додавання не змінює результат, але runtime усе одно застосовує дельти за `Provider Sequence`, бо не всі майбутні handler-и будуть комутативними.

Саме тут важлива чесність формулювання. Поточний `RadarProcessingHandlerDeltaMergeCoordinator` не будує parallel binary merge tree і не заявляє повну утилізацію ядер на фазі консолідації. Його реальний контракт сильніший для цієї книги: він приймає завершення out-of-order, буферизує їх у `SortedDictionary<long, RadarProcessingHandlerDelta>`, а зливає тільки contiguous sequence. Асоціативність лишається вимогою до mergeable handler-а і можливим наступним optimization gate, а не прихованим claim-ом поточної реалізації.

Концептуально майбутній tree-merge міг би виглядати так:

```text
[Delta 1] ──┐
            ├──► [Delta 1+2] ──┐
[Delta 2] ──┘                  │
                               ├──► [Delta 1+2+3+4] (Фінальний знімок)
[Delta 3] ──┐                  │
            ├──► [Delta 3+4] ──┘
[Delta 4] ──┘
```

Такий tree-merge зменшив би глибину злиття для комутативних/асоціативних handler-ів, але він потребує окремого proof: benchmark, блокування для non-commutative handler-ів, snapshot semantics і тести еквівалентності. У поточній книзі ми не беремо цей кредит наперед.

---

## 22.2. Робота координатора: `RadarProcessingHandlerDeltaMergeCoordinator`

Хто стежить за тим, щоб дельти свідчень підшивалися до справи за порядком? Цю роль виконує `RadarProcessingHandlerDeltaMergeCoordinator`. Його серце — метод `CompleteForCommitCore` та цикл злиття `DrainReadyDeltas`.

Координатор приймає дельти від воркерів у будь-якому порядку (оскільки воркер, що обробляв складні радарні батчі, може закінчити роботу пізніше за того, що обробляв порожнє небо). Він складає їх у буфер `SortedDictionary<long, RadarProcessingHandlerDelta>`, де ключем є номер послідовності `Provider Sequence`:

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

## 22.3. Оптимізація пам'яті: Патрон в обоймі (`ReusableValueList`)

У високопродуктивних системах обробки великих даних збирач сміття (Garbage Collector) — це найлютіший ворог. Якщо на кожен комміт батча створювати нові масиви для списку змінених значень, GC швидко зупинить систему для збору сміття. Це рівносильно тому, якби детектив викидав свій блокнот і купував новий після кожного записаного речення.

У класі `Int64SumHandlerDeltaAccumulator` (який використовується нашим `CounterChecksumBenchmarkHandler`) ми застосували архітектурну хитрість — вкладений клас `ReusableValueList`:

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

Замість того, щоб будувати повний snapshot після кожного кроку злиття, акумулятор тримає словник накопичених значень, перевикористовуваний буфер `changedBuffer` та обгортку `ReusableValueList`. Ми скидаємо внутрішній покажчик (`Reset`), підставляючи актуальний масив і лічильник елементів. Це не безкоштовна підсистема: dictionary, snapshots і fallback-merger мають свою ціну. Але hot merge path перестає створювати новий список змінених значень на кожен commit, і саме це підтверджується performance gate-ами для handler delta/merge.

---

## 22.4. Протокол безпеки: Закриття за першої підозри (Fail-Closed, зупинка без прихованої неправди)

Координатор злиття — це не просто бухгалтер, який сліпо додає цифри. Це консервативний детектив. Якщо дельта не проходить контракт, координатор блокує подальше злиття для цього handler-а й повертає fail-closed результат у pipeline.

У коді `CompleteForCommitCore` передбачено кілька рівнів перевірки:
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

## 🔍 Матеріали справи (Investigation Case Files)

### 1. Вердикт детективів (Decision Trace & Rationale)
Створення контракту delta/merge (дельта/злиття) для паралельного обчислення та швидкого злиття дельт аналітики (Віхи `024`-`025`). Mergeable handler-и реалізують `IRadarProcessingHandlerDeltaMerger`; оптимізовані handler-и можуть додати `IRadarProcessingHandlerDeltaAccumulatorFactory`. Це дозволило обробникам вираховувати свої зміни паралельно в різних потоках і зливати їх в один фінальний стан під час впорядкованої фіксації (Ordered Commit) без переходу на повільний послідовний резервний шлях (sequential fallback).

#### Чому швидкий handler має довести, що він mergeable
Перший безпечний варіант — усі custom handlers виконувати sequential, тоді порядок очевидний і merge не потрібен. Але це зводить нанівець parallel runtime саме там, де аналітика стає важкою. Другий варіант — дозволити handler-ам мутувати shared state паралельно, але це повторює кризу спільного стану. Delta/merge contract став пропуском у швидкий коридор: handler рахує власну дельту паралельно, а commit зливає її за sequence. Ціна вибору — не кожен handler автоматично mergeable; виграш — швидкість доступна лише тим розширенням, які довели свою детермінованість.

### 2. Закони фізики рантайму (System Invariants)
* **Асоціативність злиття**: Результат злиття дельт обробників має бути детермінованим і не залежати від черговості завершення потоків.
* **Flat elapsed gate**: Для captured full-cache handler delta/merge `active=4` має лишатися близьким до `active=1` за elapsed time; allocation overhead фіксується окремо й не маскується.

### 3. Патологоанатомічний звіт (Failure Modes & Recovery)
* **Непідтримуване злиття**: Snapshot-only handler примушує sequential fallback, а unsupported handler set блокує MVP processing до старту. Mergeable handler без потрібного merger contract не проходить ordered handler-delta шлях.

### 4. Слід доказової бази (Implementation & Tests)
* Контракт злиття: [IRadarProcessingHandlerDeltaMerger.cs](../../../src/Domain/Processing/Handlers/Contracts/IRadarProcessingHandlerDeltaMerger.cs)
* Тести координатора злиття: [RadarProcessingHandlerDeltaMergeCoordinatorTests.cs](../../../tests/RadarPulse.Tests/Processing/Handlers/RadarProcessingHandlerDeltaMergeCoordinatorTests.cs)
* Runtime-тести handler delta/merge: [RadarProcessingMvpHandlerDeltaRuntimeTests](../../../tests/RadarPulse.Tests/Processing/Handlers/RadarProcessingMvpHandlerDeltaRuntimeTests)
* Performance gate: [RadarProcessingHandlerDeltaPerformanceGateTests](../../../tests/RadarPulse.Tests/Processing/Handlers/RadarProcessingHandlerDeltaPerformanceGateTests)

### 5. Протокол допиту процесу (Verification Commands)
Запуск перевірки швидкості дельта-злиття для важких обробників:
```bash
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~HandlerDelta"
```

# Розділ 22: Контракт злиття дельт (Delta/Merge Contract)

Коли слідча група працює над великою справою, розслідування можна прискорити, якщо відправити детективів опитувати свідків паралельно. Детектив А розпитує свідка у північній частині міста, детектив Б — у південній. Вони не повинні телефонувати один одному після кожного почутого слова, щоб узгодити загальну картину. Кожен із них записує свої знахідки у блокнот — створює локальний звіт про зміни, або **дельту (Delta)**. Проте, коли настає час підшивати ці матеріали до кримінальної справи, всі звіти мають бути об'єднані провідним слідчим у строгому хронологічному порядку. Якщо підшити звіт про арешт підозрюваного раніше, ніж звіт про вчинення злочину, суддя розірве справу на шматки.

У попередньому розділі ми побачили, як система **RadarPulse** підключає аналітичні розширення (Custom Handlers). Але тут криється серйозний виклик: якщо кожен воркер після обробки свого пакета подій (`RadarEventBatch`) намагатиметься оновити спільний стан аналітика безпосередньо, ми отримаємо хаос і гонку за ресурсами. Якщо ж змусити воркерів чекати своєї черги для послідовного застосування обробників (що називається `sequential fallback`), вся користь від багатопоточності зведеться нанівець.

Для вирішення цієї проблеми у віхах `024` та `025` ми реалізували архітектурний паттерн **Delta/Merge Contract** (концептуально відомий як `IMergeableHandler`). Він дозволяє аналітикам обчислювати зміни паралельно, а Координатору — зливати їх послідовно та детерміновано.

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

Цей алгоритм працює як конвеєр: як тільки «повільний» батч з низьким sequence-номером нарешті прибуває, координатор миттєво «проштовхує» крізь цикл злиття всі наступні батчі, які вже чекали в черзі.

---

## 22.3. Оптимізація пам'яті: Патрон в обоймі (`ReusableValueList`)

У високопродуктивних системах обробки великих даних Garbage Collector — це найлютіший ворог. Якщо на кожен комміт батча створювати нові масиви для списку змінених значень, GC швидко зупинить систему для збору сміття. Це рівносильно тому, якби детектив викидав свій блокнот і купував новий після кожного записаного речення.

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

Замість того, щоб виділяти новий екземпляр списку при кожному кроці злиття, акумулятор використовує один і той самий перевикористовуваний буфер `changedBuffer` та обгортку `ReusableValueList`. Ми просто скидаємо внутрішній покажчик (`Reset`), підставляючи новий масив та лічильник елементів. Для зовнішніх клієнтів це виглядає як новий список `IReadOnlyList`, але з погляду пам'яті — це нуль алокацій у купі.

---

## 22.4. Протокол безпеки: Закриття за першої підозри (Fail-Closed)

Координатор злиття — це не просто бухгалтер, який сліпо додає цифри. Це консервативний детектив. Якщо виникає хоча б найменша підозра на фальсифікацію доказів, робота конвеєра повністю зупиняється.

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

У разі блокування, властивість `permanentBlockingSequence` фіксує номер «зламаного» батча, а система переходить у режим **Fail-Closed**. Доки адміністратор чи оператор не розбереться з інцидентом (або не перезапустить рантайм через скидання історії), жодне подальше злиття не відбудеться. Це гарантує, що неточні чи скомпрометовані аналітичні метрики ніколи не потраплять до фінального звіту.
---

## 🔍 Матеріали справи (Investigation Case Files)

### 1. Вердикт детективів (Decision Trace & Rationale)
Створення інтерфейсу `IMergeableHandler` для паралельного обчислення та швидкого злиття дельт аналітики (Віхи `024`-`025`). Це дозволило обробникам вираховувати свої зміни паралельно в різних потоках і зливати їх в один фінальний стан під час Ordered Commit без переходу на повільний sequential fallback.

### 2. Закони фізики рантайму (System Invariants)
* **Асоціативність злиття**: Результат злиття дельт обробників має бути детермінованим і не залежати від черговості завершення потоків.
* **Flat Latency**: Час виконання обробників на конвеєрі `active=4` не повинен перевищувати час на `active=1`.

### 3. Патологоанатомічний звіт (Failure Modes & Recovery)
* **Непідтримуване злиття**: Якщо обробник не реалізує `IMergeableHandler`, система тимчасово відкочується до послідовної обробки (Sequential Fallback), що знижує загальну пропускну здатність.

### 4. Слід доказової бази (Implementation & Tests)
* Контракт злиття: [IMergeableHandler.cs](../../../src/Domain/Processing/Handlers/Contracts/IRadarProcessingHandlerDeltaMerger.cs)
* Тести дельта-злиття: [RadarProcessingPersistentDurableHandlerDeltaTests.cs](../../../tests/RadarPulse.Tests/Processing/Durable/RadarProcessingPersistentDurableHandlerDeltaTests.cs)

### 5. Протокол допиту процесу (Verification Commands)
Запуск перевірки швидкості дельта-злиття для важких обробників:
```bash
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~PersistentDurableHandlerDeltaTests"
```

# Розділ 15: Старшина черги (`OrderedResultCoordinator`)

Щоб приборкати розбурхану стихію паралельних потоків і гарантувати, що результати обробки радарних батчів потраплять до загального реєстру строго один за одним, нам потрібен суворий регулювальник. В архітектурі RadarPulse (починаючи з віхи `021`) цю роль виконує координатор впорядкованих результатів — [RadarProcessingOrderedResultCoordinator.cs](../../../src/Infrastructure/Processing/Async/Services/RadarProcessingOrderedResultCoordinator.cs).

У нашій детективній історії ми називаємо його «Старшиною черги». Його характер непохитний, його блокнот бездоганний, а логіка роботи нагадує надійний механічний сейф із кількома секціями.

---

## 15.1. Патерн «Тимчасовий буферний сейф»

Старшина черги діє за простим, але безкомпромісним алгоритмом. Його головна мета — перетворити хаотичний потік завершення паралельних завдань на строго упорядковану лінійну послідовність результатів.

Уявімо роботу Координатора крок за кроком:

1. **Прийом рапорту:**
   Коли будь-який воркер завершує роботу над батчем, він приносить результат Координатору, викликаючи метод `Complete`. Наприклад, спритний воркер, який обробляв батч №4 з чистим небом, приходить першим.
2. **Перевірка порядкового номера:**
   Координатор дивиться на порядковий номер (`Sequence`) принесеного звіту. Він порівнює його з внутрішнім лічильником `nextPublishSequence`, який вказує на номер наступного батча, що очікує на публікацію. Зараз очікується батч №2.
   * Якщо принесений номер менший за очікуваний (наприклад, воркер приніс батч №1, який уже був записаний раніше), Координатор негайно б'є тривогу та викидає виняток `InvalidOperationException` — це ознака серйозного збою логіки викликів.
   * Якщо принесений номер більший за очікуваний (наша ситуація з батчем №4), Старшина каже: *«Чудова робота, але твій час ще не настав»*. Він забирає результат і кладе його в тимчасовий сортований буфер — словник `pending`, де ключ — це номер послідовності, а значення — сам результат.
3. **Очікування відстаючих:**
   Буфер `pending` працює як сейф тимчасового зберігання. Воркер №4 звільняється і може брати нове завдання. Звіт №4 лежить у сейфі. Згодом завершується робота над батчем №3. Оскільки ми все ще чекаємо на №2, звіт №3 також вирушає до сейфу.
4. **Дренування сейфу (Draining):**
   Нарешті, повільний воркер завершує обчислення грозового шторму в батчі №2 і приносить його Координатору.
   Ось тут і починається магія! Номер принесеного батча збігається з `nextPublishSequence` (дорівнює 2). Координатор негайно публікує результат №2.
   Після цього він не зупиняється. Він пересуває свій маркер `nextPublishSequence` на крок вперед (тепер очікується 3) і заглядає у свій словник `pending`. Там уже лежить готовий звіт №3! Координатор витягує його, публікує і знову збільшує очікуваний номер на 1.
   Він бачить у сейфі звіт №4, публікує його, пересуває покажчик на 5. Наступного елемента в словнику немає, тому Координатор зупиняє дренування і переходить у режим очікування батча №5.

Завдяки цьому ми отримуємо ідеальне поєднання: воркери працюють паралельно без простоїв на максимальній швидкості процесора, але результати стають видимими для зовнішнього світу строго в порядку надходження вхідних даних.

---

## 15.2. Реалізація Старшини черги на C#

Давайте поглянемо на технічну реалізацію цього механізму. Нижче наведено вихідний код нашого координатора результатів:

```csharp
using System;
using System.Collections.Generic;
using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Координує позачергове завершення батчів у послідовний порядок публікації.
/// </summary>
public sealed class RadarProcessingOrderedResultCoordinator
{
    private readonly object sync = new();
    private readonly SortedDictionary<long, RadarProcessingQueuedBatchProcessingResult> pending = [];
    private long nextPublishSequence;
    private bool terminalFailurePublished;

    /// <summary>
    /// Наступний порядковий номер провайдера, необхідний для публікації.
    /// </summary>
    public long NextPublishSequence
    {
        get
        {
            lock (sync)
            {
                return nextPublishSequence;
            }
        }
    }

    /// <summary>
    /// Кількість завершених результатів, що чекають у буфері.
    /// </summary>
    public int PendingCount
    {
        get
        {
            lock (sync)
            {
                return pending.Count;
            }
        }
    }

    /// <summary>
    /// Додає завершений результат і повертає всі готові до публікації результати в порядку черги.
    /// </summary>
    public IReadOnlyList<RadarProcessingQueuedBatchProcessingResult> Complete(
        RadarProcessingQueuedBatchProcessingResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        lock (sync)
        {
            var sequence = result.Sequence.Value;
            if (sequence < nextPublishSequence)
            {
                throw new InvalidOperationException(
                    $"Послідовність обробки {sequence} вже була опублікована.");
            }

            if (!pending.TryAdd(sequence, result))
            {
                throw new InvalidOperationException(
                    $"Послідовність обробки {sequence} вже завершена.");
            }

            return PublishAvailableUnsafe();
        }
    }

    private IReadOnlyList<RadarProcessingQueuedBatchProcessingResult> PublishAvailableUnsafe()
    {
        List<RadarProcessingQueuedBatchProcessingResult>? published = null;
        while (pending.TryGetValue(nextPublishSequence, out var result))
        {
            // Якщо опубліковано критичну помилку, ми не можемо публікувати успішні результати далі.
            if (!CanPublishAfterTerminalFailure(result))
            {
                break;
            }

            pending.Remove(nextPublishSequence);
            nextPublishSequence++;
            published ??= [];
            published.Add(result);

            if (IsTerminalFailure(result.Status))
            {
                terminalFailurePublished = true;
            }
        }

        return published is null
            ? Array.Empty<RadarProcessingQueuedBatchProcessingResult>()
            : Array.AsReadOnly(published.ToArray());
    }

    private bool CanPublishAfterTerminalFailure(
        RadarProcessingQueuedBatchProcessingResult result) =>
        !terminalFailurePublished ||
        result.Status is RadarProcessingQueuedBatchProcessingStatus.Canceled or
            RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault;

    private static bool IsTerminalFailure(
        RadarProcessingQueuedBatchProcessingStatus status) =>
        status is RadarProcessingQueuedBatchProcessingStatus.FailedProcessing or
            RadarProcessingQueuedBatchProcessingStatus.FailedValidation or
            RadarProcessingQueuedBatchProcessingStatus.FailedMigration;
}
```

---

## 15.3. Межа витривалості: Обробка термінальних збоїв

Детективний аналіз коду Координатора відкриває ще одну важливу деталь — методи `CanPublishAfterTerminalFailure` та `IsTerminalFailure`. Що відбувається, коли один із батчів зазнає критичного збою (наприклад, не проходить валідацію контрольної суми або завершується з винятком процесу)?

Якщо батч №2 ламається, він публікується як термінальна помилка (`terminalFailurePublished = true`). З цього моменту система переходить у стан аварійної зупинки. Усі наступні успішні батчі, які воркери встигли обчислити паралельно (наприклад, №3 та №4), не можуть бути зафіксовані в ядрі. Вони блокуються.

Проте, система повинна залишити чіткі сліди для діагностики. Координатор дозволяє опублікувати результати зі статусом `Canceled` або `SkippedAfterFault`, щоб закрити сесію обробки без пропусків у журналах аудиту.

Завдяки Старшині черги ми отримали надійний заслін від хаосу паралелізму, гарантувавши повну хронологічну послідовність на виході системи.
---

## 🔍 Матеріали справи (Investigation Case Files)

### 1. Вердикт детективів (Decision Trace & Rationale)
Впровадження координатора впорядкованих результатів `RadarProcessingOrderedResultCoordinator` (Віха `021`). Він виконує роль тимчасового буфера-сейфа: результати воркерів накопичуються в ньому і випускаються у світ строго за збільшенням номера `Provider Sequence`.

### 2. Закони фізики рантайму (System Invariants)
* **Порядок провайдера**: Жоден батч не може бути закомічений або відправлений споживачам, якщо не закомічено всі попередні за номером батчі.
* **Межа активності**: Максимальна кількість одночасно оброблюваних батчів обмежена (`OrderedActiveBatchCapacity = 4`).

### 3. Патологоанатомічний звіт (Failure Modes & Recovery)
* **Head-of-Line Blocking**: Якщо один батч падає або затримується, координатор призупиняє випуск усіх наступних батчів, запобігаючи пошкодженню хронології.

### 4. Слід доказової бази (Implementation & Tests)
* Координатор результатів: [RadarProcessingOrderedResultCoordinator.cs](../../../src/Infrastructure/Processing/Async/Services/RadarProcessingOrderedResultCoordinator.cs)
* Тести координатора: [RadarProcessingOrderedResultCoordinatorTests.cs](../../../tests/RadarPulse.Tests/Processing/Async/RadarProcessingOrderedResultCoordinatorTests.cs)

### 5. Протокол допиту процесу (Verification Commands)
Запуск тестів впорядкування за маркером послідовності:
```bash
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~RadarProcessingOrderedResultCoordinatorTests"
```

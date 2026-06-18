# Розділ 15: Старшина черги: контур упорядкованої публікації

Паралельні воркери не завершують батчі в порядку [`Provider Sequence`](../../../../src/Domain/Processing/Queueing/Models/RadarProcessingQueuedBatchSequence.cs). Один швидко рахує легкий пакет, інший довше тримає важкий штормовий батч, а зовнішній світ усе одно має побачити результати строго один за одним. В архітектурі RadarPulse (починаючи з віхи `021`) цю межу тримає контур впорядкованої публікації: production-шлях у [`DrainOrderedConcurrentAsync`](../../../../src/Infrastructure/Processing/Queueing/Services/RadarProcessingQueuedProcessingSession/RadarProcessingQueuedProcessingSession.DrainOrderedConcurrent.cs) збирає завершення воркерів, а [`PublishReadyOrderedCompletions`](../../../../src/Infrastructure/Processing/Queueing/Services/RadarProcessingQueuedProcessingSession/RadarProcessingQueuedProcessingSession.OrderedConcurrent.Publish.cs) випускає тільки наступний очікуваний номер.

Окремий [`RadarProcessingOrderedResultCoordinator`](../../../../src/Infrastructure/Processing/Async/Services/RadarProcessingOrderedResultCoordinator.cs) фіксує той самий інваріант у компактній тестованій формі. Це не окрема production-гілка, а кодова модель правила: приймати завершення в будь-якому порядку, але публікувати їх тільки за хронологією провайдера.

У детективній мові книги це «Старшина черги»: не герой сцени, а дисципліна, яка не дозволяє швидкому воркеру переписати хронологію.

---

## 15.1. Патерн «Тимчасовий буферний сейф»

Старшина черги діє за простим, але безкомпромісним алгоритмом. Його головна мета — перетворити хаотичний потік завершення паралельних завдань на строго упорядковану лінійну послідовність результатів.

Уявімо роботу цього контуру крок за кроком:

1. **Прийом рапорту:**
   Коли будь-який воркер завершує роботу над батчем, production-дренаж переносить його completion із `active` у тимчасовий список `completed`. У компактній моделі цього самого правила такий рапорт виглядає як виклик [`Complete`](../../../../src/Infrastructure/Processing/Async/Services/RadarProcessingOrderedResultCoordinator.cs). Наприклад, спритний воркер, який обробляв батч №4 з чистим небом, приходить першим.
2. **Перевірка порядкового номера:**
   Координатор дивиться на порядковий номер (`Sequence`) принесеного звіту. Він порівнює його з внутрішнім лічильником `nextPublishSequence`, який вказує на номер наступного батча, що очікує на публікацію. Зараз очікується батч №2.
   * Якщо принесений номер менший за очікуваний (наприклад, воркер приніс батч №1, який уже був записаний раніше), це ознака серйозного збою логіки викликів. У компактному coordinator-класі такий випадок одразу завершується `InvalidOperationException`.
   * Якщо принесений номер більший за очікуваний (наша ситуація з батчем №4), Старшина каже: *«Чудова робота, але твій час ще не настав»*. Він забирає результат і кладе його в тимчасовий буфер: у production-шляху це `completed`, у сфокусованому coordinator-класі — словник `pending`, де ключ — це номер послідовності, а значення — сам результат.
3. **Очікування відстаючих:**
   Тимчасовий буфер працює як сейф зберігання. Воркер №4 звільняється і може брати нове завдання. Звіт №4 лежить у сейфі. Згодом завершується робота над батчем №3. Оскільки ми все ще чекаємо на №2, звіт №3 також вирушає до сейфу.
4. **Дренування сейфу (Draining):**
   Нарешті, повільний воркер завершує обчислення грозового шторму в батчі №2 і приносить його Координатору.
   Тут спрацьовує головне правило. Номер принесеного батча збігається з `nextPublishSequence` (дорівнює 2), тому Координатор публікує результат №2.
   Після цього він не зупиняється. Він пересуває свій маркер `nextPublishSequence` на крок вперед (тепер очікується 3) і заглядає у свій буфер завершень. Там уже лежить готовий звіт №3! Координатор витягує його, публікує і знову збільшує очікуваний номер на 1.
   Він бачить у сейфі звіт №4, публікує його, пересуває покажчик на 5. Наступного елемента в буфері немає, тому Координатор зупиняє дренування і переходить у режим очікування батча №5.

Завдяки цьому воркери можуть працювати паралельно, а результати стають видимими для зовнішнього світу строго в порядку надходження вхідних даних.

---

## 15.2. Реалізація Старшини черги на C#

У кодовій базі цей механізм має дві форми. Production-шлях у [`RadarProcessingQueuedProcessingSession`](../../../../src/Infrastructure/Processing/Queueing/Services/RadarProcessingQueuedProcessingSession/RadarProcessingQueuedProcessingSession.DrainOrderedConcurrent.cs) тримає список активних задач `active`, список завершених, але ще не опублікованих результатів `completed`, і маркер `nextPublishSequence`. Коли `Task.WhenAny` повертає завершену роботу, вона переходить у `completed`, а [`PublishReadyOrderedCompletions`](../../../../src/Infrastructure/Processing/Queueing/Services/RadarProcessingQueuedProcessingSession/RadarProcessingQueuedProcessingSession.OrderedConcurrent.Publish.cs) шукає рівно той sequence, який можна зафіксувати наступним.

Для ізоляції самого правила існує компактний [`RadarProcessingOrderedResultCoordinator`](../../../../src/Infrastructure/Processing/Async/Services/RadarProcessingOrderedResultCoordinator.cs). Він не описує весь production-дренаж, але дає маленький, добре тестований фрагмент інваріанта: додати завершення, відхилити дубль, зберегти out-of-order результат і видати готовий contiguous range. Нижче наведено скорочений адаптований фрагмент цієї моделі:

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

Цей фрагмент показує нормальну дисципліну публікації: прийняти завершення, покласти його в буфер і випустити тільки тоді, коли настав його номер. Але реальний координатор має витримувати не лише успішні фініші. Найскладніший випадок починається тоді, коли один елемент послідовності ламається, а наступні вже встигли завершитися.

---

## 15.3. Межа витривалості: Обробка термінальних збоїв

Детективний аналіз ordered publication відкриває ще одну важливу деталь: порядок треба зберегти навіть тоді, коли один із батчів зазнає критичного збою (наприклад, не проходить валідацію контрольної суми або завершується з винятком процесу). У production-шляху це видно в [`PublishReadyOrderedCompletions`](../../../../src/Infrastructure/Processing/Queueing/Services/RadarProcessingQueuedProcessingSession/RadarProcessingQueuedProcessingSession.OrderedConcurrent.Publish.cs): після failed status сесія викликає `MarkFaulted`, скасовує активні роботи через `activeCancellation` і далі фіксує наступні записи як `SkippedAfterFault`, якщо вони потрібні для закриття журналу. У компактній моделі те саме правило зібране в методах [`CanPublishAfterTerminalFailure`](../../../../src/Infrastructure/Processing/Async/Services/RadarProcessingOrderedResultCoordinator.cs) та [`IsTerminalFailure`](../../../../src/Infrastructure/Processing/Async/Services/RadarProcessingOrderedResultCoordinator.cs).

Якщо батч №2 ламається, production-сесія фіксує faulted state і скасовує активні роботи; у компактному coordinator-класі цьому відповідає `terminalFailurePublished = true`. З цього моменту система переходить у стан аварійної зупинки. Усі наступні успішні батчі, які воркери встигли обчислити паралельно (наприклад, №3 та №4), не можуть бути зафіксовані в ядрі. Вони блокуються.

Проте, система повинна залишити чіткі сліди для діагностики. Координатор дозволяє опублікувати результати зі статусом `Canceled` або `SkippedAfterFault`, щоб закрити сесію обробки без пропусків у журналах аудиту.

---

## 15.4. Математика дренування: Буфер завершень та ціна синхронізації

Production-шлях реалізує сейф максимально просто: `completed` — це обмежений список завершень, а [`FindCompletionIndex`](../../../../src/Infrastructure/Processing/Queueing/Services/RadarProcessingQueuedProcessingSession/RadarProcessingQueuedProcessingSession.State.cs) лінійно шукає елемент із поточним `nextPublishSequence`. На папері це `O(N)`, але тут `N` обмежене `OrderedActiveBatchCapacity`, тобто baseline-значенням `4`. Для такого малого буфера лінійний пошук дешевший і прозоріший за складнішу структуру даних.

У сфокусованому [`RadarProcessingOrderedResultCoordinator`](../../../../src/Infrastructure/Processing/Async/Services/RadarProcessingOrderedResultCoordinator.cs) той самий інваріант показаний через `SortedDictionary<long, RadarProcessingQueuedBatchProcessingResult>`. Це корисно як компактна модель:
1. **Сортований порядок злиття:** Словник підтримує внутрішній бінарний деревовидний порядок (Red-Black Tree), що дає пошук та видалення за `O(log N)`. Оскільки ліміт активних batch-ів у системі обмежений, розмір словника малий, а практична ціна операції залишається контрольованою.
2. **Коротка критична секція:** Якщо [`Complete`](../../../../src/Infrastructure/Processing/Async/Services/RadarProcessingOrderedResultCoordinator.cs) викликають з кількох потоків, синхронізація обмежується входом у `lock (sync)` для додавання результату до словника та перевірки очікуваного батча. Обчислювальна робота воркерів та важкий дисковий ввід-вивід відбуваються **поза межами цього локу**.

Чому в компактному coordinator-класі достатньо саме `lock (sync)`, а не потрібен складніший локер? Він захищає дуже малий, але зв'язаний стан: `pending`, `nextPublishSequence` і `terminalFailurePublished`. Ці поля не можна оновлювати окремо, бо правильність живе не в одній операції словника, а в цілій транзакції: додати completion, перевірити дублікати, опублікувати всі consecutive ready результати, зсунути `nextPublishSequence` і застосувати terminal-failure rule.

`ConcurrentDictionary` дав би потокобезпечні операції над словником, але не дав би атомарності для всієї цієї послідовності. `ReaderWriterLockSlim` теж не додає практичного виграшу, бо гарячий шлях [`Complete`](../../../../src/Infrastructure/Processing/Async/Services/RadarProcessingOrderedResultCoordinator.cs) є write path, а read-властивості короткі. `SemaphoreSlim` потрібен для async wait, але всередині критичної секції немає `await` і не має бути важкої роботи.

Тому використано приватний monitor lock: `private readonly object sync = new();`. Це не `lock (this)`: зовнішній код не може залочити цей об'єкт і випадково зупинити coordinator. Усередині lock-а залишаються тільки `TryAdd`, `TryGetValue`, `Remove`, зміна `nextPublishSequence` і `terminalFailurePublished`; compute, I/O, worker scheduling і commit work виконуються поза ним. За обмеженого `OrderedActiveBatchCapacity` конкуренція за цей lock залишається малою і передбачуваною.

Тут важливо не сплутати кілька різних “ємностей”. `OrderedActiveBatchCapacity` — це не розмір provider queue і не кількість worker threads. Це межа ordered-concurrent drain: скільки batch-ів може одночасно перебувати в активному compute до того, як ordered publication наздожене їх. У коді ця межа оформлена як [`RadarProcessingOrderedConcurrencyOptions.ActiveBatchCapacity`](../../../../src/Infrastructure/Processing/Async/Options/RadarProcessingOrderedConcurrencyOptions.cs); значення `1` фактично повертає систему до sequential processing, а baseline-значення `4` дозволяє кільком batch-ам рахуватися паралельно без неконтрольованого накопичення ранніх результатів.

Це обмеження захищає одразу три речі: розмір тимчасового буфера завершень, кількість retained resources, які можуть одночасно чекати commit, і масштаб head-of-line blocking, якщо ранній batch затримався. Збільшити ліміт можна, але це вже не “безкоштовна паралельність”: зростають пам'ять, prewarm footprint і кількість результатів, які ordered publication змушений тимчасово тримати до появи найстаршого відсутнього sequence.

**Red-Black Tree** — це самобалансоване бінарне дерево пошуку. У звичайному бінарному дереві невдалий порядок вставок може перетворити дерево майже на довгий список: пошук стає повільним, бо доводиться проходити багато вузлів підряд. Red-Black Tree після вставок і видалень робить невеликі перебудови гілок і підтримує дерево приблизно збалансованим. Для нас це означає просту практичну річ: якщо компактний coordinator отримує результати в дивному порядку — №4, №3, №2, №5 — `pending` не деградує в хаотичний список, а зберігає передбачувану ціну `TryAdd`, `TryGetValue` і `Remove`.

У цій системі Red-Black Tree не є “магією продуктивності”. При `OrderedActiveBatchCapacity = 4` буфер майже завжди дуже малий. Його користь у компактній моделі в іншому: структура даних сама тримає ключі відсортованими за `Provider Sequence`, тому coordinator може швидко перевірити: “чи вже є саме той номер, який я маю опублікувати наступним?”

Завдяки Старшині черги хаос завершення воркерів не просочується у фінальний стан: назовні виходить тільки впорядкована послідовність.

Тепер, коли ми забезпечили впорядкування результатів, перед нами постає питання: як саме ці результати фіксуються у стані системи і чому наша перша спроба зробити це паралельно ледь не зруйнувала математичну цілісність ядра? Про це — у наступному розділі.
---

## Матеріали справи

### 1. Вердикт детективів
Впровадження ordered publication contour (Віха `021`). Production-шлях у [`DrainOrderedConcurrentAsync`](../../../../src/Infrastructure/Processing/Queueing/Services/RadarProcessingQueuedProcessingSession/RadarProcessingQueuedProcessingSession.DrainOrderedConcurrent.cs) виконує роль тимчасового буфера-сейфа: результати воркерів накопичуються в `completed` і випускаються у світ строго за збільшенням номера [`Provider Sequence`](../../../../src/Domain/Processing/Queueing/Models/RadarProcessingQueuedBatchSequence.cs). [`RadarProcessingOrderedResultCoordinator`](../../../../src/Infrastructure/Processing/Async/Services/RadarProcessingOrderedResultCoordinator.cs) тримає той самий інваріант у компактній тестованій формі.

#### Чому порядок повертає окремий контур
Для впорядкування можна було поставити глобальний lock навколо всього commit, але тоді паралельність закінчувалася б саме там, де мала приносити користь. Можна було сортувати всі результати наприкінці, але live-процесинг не може чекати завершення всього архіву. Ordered publication став сейфом між цими крайнощами: приймає результати в будь-якому порядку, а випускає тільки наступний sequence. Ціна вибору — тимчасове утримання out-of-order результатів; виграш — streaming pipeline не втрачає часової правди.

### 2. Закони фізики рантайму
* **Порядок провайдера**: Жоден батч не може бути закомічений або відправлений споживачам, якщо не закомічено всі попередні за номером батчі.
* **Межа активності**: Максимальна кількість batch-ів, які можуть одночасно рахуватися попереду ordered publication, обмежена (`OrderedActiveBatchCapacity = 4`).

### 3. Патологоанатомічний звіт
* **Head-of-Line Blocking**: Якщо один батч падає або затримується, координатор призупиняє випуск усіх наступних батчів, запобігаючи пошкодженню хронології.

### 4. Слід доказової бази
* Production ordered drain: [RadarProcessingQueuedProcessingSession.DrainOrderedConcurrent.cs](../../../../src/Infrastructure/Processing/Queueing/Services/RadarProcessingQueuedProcessingSession/RadarProcessingQueuedProcessingSession.DrainOrderedConcurrent.cs)
* Production ordered publication: [RadarProcessingQueuedProcessingSession.OrderedConcurrent.Publish.cs](../../../../src/Infrastructure/Processing/Queueing/Services/RadarProcessingQueuedProcessingSession/RadarProcessingQueuedProcessingSession.OrderedConcurrent.Publish.cs)
* Компактна модель coordinator-а: [RadarProcessingOrderedResultCoordinator.cs](../../../../src/Infrastructure/Processing/Async/Services/RadarProcessingOrderedResultCoordinator.cs)
* Тести координатора: [RadarProcessingOrderedResultCoordinatorTests.cs](../../../../tests/RadarPulse.Tests/Processing/Async/RadarProcessingOrderedResultCoordinatorTests.cs)
* Тести production ordered drain: [RadarProcessingQueuedProcessingSessionOrderedConcurrentTests.cs](../../../../tests/RadarPulse.Tests/Processing/Queueing/RadarProcessingQueuedProcessingSessionOrderedConcurrentTests.cs)

### 5. Протокол допиту процесу
Запуск тестів впорядкування за маркером послідовності:
```bash
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~RadarProcessingOrderedResultCoordinatorTests|FullyQualifiedName~RadarProcessingQueuedProcessingSessionOrderedConcurrentTests"
```

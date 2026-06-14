# Розділ 3: Контракт радарного пакета даних

Уявіть, що ви ведете масштабне розслідування. Злочинна мережа діє по всій країні, і вам щосекунди надходять тисячі дрібних звітів від оперативників: номери машин, імена підозрюваних, адреси забігайлівок. Якщо ви під кожну таку записку будете заводити окрему велику картонну папку-справу з гербом міністерства, купувати під неї окремий сейф та виділяти окремого кур'єра для перенесення між кабінетами — ваш офіс захлинеться в бюрократичному папері за першу ж годину. Витрати на обслуговування процесу (оренда приміщень для сейфів, зарплата кур'єрам) перевищать користь від самого розслідування.

Саме це відбувається зі звичайною .NET-програмою, яка намагається наївно представляти гігабайти радарних точок у вигляді класичних об'єктів (класів) C#.

## 3.1. Битва за купу: Тінь збирача сміття (Garbage Collector)

Коли радар робить повне коло, він генерує мільйони одиничних відбитків сигналів. У кожного відбитка є метадані: час вимірювання, кут піднесення антени, азимут, відстань до хмари, частота сигналу та сила відбиття.

Якщо ми спробуємо спроектувати це як звичайний клас C#:

```csharp
public class RadarPoint
{
    public long Timestamp { get; set; }
    public double Azimuth { get; set; }
    // ... десятки інших властивостей ...
    public byte[] RawPayload { get; set; }
}
```

ми підпишемо системі смертний вирок.

У середовищі .NET кожен об'єкт, виділений у керованій купі (Heap), складається не лише з наших полів. Навколо даних є службова інформація рантайму:
1. **Object header / SyncBlock** — службовий заголовок об'єкта, який використовується для блокувань, хеш-кодів та внутрішніх потреб рантайму.
2. **TypeHandle / MethodTable pointer** — вказівник на опис типу, через який CLR знає, з яким класом має справу.
3. **Вирівнювання пам'яті** — об'єкти розкладаються в пам'яті з урахуванням машинних меж, тому між корисними даними можуть з'являтися порожні байти.

Точний розмір залежить від версії CLR, архітектури процесора та режиму виконання, але порядок проблеми не змінюється: навіть крихітний об'єкт із парою полів легко перетворюється на десятки байтів службового навантаження. Для NEXRAD це швидко стає неприйнятним масштабом, але тут важливо не змішувати два різні орієнтири. Перший — розмір реального radar volume: один такий файл уже може містити десятки мільйонів gate-moment значень, тобто окремих виміряних payload-значень у комірках променів радара. Другий — навантажувальний benchmark Віхи `004`: він не описував середній темп приходу даних із радара, а спеціально перевіряв, чи витримує доменний контракт обробку понад 500 мільйонів payload-значень за секунду.

Саме тому небезпечна не конкретна кругла цифра, а сама модель «кожне маленьке значення — окремий heap object». Якщо кожне таке значення або кожен маленький фрагмент метаданих перетворити на окремий об'єкт, ми отримаємо гігабайти «сміття», яке просто лежить у купі й чекає на збирача сміття (Garbage Collector, GC). Коли GC вирішить навести лад, він буде змушений зупинити роботу всієї програми (Stop-the-World pause), щоб перевірити зв'язки між мільйонами дрібних об'єктів. Процесор замре, обробка радарів зупиниться, і реальний шторм на карті оновиться з катастрофічним запізненням.

## 3.2. Рішення: Сержант [`RadarStreamEvent`](../../../src/Domain/Streaming/Streams/Models/RadarStreamEvent.cs) як Unmanaged Struct

Щоб виграти цю битву, під час Віхи `004` (Processing Core Input Contract) ми повністю змінили правила гри. Ми відмовилися від класів і перейшли на некеровані (unmanaged) структури фіксованого розміру.

Знайомтеся з нашим «сержантом» — [`RadarStreamEvent`](../../../src/Domain/Streaming/Streams/Models/RadarStreamEvent.cs). Його розмір становить **рівно 64 байти**. Він не живе в купі як окремий об'єкт. Він лежить у щільних, безперервних масивах пам'яті, де одна подія слідує за іншою без жодних заголовків та метаінформації .NET.

Ось спрощена версія нашого C#-контракту:

```csharp
using System.Runtime.InteropServices;

namespace RadarPulse.Domain.Streaming;

[StructLayout(LayoutKind.Sequential, Size = SizeInBytes)]
public readonly struct RadarStreamEvent
{
    public const int SizeInBytes = 64;

    // Таймстампи для валідації хронології
    public readonly long VolumeTimestampUtcTicks;
    public readonly long MessageTimestampUtcTicks;

    // Ідентифікатори джерела
    public readonly int SourceId;
    public readonly int SourceRecord;
    public readonly int SourceMessage;
    public readonly int RadialSequence;

    // Головний трюк: відсутність вкладених масивів
    // Замість byte[] ми зберігаємо зміщення та довжину
    // в спільному буфері пакета подій (батча)
    public readonly int PayloadOffset;
    public readonly int PayloadLength;

    // Фізичні характеристики сигналу
    public readonly float Scale;
    public readonly float Offset;
    public readonly ushort RadarOrdinal;
    public readonly ushort MomentId;
    public readonly ushort ElevationSlot;
    public readonly ushort AzimuthBucket;
    public readonly ushort RangeBand;
    public readonly ushort GateStart;
    public readonly ushort GateCount;
    public readonly RadarStreamWordSize WordSize;       // 8 або 16 біт
    public readonly RadarStreamStatusModel StatusModel; // статус
}
```

Зверніть увагу на головний архітектурний трюк. У структурі [`RadarStreamEvent`](../../../src/Domain/Streaming/Streams/Models/RadarStreamEvent.cs) немає поля `byte[] Payload`. Якби воно там було, це поле містило б посилання на інший об'єкт у купі, що знову повернуло б нас до проблеми алокацій.

Замість цього подія зберігає два простих цілих числа: `PayloadOffset` (зміщення) та `PayloadLength` (довжина). Вони вказують на конкретний сегмент в одному великому, спільному масиві байтів, який належить усьому пакету подій, або батчу. Це схоже на те, як детектив записує у блокнот: «Докази лежать у великій скрині на полиці №3, починаючи з 15-го сантиметра і довжиною 40 сантиметрів». Самі докази лежать в одному місці, а ми оперуємо лише легкими числовими координатами.

## 3.3. Шар Streaming: [`RadarEventBatch`](../../../src/Domain/Streaming/Batches/Models/RadarEventBatch.cs)

Ці 64-байтні структури групуються в єдину сутність — [`RadarEventBatch`](../../../src/Domain/Streaming/Batches/Models/RadarEventBatch.cs). Це немутабельний (immutable) пакет подій, або батч, який містить:
1. Масив метаданих подій: `ReadOnlyMemory<RadarStreamEvent> Events`.
2. Великий суцільний масив байтів: `ReadOnlyMemory<byte> Payload`.
3. Версії схем та каталогів ([`StreamSchemaVersion`](../../../src/Domain/Streaming/Streams/Models/StreamSchemaVersion.cs), [`DictionaryVersion`](../../../src/Domain/Streaming/Streams/Models/DictionaryVersion.cs), [`SourceUniverseVersion`](../../../src/Domain/Streaming/Sources/Models/SourceUniverseVersion.cs)) для підтвердження того, що батч інтерпретується за тими самими правилами, за якими був створений.

Ось як це виглядає концептуально:

```csharp
public sealed class RadarEventBatch
{
    public StreamSchemaVersion StreamSchemaVersion { get; }
    public DictionaryVersion DictionaryVersion { get; }
    public SourceUniverseVersion SourceUniverseVersion { get; }

    // Суцільний блок пам'яті з 64-байтними структурами
    public ReadOnlyMemory<RadarStreamEvent> Events { get; }

    // Суцільний блок пам'яті для корисного навантаження всіх подій
    public ReadOnlyMemory<byte> Payload { get; }

    public RadarEventBatchLifetime Lifetime { get; }

    // Валідація лінійних зміщень корисного навантаження подій
    private static void ValidatePayloadReferences(
        ReadOnlySpan<RadarStreamEvent> events,
        int payloadLength)
    {
        // Перевірка, що події посилаються тільки всередину масиву Payload
        // та не перекривають межі пам'яті
    }
}
```

## 3.4. Парсинг та пам'ять: Концептуальний Cast проти реального Builder

Коли ви стикаєтеся з необхідністю обробки 500 мільйонів payload-значень за секунду, першим імпульсом є максимальне спрощення десеріалізації. В ідеальному світі ми могли б скористатися концептуальним трюком **Zero-Copy Deserialization** (десеріалізація без копіювання), інтерпретуючи сирі розпаковані байти безпосередньо як масив некерованих 64-байтних структур [`RadarStreamEvent`](../../../src/Domain/Streaming/Streams/Models/RadarStreamEvent.cs):

```csharp
// Концептуальний ідеал (не використовується в реальній реалізації):
ReadOnlySpan<RadarStreamEvent> events = MemoryMarshal.Cast<byte, RadarStreamEvent>(rawDecompressedBytes);
```

Цей приклад виглядає неймовірно ефектно на папері: процесор отримує прямий доступ до пам'яті за одну інструкцію без жодного копіювання. Проте в суворому середовищі NEXRAD Level II цей концептуальний метод виявляється повністю непридатним.

Замість цього в реальному коді нашого проекту використовується **[`RadarEventBatchBuilder`](../../../src/Domain/Streaming/Batches/Services/RadarEventBatchBuilder/RadarEventBatchBuilder.cs)** з багаторазовими внутрішніми буферами, а **`ArrayPool`** підключається на сусідніх гарячих ділянках: декомпресія, retained payload, pooled-copy та дельти обробки. Давайте порівняємо ці підходи та розберемося, чому реальна архітектура виявилася значно надійнішою:

Endianness, або порядок байтів, — це домовленість про те, у якому напрямку записуються байти всередині числа, яке займає більше одного байта. Для одного `byte` проблеми немає. Вона з'являється для `ushort`, `int`, `float`, часових міток, зміщень та інших багатобайтових значень. Наприклад, число `300` як 32-бітне ціле у Big-Endian записується байтами `00 00 01 2C`: старший байт іде першим, як у звичайному записі числа. Little-Endian машина інтерпретує ті самі чотири байти у зворотному порядку — як `0x2C010000`, тобто зовсім інше число.

Для NEXRAD це критично, бо формат файлу зберігає числові поля у Big-Endian, а типові x86-64/ARM64 процесори, на яких працює .NET-код, читають числа як Little-Endian. `MemoryMarshal.Cast` не парсить і не виправляє порядок байтів: він просто накладає нашу структуру поверх сирої пам'яті. Тому реальний builder має явно читати big-endian поля, нормалізувати їх у процесорний порядок і тільки після цього складати доменний [`RadarStreamEvent`](../../../src/Domain/Streaming/Streams/Models/RadarStreamEvent.cs).

Якщо завтра код запуститься на іншому процесорі, цей висновок не зміниться. NEXRAD лишається Big-Endian форматом незалежно від заліза, а `BinaryPrimitives.Read*BigEndian` описує саме порядок байтів у файлі, не здогадку про поточний CPU. На Little-Endian машині .NET зробить перестановку байтів; на Big-Endian машині така операція фактично зведеться до прямого читання. У будь-якому разі доменний контракт отримує вже нормалізовані числа. Прямий `MemoryMarshal.Cast` лишається крихким навіть на гіпотетичному Big-Endian CPU, бо проблема не тільки в ендіанності: NEXRAD має зовнішній бінарний layout, змінні payload-діапазони та власні правила зміщень.

| Критерій порівняння | Концептуальний `MemoryMarshal.Cast` | Реальна реалізація: [`RadarEventBatchBuilder`](../../../src/Domain/Streaming/Batches/Services/RadarEventBatchBuilder/RadarEventBatchBuilder.cs) + retained ownership |
| :--- | :--- | :--- |
| **Порядок байтів (Endianness)** | **Провал.** NEXRAD Level II зберігає числа у форматі Big-Endian. Процесори x86-64/ARM64 працюють з Little-Endian. Прямий Cast прочитає перевернуті байти (сміття). | **Успіх.** Будівельник зчитує поля за допомогою `BinaryPrimitives.ReadInt32BigEndian` та виконує правильну конвертацію ендіанності на льоту. |
| **Змінна довжина та зміщення** | **Провал.** Прямий Cast вимагає однорідного масиву фіксованого розміру. У NEXRAD початкові зміщення (`PayloadOffset`) та довжина гейтів (`GateCount`) кожної поодинокої події відрізняються. | **Успіх.** Будівельник динамічно обчислює зміщення корисного навантаження (`payloadOffset`) для кожної події та копіює його у суміжний буфер. |
| **Алокації пам'яті (Heap Allocations)** | Нульові (просте приведення покажчиків), але тільки для ідеального однорідного формату. | На стабільному leased hot path builder повторно використовує власні `eventBuffer` та `payloadBuffer`, збільшуючи їх лише через `Array.Resize`, коли capacity замала. Довше володіння батчем переноситься в retained/pooled-copy шар, де масиви вже орендуються з `ArrayPool`. |
| **Валідація та цілісність** | Відсутня на етапі приведення типу. | Будівельник перевіряє межі масивів ([`EnsurePayloadCapacity`](../../../src/Domain/Streaming/Batches/Services/RadarEventBatchBuilder/RadarEventBatchBuilder.CapacityReset.cs)), валідує версії топології та підраховує контрольну суму корисного навантаження за один прохід. |
| **Архітектурна чистота** | Жорстке зв'язування з бінарним форматом файлу. | Повна ізоляція: будівельник конвертує складний зовнішній двійковий формат у чисті доменні структури. |

Реальний процес наповнення батча відбувається поетапно: парсер NEXRAD покроково зчитує бінарні записи, конвертує числа, конструює структуру [`RadarStreamEvent`](../../../src/Domain/Streaming/Streams/Models/RadarStreamEvent.cs) та записує її у внутрішній масив `eventBuffer`. Потім корисне навантаження (`payload`) копіюється у виділене місце в `payloadBuffer`. Якщо батч споживається одразу, `BuildLeased()` віддає посилання на ці самі буфери, а `ResetRetainingCapacity()` очищає лічильники без повторного виділення масивів. Якщо батч треба утримувати довше за callback, retained шар робить pooled-copy і вже там відповідає за оренду та повернення масивів.

Друга складова нашого успіху — **кеш-локальність (Cache Friendliness)**. Тут важливо говорити не про 64 біти, а про **64 байти**: саме такий фіксований розмір має [`RadarStreamEvent`](../../../src/Domain/Streaming/Streams/Models/RadarStreamEvent.cs). Процесор AMD Ryzen 9 9900X (Zen 5), на якому фіксувався benchmark-контур Віхи `004`, оперує кеш-лініями розміром 64 байти. Тому структура розміром 64 байти дає нам рівний cache-line stride: події лежать у пам'яті щільно, з однаковим кроком, без графа посилань і без per-event heap allocations.

Це performance-контракт, а не умова коректності. Якщо інший процесор має 128-байтну кеш-лінію, в одну лінію можуть потрапляти дві події; якщо кеш-лінія менша, одна подія може зачіпати більше однієї лінії. Якщо CLR або allocator не покладе перший елемент масиву рівно на межу cache line, окрема 64-байтна структура також може перетнути межу двох ліній. Але головний виграш лишається: послідовний масив фіксованих unmanaged-структур читається передбачувано, а не через розкидані об'єкти в купі. На новому класі процесорів це треба не приймати на віру, а повторно підтверджувати benchmark-ами.

Це конкретний прояв **mechanical sympathy** — підходу, у якому форма коду поважає реальну механіку заліза. Ми не змушуємо процесор блукати хаотичним графом об'єктів у купі; ми даємо йому щільний масив фіксованих структур, який добре підходить для послідовного читання пам'яті. Кеш-локальність тут не випадкова мікрооптимізація, а частина контракту між доменною моделлю і процесором.

Коли воркер починає читати метадані події `Events[i]`, апаратний завантажувач процесора (Hardware Prefetcher) автоматично підтягує наступні події `Events[i+1]` та `Events[i+2]` з оперативної пам'яті в надшвидкий кеш L1 та L2 заздалегідь. Процесор працює на максимальній частоті, не зупиняючи ядра для очікування повільної RAM.

Завдяки leased delivery, повторному використанню буферів, pooled-copy для довгоживучих батчів та кеш-локальності збирач сміття (Garbage Collector) перестав бути головним учасником гарячого шляху, а checksum/contract parity залишився предметом тестів і benchmark-гейтів.

Тепер, коли ми розібралися з фізичною оптимізацією даних у пам'яті, давайте подивимося на архітектурну мапу нашої системи. Як захистити доменні правила від бруду зовнішнього світу? Про це — у наступному розділі.
---

## 🔍 Матеріали справи (Investigation Case Files)

### 1. Вердикт детективів (Decision Trace & Rationale)
Впровадження некерованих (unmanaged, тобто таких, що не містять посилань на об'єкти в купі) 64-байтних структур [`RadarStreamEvent`](../../../src/Domain/Streaming/Streams/Models/RadarStreamEvent.cs) та об'єднання їх у [`RadarEventBatch`](../../../src/Domain/Streaming/Batches/Models/RadarEventBatch.cs) (Віхи `003`-`004`). Це прибрало per-event heap allocations — виділення пам'яті в керованій купі для кожної окремої події — із доменного контракту і дало benchmark-рівень, тобто виміряну в навантажувальному тесті пропускну здатність, понад 500 мільйонів payload-значень за секунду. У цьому контексті payload — це корисне навантаження радарного вимірювання, а близько `0.20` allocated bytes/payload value на cache-wide replay означає приблизно `0.20` байта нових алокацій на одне payload-значення під час прогону по всьому кешованому набору даних.

#### Чому сирі байти не стали контрактом домену
Ми могли залишити кожне радарне значення окремим об'єктом, але тоді головним учасником справи став би збирач сміття (Garbage Collector). Могли зробити прямий `MemoryMarshal.Cast` по розпакованих байтах, але NEXRAD має big-endian поля — числа, записані зі старшим байтом на початку, — змінні payload-діапазони корисного навантаження й зовнішню бінарну форму, яку не можна пускати просто в домен. Обраний шлях — компактний [`RadarStreamEvent`](../../../src/Domain/Streaming/Streams/Models/RadarStreamEvent.cs) плюс [`RadarEventBatchBuilder`](../../../src/Domain/Streaming/Batches/Services/RadarEventBatchBuilder/RadarEventBatchBuilder.cs) — не найкоротший у коді, зате він переводить чужий формат у наш контрольований контракт. Ціна вибору — ручна нормалізація полів і власний builder, тобто збирач батча; виграш — cache-line stride, рівний крок читання даних кеш-лініями процесора, deterministic payload references, тобто передбачувані посилання на ділянки корисного навантаження через зміщення (`offset`) і довжину (`length`), і доказовий throughput — пропускна здатність, підтверджена benchmark-ами.

### 2. Закони фізики рантайму (System Invariants)
* **Алокаційний ліміт**: Довжина структури [`RadarStreamEvent`](../../../src/Domain/Streaming/Streams/Models/RadarStreamEvent.cs) має дорівнювати рівно 64 байтам, щоб зберігати фіксований крок читання подій (cache-line stride) і не повертати per-event heap allocations у гарячий шлях.
* **Валідація батча**: Кожен батч має містити суворі контрольні суми для перевірки цілісності радарного зліпку.

### 3. Патологоанатомічний звіт (Failure Modes & Recovery)
* **Порушення контрольної суми**: При невідповідності хешу батча вхідний конвеєр відкидає його та повертає помилку через [`RadarEventBatchValidationResult`](../../../src/Domain/Streaming/Batches/Models/RadarEventBatchValidationResult.cs) до системи моніторингу.

### 4. Докази продуктивності (Performance Evidence)

| Твердження (Claim) | Доказ (Evidence) | Команда верифікації (Verification Command) |
| :--- | :--- | :--- |
| [`RadarStreamEvent`](../../../src/Domain/Streaming/Streams/Models/RadarStreamEvent.cs) займає рівно одну 64-байтну cache line | Контрактний тест [`RadarStreamContractTests.RadarStreamEventUsesOneCacheLineStride`](../../../tests/RadarPulse.Tests/Streaming/Streams/RadarStreamContractTests.cs) | `dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~RadarStreamContractTests"` |
| Обробка 500M+ payload-значень/сек | Milestone 004 зафіксував `553_123_110.90` payload values/s на single-file benchmark і `509_716_417.97` payload values/s на cache-wide KTLX corpus | `dotnet run --no-build -c Release --project src/Presentation/RadarPulse.Cli/RadarPulse.Cli.csproj -- archive benchmark stream --cache data/nexrad --max-files 1000000 --iterations 1 --warmup-iterations 0 --parallelism 24 --decompressor radarpulse` |
| Алокації близько `0.20` байта/payload-значення на cache-wide replay | Milestone 004 closeout: `allocated bytes / payload value: 0.20` після leased hot-path delivery та reusable projector buffers | Та сама `archive benchmark stream` команда; unit-тести лише фіксують інваріанти, не є доказом throughput |
| Консистенція збірки батчів | Тести builder/validator перевіряють межі payload, lifetime, leased snapshot та checksum-контракти | `dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~RadarEventBatch"` |

### 5. Слід доказової бази (Implementation & Tests)
* Структура події: [RadarStreamEvent.cs](../../../src/Domain/Streaming/Streams/Models/RadarStreamEvent.cs)
* Модель батча подій: [RadarEventBatch.cs](../../../src/Domain/Streaming/Batches/Models/RadarEventBatch.cs)
* Реальний builder: [RadarEventBatchBuilder.cs](../../../src/Domain/Streaming/Batches/Services/RadarEventBatchBuilder/RadarEventBatchBuilder.cs), [RadarEventBatchBuilder.CapacityReset.cs](../../../src/Domain/Streaming/Batches/Services/RadarEventBatchBuilder/RadarEventBatchBuilder.CapacityReset.cs), [RadarEventBatchBuilder.Build.cs](../../../src/Domain/Streaming/Batches/Services/RadarEventBatchBuilder/RadarEventBatchBuilder.Build.cs)
* Retained pooled-copy шар: [RadarProcessingRetainedPayloadFactory.PooledCopy.cs](../../../src/Infrastructure/Processing/Retention/Services/RadarProcessingRetainedPayloadFactory/RadarProcessingRetainedPayloadFactory.PooledCopy.cs)
* Контракт cache-line: [RadarStreamContractTests.cs](../../../tests/RadarPulse.Tests/Streaming/Streams/RadarStreamContractTests.cs)
* Тести збирання та валідації: [RadarEventBatchValidatorTests.cs](../../../tests/RadarPulse.Tests/Streaming/Batches/RadarEventBatchValidatorTests.cs)
* Benchmark-докази Milestone 004: [closeout](../../milestones/004-processing-core-input-contract-closeout.md), [decision trace](../../milestones/004-processing-core-input-contract-decision-trace.md), [plan evidence](../../milestones/004-processing-core-input-contract-plan.md)
* Практичний протокол профілювання: [Додаток А](appendix_a_profiling.md)

### 6. Протокол допиту процесу (Verification Commands)
Перевірка контрактів радарного батча:
```bash
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~RadarEventBatch"
```

Відтворення benchmark-доказу для throughput та allocation-per-value:
```bash
dotnet build -c Release src/Presentation/RadarPulse.Cli/RadarPulse.Cli.csproj --no-restore
dotnet run --no-build -c Release --project src/Presentation/RadarPulse.Cli/RadarPulse.Cli.csproj -- archive benchmark stream --cache data/nexrad --max-files 1000000 --iterations 1 --warmup-iterations 0 --parallelism 24 --decompressor radarpulse
```

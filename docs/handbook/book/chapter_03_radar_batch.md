# Розділ 3: Контракт радарного батча

Уявіть, що ви ведете масштабне розслідування. Злочинна мережа діє по всій країні, і вам щосекунди надходять тисячі дрібних звітів від оперативників: номери машин, імена підозрюваних, адреси забігайлівок. Якщо ви під кожну таку записку будете заводити окрему велику картонну папку-справу з гербом міністерства, купувати під неї окремий сейф та наймати окремого кур'єра для перенесення між кабінетами — ваш офіс захлинеться в бюрократичному папері за першу ж годину. Витрати на обслуговування процесу (оренда приміщень для сейфів, зарплата кур'єрам) перевищать користь від самого розслідування.

Саме це відбувається зі звичайною .NET-програмою, яка намагається наївно представляти гігабайти радарних точок у вигляді класичних об'єктів (класів) C#.

## 3.1. Битва за купу: Тінь Garbage Collector

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

У середовищі .NET кожен об'єкт, виділений у керованій купі (Heap), має суттєві накладні витрати:
1. **SyncBlockIndex** (8 байтів на 64-бітній системі) — використовується для блокувань та хеш-кодів.
2. **TypeHandle** (8 байтів) — вказівник на таблицю методів класу для забезпечення поліморфізму.
3. **Вирівнювання пам'яті** — пам'ять під об'єкт виділяється порціями, що призводить до появи порожніх «дірок».

У результаті, навіть крихітний об'єкт із парою полів займає в пам'яті мінімум 24–32 байти. А тепер помножте ці накладні витрати на 10 мільйонів подій за хвилину. Ми отримаємо гігабайти «сміття», яке просто лежить у купі й чекає на Garbage Collector (GC). Коли GC вирішить навести лад, він буде змушений зупинити роботу всієї програми (Stop-the-World pause), щоб перевірити зв'язки між мільйонами дрібних об'єктів. Процесор замре, обробка радарів зупиниться, і реальний шторм на карті оновиться з катастрофічним запізненням.

## 3.2. Рішення: Сержант `RadarStreamEvent` як Unmanaged Struct

Щоб виграти цю битву, під час Віхи `004` (Processing Core Input Contract) ми повністю змінили правила гри. Ми відмовилися від класів і перейшли на некеровані (unmanaged) структури фіксованого розміру.

Знайомтеся з нашим «сержантом» — `RadarStreamEvent`. Його розмір становить **рівно 64 байти**. Він не живе в купі як окремий об'єкт. Він лежить у щільних, безперервних масивах пам'яті, де одна подія слідує за іншою без жодних заголовків та метаінформації .NET.

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
    // Замість byte[] ми зберігаємо зміщення та довжину в спільному буфері батча
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

Зверніть увагу на головний архітектурний трюк. У структурі `RadarStreamEvent` немає поля `byte[] Payload`. Якби воно там було, це поле містило б посилання на інший об'єкт у купі, що знову повернуло б нас до проблеми алокацій.

Замість цього подія зберігає два простих цілих числа: `PayloadOffset` (зміщення) та `PayloadLength` (довжина). Вони вказують на конкретний сегмент в одному великому, спільному масиві байтів, який належить усьому батчу. Це схоже на те, як детектив записує у блокнот: «Докази лежать у великій скрині на полиці №3, починаючи з 15-го сантиметра і довжиною 40 сантиметрів». Самі докази лежать в одному місці, а ми оперуємо лише легкими числовими координатами.

## 3.3. Шар Streaming: `RadarEventBatch`

Ці 64-байтні структури групуються в єдину сутність — `RadarEventBatch`. Це немутабельний (immutable) пакет, який містить:
1. Масив метаданих подій: `ReadOnlyMemory<RadarStreamEvent> Events`.
2. Великий суцільний масив байтів: `ReadOnlyMemory<byte> Payload`.
3. Версії схем та каталогів (`StreamSchemaVersion`, `DictionaryVersion`, `SourceUniverseVersion`) для підтвердження того, що батч інтерпретується за тими самими правилами, за якими був створений.

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

Коли ви стикаєтеся з необхідністю обробки 500 мільйонів payload-значень за секунду, першим імпульсом є максимальне спрощення десеріалізації. В ідеальному світі ми могли б скористатися концептуальним трюком **Zero-Copy Deserialization** (десеріалізація без копіювання), інтерпретуючи сирі розпаковані байти безпосередньо як масив некерованих 64-байтних структур `RadarStreamEvent`:

```csharp
// Концептуальний ідеал (не використовується в реальній реалізації):
ReadOnlySpan<RadarStreamEvent> events = MemoryMarshal.Cast<byte, RadarStreamEvent>(rawDecompressedBytes);
```

Цей приклад виглядає неймовірно ефектно на папері: процесор отримує прямий доступ до пам'яті за одну інструкцію без жодного копіювання. Проте в суворому середовищі NEXRAD Level II цей концептуальний метод виявляється повністю непридатним.

Замість цього в реальному коді нашого проекту використовується **`RadarEventBatchBuilder`** з багаторазовими внутрішніми буферами, а **`ArrayPool`** підключається на сусідніх гарячих ділянках: декомпресія, retained payload, pooled-copy та дельти обробки. Давайте порівняємо ці підходи та розберемося, чому реальна архітектура виявилася значно надійнішою:

| Критерій порівняння | Концептуальний `MemoryMarshal.Cast` | Реальна реалізація: `RadarEventBatchBuilder` + retained ownership |
| :--- | :--- | :--- |
| **Порядок байтів (Endianness)** | **Провал.** NEXRAD Level II зберігає числа у форматі Big-Endian. Процесори x86-64/ARM64 працюють з Little-Endian. Прямий Cast прочитає перевернуті байти (сміття). | **Успіх.** Будівельник зчитує поля за допомогою `BinaryPrimitives.ReadInt32BigEndian` та виконує правильну конвертацію ендіанності на льоту. |
| **Змінна довжина та зміщення** | **Провал.** Прямий Cast вимагає однорідного масиву фіксованого розміру. У NEXRAD початкові зміщення (`PayloadOffset`) та довжина гейтів (`GateCount`) кожної поодинокої події відрізняються. | **Успіх.** Будівельник динамічно обчислює зміщення корисного навантаження (`payloadOffset`) для кожної події та копіює його у суміжний буфер. |
| **Алокації пам'яті (Heap Allocations)** | Нульові (просте приведення покажчиків), але тільки для ідеального однорідного формату. | На стабільному leased hot path builder повторно використовує власні `eventBuffer` та `payloadBuffer`, збільшуючи їх лише через `Array.Resize`, коли capacity замала. Довше володіння батчем переноситься в retained/pooled-copy шар, де масиви вже орендуються з `ArrayPool`. |
| **Валідація та цілісність** | Відсутня на етапі приведення типу. | Будівельник перевіряє межі масивів (`EnsurePayloadCapacity`), валідує версії топології та підраховує контрольну суму корисного навантаження за один прохід. |
| **Архітектурна чистота** | Жорстке зв'язування з бінарним форматом файлу. | Повна ізоляція: будівельник конвертує складний зовнішній двійковий формат у чисті доменні структури. |

Реальний процес наповнення батча відбувається поетапно: парсер NEXRAD покроково зчитує бінарні записи, конвертує числа, конструює структуру `RadarStreamEvent` та записує її у внутрішній масив `eventBuffer`. Потім корисне навантаження (`payload`) копіюється у виділене місце в `payloadBuffer`. Якщо батч споживається одразу, `BuildLeased()` віддає посилання на ці самі буфери, а `ResetRetainingCapacity()` очищає лічильники без повторного виділення масивів. Якщо батч треба утримувати довше за callback, retained шар робить pooled-copy і вже там відповідає за оренду та повернення масивів.

Друга складова нашого успіху — **кеш-локальність (Cache Friendliness)**. Процесор AMD Ryzen 9 9900X (Zen 5) оперує кеш-лініями розміром 64 байти. Оскільки розмір нашої структури становить рівно 64 байти, а масив є безперервним у пам'яті, кожна подія займає рівно одну кеш-лінію процесора.

Коли воркер починає читати метадані події `Events[i]`, апаратний завантажувач процесора (Hardware Prefetcher) автоматично підтягує наступні події `Events[i+1]` та `Events[i+2]` з оперативної пам'яті в надшвидкий кеш L1 та L2 заздалегідь. Процесор працює на максимальній частоті, не зупиняючи ядра для очікування повільної RAM.

Завдяки leased delivery, повторному використанню буферів, pooled-copy для довгоживучих батчів та кеш-локальності Garbage Collector перестав бути головним учасником гарячого шляху, а checksum/contract parity залишився предметом тестів і benchmark-гейтів.

Тепер, коли ми розібралися з фізичною оптимізацією даних у пам'яті, давайте подивимося на архітектурну мапу нашої системи. Як захистити доменні правила від бруду зовнішнього світу? Про це — у наступному розділі.
---

## 🔍 Матеріали справи (Investigation Case Files)

### 1. Вердикт детективів (Decision Trace & Rationale)
Впровадження некерованих (unmanaged) 64-байтних структур `RadarStreamEvent` та об'єднання їх у `RadarEventBatch` (Віхи `003`-`004`). Це прибрало per-event heap allocations із доменного контракту і дало benchmark-рівень понад 500 мільйонів payload-значень за секунду з близько `0.20` allocated bytes/payload value на cache-wide replay.

#### Чому сирі байти не стали контрактом домену
Ми могли залишити кожне радарне значення окремим об'єктом, але тоді головним учасником справи став би Garbage Collector. Могли зробити прямий `MemoryMarshal.Cast` по розпакованих байтах, але NEXRAD має big-endian поля, змінні payload-діапазони й зовнішню бінарну форму, яку не можна пускати просто в домен. Обраний шлях — компактний `RadarStreamEvent` плюс `RadarEventBatchBuilder` — не найкоротший у коді, зате він переводить чужий формат у наш контрольований контракт. Ціна вибору — ручна нормалізація полів і власний builder; виграш — cache-line stride, deterministic payload references і доказовий throughput.

### 2. Закони фізики рантайму (System Invariants)
* **Алокаційний ліміт**: Довжина структури `RadarStreamEvent` має дорівнювати рівно 64 байтам для оптимального вирівнювання в пам'яті процесора.
* **Валідація батча**: Кожен батч має містити суворі контрольні суми для перевірки цілісності радарного зліпку.

### 3. Патологоанатомічний звіт (Failure Modes & Recovery)
* **Порушення контрольної суми**: При невідповідності хешу батча вхідний конвеєр відкидає його та повертає помилку через `RadarEventBatchValidationResult` до системи моніторингу.

### 4. Докази продуктивності (Performance Evidence)

| Твердження (Claim) | Доказ (Evidence) | Команда верифікації (Verification Command) |
| :--- | :--- | :--- |
| `RadarStreamEvent` займає рівно одну 64-байтну cache line | Контрактний тест `RadarStreamContractTests.RadarStreamEventUsesOneCacheLineStride` | `dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~RadarStreamContractTests"` |
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

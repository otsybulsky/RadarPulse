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

Який результат дала ця реформа?

Він приголомшливий. На наших бенчмарках під час Віхи `009-010` швидкість обробки радарних даних досягла **понад 500 мільйонів значень корисного навантаження на секунду** (500M+ payload values/sec). А накладні витрати на виділення пам'яті в купі (Allocations) впали до мізерних **менше 0.20 байта** на одне оброблене значення!

Ми практично позбулися Garbage Collector як фактора ризику. Пам'ять системи RadarPulse залишається чистою та стабільною, як стіл педантичного слідчого перед початком робочого дня.

Тепер, коли ми розібралися з фізичною оптимізацією даних у пам'яті, давайте подивимося на архітектурну мапу нашої системи. Як захистити доменні правила від бруду зовнішнього світу? Про це — у наступному розділі.
---

## 🔍 Матеріали справи (Investigation Case Files)

### 1. Вердикт детективів (Decision Trace & Rationale)
Впровадження некерованих (unmanaged) 64-байтних структур `RadarStreamEvent` та об'єднання їх у `RadarEventBatch` (Віхи `003`-`004`). Це виключило виділення пам'яті в кучі (heap allocations) на кожну подію, дозволивши обробляти понад 500 мільйонів радарних значень на секунду без навантаження на Garbage Collector.

### 2. Закони фізики рантайму (System Invariants)
* **Алокаційний ліміт**: Довжина структури `RadarStreamEvent` має дорівнювати рівно 64 байтам для оптимального вирівнювання в пам'яті процесора.
* **Валідація батча**: Кожен батч має містити суворі контрольні суми для перевірки цілісності радарного зліпку.

### 3. Патологоанатомічний звіт (Failure Modes & Recovery)
* **Порушення контрольної суми**: При невідповідності хэшу батча вхідний конвеєр відкидає його та надсилає сигнал `RadarEventBatchValidationException` до системи моніторингу.

### 4. Слід доказової бази (Implementation & Tests)
* Структура події: [RadarStreamEvent.cs](../../../src/Domain/Streaming/Streams/Models/RadarStreamEvent.cs)
* Модель батча подій: [RadarEventBatch.cs](../../../src/Domain/Streaming/Batches/Models/RadarEventBatch.cs)
* Тести збирання та валідації: [RadarEventBatchValidatorTests.cs](../../../tests/RadarPulse.Tests/Streaming/Batches/RadarEventBatchValidatorTests.cs)

### 5. Протокол допиту процесу (Verification Commands)
Перевірка алокацій та збірки радарних батчів:
```bash
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~RadarEventBatch"
```

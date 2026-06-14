# Додаток А: Апаратне профілювання системи у лабораторії

Кожен видатний детектив знає: не можна вірити свідкам на слово. У світі високої продуктивності "свідки" — це наші припущення про роботу коду, а "речові докази" — це сирі метрики апаратного профілювання. Коли ми стверджуємо, що система RadarPulse здатна обробляти 500 мільйонів payload-значень за секунду без суттєвого навантаження на збирач сміття (Garbage Collector), ми повинні надати докази, зібрані за допомогою лабораторних приладів.

Цей додаток є практичним посібником із профілювання нашого CLI-застосунку [`RadarPulse.Cli`](../../../src/Presentation/RadarPulse.Cli/RadarPulse.Cli.csproj) на Ryzen 9 стенді за допомогою стандартних низькорівневих інструментів діагностики .NET: `dotnet-trace`, `dotnet-counters` та `dotnet-gcdump`.

---

## А.1. Профілювання CPU за допомогою `dotnet-trace`

Щоб дізнатися, на що саме процесор витрачає такти (CPU cycles) під час обробки радарних файлів, ми використовуємо утиліту `dotnet-trace`. Вона працює за принципом статистичного семплювання (sampling profiling): сотні разів на секунду утиліта знімає стек викликів усіх активних потоків, формуючи карту завантаженості системи.

### 1. Встановлення інструменту
Якщо інструмент ще не встановлено у вашій системі, виконайте глобальну команду:
```bash
dotnet tool install --global dotnet-trace
```

### 2. Запуск збору профілю
Запустіть CLI-застосунок, а потім дізнайтеся ідентифікатор його процесу (PID). Альтернативно, ви можете запустити збір трейсу безпосередньо разом із стартом програми:
```bash
dotnet-trace collect --profile cpu-sampling --name RadarPulse.Cli --format Speedscope
```
Або вказавши конкретний PID працюючого процесу:
```bash
dotnet-trace collect -p <PID> --profile cpu-sampling --format Speedscope
```

*Примітка: Формат `--format Speedscope` є надзвичайно зручним, оскільки згенерований файл `.speedscope.json` можна безпосередньо завантажити у веб-інтерфейс [speedscope.app](https://www.speedscope.app/) для візуалізації.*

---

## А.2. Аналіз Flame Graphs (Графіків полум'я)

Після завершення сесії `dotnet-trace` генерує файл трасування. Відкривши його у Speedscope, ми отримуємо **Flame Graph (Графік полум'я)**.

```text
[   Main Loop (100% CPU)   ]
[  ProcessBatch (85% CPU)  ]
[ ReadNexrad ][ ParseEvents ] [ WriteHistory ]
[  45% CPU   ][   30% CPU   ] [   10% CPU   ]
```

### Як читати графік:
1. **Ширина прямокутника** відображає частку часу CPU, яку витратив конкретний метод (включаючи його дочірні виклики). Чим ширша смуга — тим більше часу там провів процесор.
2. **Вертикальна вісь (стек)** показує глибину викликів. Зверху знаходяться базові методи (наприклад, [`Program.Main`](../../../src/Presentation/RadarPulse.Cli/EntryPoint/Program.cs)), а знизу — кінцеві інструкції (наприклад, операції бітових зсувів або читання з потоку).
3. **Пошук аномалій:**
   * Якщо ви бачите широкий прямокутник `System.Text.Json.Deserialize` посеред гарячого конвеєра — це сигнал про приховану серіалізацію, яка «краде» такти.
   * У здоровому профілі RadarPulse основна ширина має концентруватися навколо декомпресії архіву, [`ArchiveTwoRadarEventBatchProjector`](../../../src/Infrastructure/Archive/Archive2/Projectors/ArchiveTwoRadarEventBatchProjector/ArchiveTwoRadarEventBatchProjector.cs), leased-побудови батчів та обчислювальних циклів дельт. Точні відсотки не треба задавати наперед: вони мають виходити з конкретного captured trace.

---

## А.3. Allocation rate через `dotnet-counters`

`dotnet-gcdump` показує живу купу на момент знімка, але не доводить швидкість алокацій. Для тверджень на кшталт "гарячий шлях не генерує сміття" потрібен allocation-rate доказ: лічильники runtime або benchmark-колонки allocated bytes.

### 1. Встановлення інструменту
```bash
dotnet tool install --global dotnet-counters
```

### 2. Live-моніторинг runtime counters
```bash
dotnet-counters monitor --process-id <PID> System.Runtime
```

Під час benchmark-запуску перевіряйте щонайменше:

* `Allocation Rate`
* `GC Heap Size`
* `Gen 0 GC Count`
* `% Time in GC`

### 3. Збереження counters як артефакту
```bash
dotnet-counters collect --process-id <PID> --counters System.Runtime --format csv -o .tmp/profiling/radar_pulse_runtime_counters.csv
```

CSV-файл зручний для доказової бази: його можна покласти в `.tmp` або build artifacts і порівняти з benchmark-рядком `allocated bytes / payload value`.

---

## А.4. Аналіз живої купи через `dotnet-gcdump`

Коли ми перевіряємо витоки пам'яті або форму retained-пулів, `dotnet-gcdump` дозволяє зробити швидкий знімок керованої купи .NET без суттєвого уповільнення процесу. Це не allocation-rate інструмент; він відповідає на інше питання: "які об'єкти живі зараз і чому вони утримуються?"

### 1. Збір знімка купи
Для створення дампу виконайте команду:
```bash
dotnet-gcdump collect -p <PID> -o .tmp/profiling/radar_pulse_live_heap.gcdump
```

### 2. Аналіз результатів у Visual Studio або PerfView
Відкривши файл `.gcdump`, ви побачите список усіх живих об'єктів у купі та обсяг пам'яті, який вони займають:

* **Очікуваний результат:** Кількість і розмір живих `RadarStreamEvent[]` та великих `byte[]` мають стабілізуватися після warmup, а не рости разом із кількістю оброблених файлів.
* **Слід пулів пам'яті:** Великі масиви мають пояснюватися builder-буферами, `ArrayPool<T>`, retained event/payload pools або активними leased/retained batch-об'єктами.
* **Порівняння знімків (Diffing):** Зробіть один дамп після warmup, а другий — після тривалого benchmark-запуску. Якщо retained heap росте без повернення до плато, це вже доказ витоку або неповерненого pooled resource.

---

## А.5. Матеріали справи (Investigation Case Files)

### 1. Закони фізики рантайму (System Invariants)
* **Allocation rate:** Гарячий шлях має підтверджуватися `dotnet-counters` або benchmark-колонками `allocated bytes / payload value`. `dotnet-gcdump` сам по собі цього не доводить.
* **CPU Hot Path:** Значна частка CPU має припадати на декомпресію, projection, leased batch delivery та дельта-цикли. Конкретний відсоток фіксується в trace-артефакті, а не вгадується наперед.

### 2. Слід доказової бази (Implementation & Tests)
* Конфігурація діагностики: [RadarPulse.Cli.csproj](../../../src/Presentation/RadarPulse.Cli/RadarPulse.Cli.csproj)
* Сценарії навантаження: [radarpulse-product-demo.ps1](../../../scripts/radarpulse-product-demo.ps1)
* Projection hot path: [ArchiveTwoRadarEventBatchProjector.cs](../../../src/Infrastructure/Archive/Archive2/Projectors/ArchiveTwoRadarEventBatchProjector/ArchiveTwoRadarEventBatchProjector.cs)
* Leased batch delivery: [RadarEventBatchBuilder.Build.cs](../../../src/Domain/Streaming/Batches/Services/RadarEventBatchBuilder/RadarEventBatchBuilder.Build.cs)
* Retained pooled-copy: [RadarProcessingRetainedPayloadFactory.PooledCopy.cs](../../../src/Infrastructure/Processing/Retention/Services/RadarProcessingRetainedPayloadFactory/RadarProcessingRetainedPayloadFactory.PooledCopy.cs)
* Throughput/allocation benchmark evidence: [Milestone 004 closeout](../../milestones/004-processing-core-input-contract-closeout.md)

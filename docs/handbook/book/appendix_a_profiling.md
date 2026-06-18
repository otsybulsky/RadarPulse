# Додаток 1: Лабораторне профілювання продуктивності

У продуктивній системі не можна вірити припущенням на слово. Твердження про швидкодію мають спиратися на сирі профілі виконання, runtime-лічильники, benchmark-рядки та знімки пам'яті. Коли ми стверджуємо, що система RadarPulse здатна обробляти 500 мільйонів payload-значень за секунду без суттєвого навантаження на збирач сміття (Garbage Collector), ми повинні надати докази, зібрані за допомогою лабораторних приладів.

Цей додаток є практичним посібником із профілювання нашого CLI-застосунку [`RadarPulse.Cli`](../../../src/Presentation/RadarPulse.Cli/RadarPulse.Cli.csproj) на Ryzen 9 стенді. Але головний підхід RadarPulse не починається з профайлера. Він починається з доказової інженерії всередині самого проекту: benchmark-команд, типізованих лічильників, capacity evidence, тестів і milestone-записів.

Термін "апаратне профілювання" тут був би занадто сильним. Повне hardware-level профілювання означає роботу з апаратними лічильниками процесора (hardware counters / PMU), cache-miss подіями, branch misprediction, інструкціями на цикл та інструментами на кшталт Linux `perf`, ETW/PerfView або vendor-specific профайлерів. У цьому додатку ми робимо інше: збираємо відтворюваний лабораторний маршрут доказів (evidence route) для .NET runtime, а додаткові низькорівневі інструменти використовуємо як інструменти деталізації — вони пояснюють CPU sampling, allocation rate, GC pressure і форму живої купи після того, як базовий benchmark/telemetry сигнал уже зафіксовано. Якщо майбутнє performance-твердження вимагатиме доказів саме на рівні cache misses або branch prediction, це має бути окремий шар профілювання, а не прихований зміст цього додатка.

---

## А.1. Доказова інженерія без сторонніх інструментів

Базовий контур доказів у RadarPulse не залежить від IDE-профайлера, платного APM, зовнішнього dashboard-а, Excel-таблиці чи приватного скриншота автора. Сторонній інструмент може допомогти знайти причину, але він не має бути єдиним місцем, де живе правда про систему.

Тому першою лінією доказів є не `dotnet-trace`, а власний вимірювальний контур проекту:

```text
твердження
  -> команда в репозиторії
  -> типізований результат або benchmark-рядок
  -> тест або gate, який захищає форму результату
  -> milestone-доказ із hardware/corpus/scope boundary
  -> повторний запуск рецензентом
```

Цей підхід має кілька правил.

1. **Доказ має народжуватися поруч із кодом.** Якщо ми заявляємо throughput, команда має бути в CLI. Якщо ми заявляємо дисципліну алокацій (allocation discipline), рядок має містити `allocated bytes / payload value`, а не загальний коментар "алокацій мало". Якщо ми заявляємо готовність (readiness) або місткість (capacity), ці факти мають бути доступні через типізовану model/API, а не тільки через текст у логах.

2. **Рядок benchmark-а має бути самодостатнім.** Він повинен містити режим, корпус (corpus), iterations/warmup, parallelism, payload count, elapsed time, throughput і allocation denominator. Без цього цифра не є доказом; це лише число без карти.

3. **Тести захищають форму доказу, а не підміняють вимірювання.** Unit-тест не доводить 500M payload values/s. Але він може гарантувати, що CLI-команда існує, параметри парсяться, телеметричний підсумок (telemetry summary) не приймає від'ємні лічильники, capacity evidence не губить first blocking reason, а benchmark output містить поля, потрібні для технічного review.

4. **Milestone-запис фіксує межу твердження.** Performance evidence завжди прив'язується до hardware, OS/cache contour, corpus, команди запуску і конкретного scope. Тому `500M+ payload values/s` у книзі не звучить як універсальна production-сертифікація. Це прийнятий лабораторний результат для конкретного стенда і corpus-а.

5. **Низькорівневий профайлер деталізує, а не замінює доказ.** `dotnet-trace`, `dotnet-counters` і `dotnet-gcdump` корисні, коли треба розкласти вже помічений сигнал на причини: де саме горить CPU, чи росте allocation rate, які GC-події супроводжують workload, або які об'єкти лишаються в живій купі. Але базове твердження має відтворюватися без них: через `dotnet test`, `dotnet run`, product demo verify, benchmark CLI і milestone evidence.

Практичний виграш такого підходу не в тому, що він виглядає суворіше. Він змінює саму якість інженерної розмови.

* **Відтворюваність:** рецензент може запустити ту саму команду з репозиторію і побачити ту саму форму доказу. Йому не потрібен авторський ноутбук, приватне середовище профайлера або скриншот із локального GUI.
* **Порівнюваність у часі:** кожен milestone залишає не тільки висновок, а й команду, corpus, contour і поля виводу (output fields). Наступна оптимізація порівнюється з попередньою по однаковому denominator-у: payload values/s, allocated bytes / payload value, retained bytes, queue counters.
* **Захист від самообману:** якщо performance-твердження не має команди, scope і вимірюваного output-а, воно не потрапляє в книгу як доказ. Це відсікає красиві, але порожні формулювання на кшталт "стало швидше" або "алокацій майже немає".
* **Форма, дружня до CI:** частину доказу можна захищати автоматично: parsing options, shape of output, non-negative counters, readiness/capacity fields, architecture gates. Саме вимірювання throughput може залишатися ручним і прив'язаним до hardware, але його контракт лишається машинно перевірюваним.
* **Незалежність від інструментів:** зовнішній profiler можна замінити. Маршрут доказів лишається. Якщо `dotnet-trace` недоступний, базовий benchmark і типізована телеметрія все одно покажуть, що саме змінилося; profiler лише допоможе деталізувати причину.
* **Чесна межа твердження:** доказ одразу говорить, де він чинний: hardware, corpus, cache contour, iteration count, validation profile. Це не зменшує силу результату, а робить його придатним для технічного захисту.

Саме тому цей додаток читається у два шари. Спершу йде власна доказова інженерія проекту: код сам виводить вимірювані факти. Потім ідуть runtime-інструменти .NET як інструменти деталізації: вони не створюють твердження, а пояснюють, чому виміряні факти мають саме таку форму.

---

## А.2. Деталізація CPU за допомогою `dotnet-trace`

Коли benchmark уже показав зміну throughput або підозрілу форму latency/allocation, треба зрозуміти, де саме runtime витрачає час. Для цього використовуємо `dotnet-trace`. Він збирає EventPipe-трейс і може семплювати керовані стеки потоків, формуючи карту гарячих ділянок без нативного профайлера.

### 1. Встановлення інструменту
Якщо інструмент ще не встановлено у вашій системі, виконайте глобальну команду:
```bash
dotnet tool install --global dotnet-trace
```

### 2. Attach до вже запущеного процесу
Запустіть CLI-застосунок або benchmark-команду, а потім дізнайтеся ідентифікатор процесу (PID):

```bash
dotnet-trace ps
```

Після цього зберіть CPU-sampling trace для конкретного PID:

```bash
dotnet-trace collect --process-id <PID> --profile dotnet-sampled-thread-time,dotnet-common --format Speedscope
```

Можна також підключитися за іменем процесу, якщо воно однозначне:

```bash
dotnet-trace collect --name RadarPulse.Cli --profile dotnet-sampled-thread-time,dotnet-common --format Speedscope
```

На Linux і macOS attach за PID або name вимагає, щоб цільовий процес і `dotnet-trace` мали сумісний diagnostic IPC context; на практиці це означає запуск від того самого користувача і, для `--name`, той самий `TMPDIR`.

### 3. Запуск процесу під трасуванням
Якщо треба зібрати trace від самого старту CLI, `dotnet-trace collect` може запустити дочірній процес після `--`. Для доказового сценарію краще спочатку зібрати Release-версію, а потім запускати вже готовий `.dll` через `dotnet exec`, щоб не трасувати зайві процеси `dotnet run`. Приклад нижче використовує стандартний локальний build output:

```bash
dotnet build -c Release src/Presentation/RadarPulse.Cli/RadarPulse.Cli.csproj
dotnet-trace collect --profile dotnet-sampled-thread-time,dotnet-common --format Speedscope -- \
  dotnet exec src/Presentation/RadarPulse.Cli/bin/Release/net10.0/RadarPulse.Cli.dll \
  archive benchmark stream --cache data/nexrad --max-files 1000000 \
  --iterations 1 --warmup-iterations 0 --parallelism 24 --decompressor radarpulse
```

Якщо потрібна взаємодія з stdin/stdout дочірнього процесу, додайте `--show-child-io`. Для довгих benchmark-ів варто обмежувати тривалість збору або збирати trace тільки на підозрілому відрізку, інакше trace-файл швидко стає важким.

*Примітка: Формат `--format Speedscope` є надзвичайно зручним, оскільки згенерований файл `.speedscope.json` можна безпосередньо завантажити у веб-інтерфейс [speedscope.app](https://www.speedscope.app/) для візуалізації.*

---

## А.3. Аналіз графіків полум'я

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

## А.4. Темп алокацій через `dotnet-counters`

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

## А.5. Аналіз живої купи через `dotnet-gcdump`

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

## А.6. Матеріали справи

### 1. Закони фізики рантайму
* **Спершу доказ (evidence first):** первинне performance-твердження має підтверджуватися командою репозиторію, типізованим результатом і milestone evidence, а не стороннім скриншотом профайлера.
* **Профайлер пояснює форму:** низькорівневі інструменти деталізації пояснюють CPU, allocation, GC або форму heap після базового вимірювання; вони не замінюють benchmark або gate.
* **Allocation rate:** гарячий шлях має підтверджуватися `dotnet-counters` або benchmark-колонками `allocated bytes / payload value`. `dotnet-gcdump` сам по собі цього не доводить.
* **CPU hot path:** значна частка CPU має припадати на декомпресію, projection, leased batch delivery та дельта-цикли. Конкретний відсоток фіксується в trace-артефакті, а не вгадується наперед.

### 2. Слід доказової бази
* Конфігурація діагностики: [RadarPulse.Cli.csproj](../../../src/Presentation/RadarPulse.Cli/RadarPulse.Cli.csproj)
* CLI usage і benchmark-команди: [RadarPulseCliUsage.cs](../../../src/Presentation/RadarPulse.Cli/EntryPoint/RadarPulseCliApplication/RadarPulseCliUsage.cs)
* Processing benchmark reporter: [ProcessingBenchmarkCliReporter.Synthetic.cs](../../../src/Presentation/RadarPulse.Cli/EntryPoint/RadarPulseCliApplication/ProcessingBenchmarkCliReporter/ProcessingBenchmarkCliReporter.Synthetic.cs)
* Archive stream benchmark reporter: [ArchiveBenchmarkCliApplication.StreamCacheReport.cs](../../../src/Presentation/RadarPulse.Cli/EntryPoint/RadarPulseCliApplication/ArchiveBenchmarkCliApplication/ArchiveBenchmarkCliApplication.StreamCacheReport.cs)
* Queue telemetry summary: [RadarProcessingProviderQueueTelemetrySummary.cs](../../../src/Domain/Processing/Queueing/Telemetry/RadarProcessingProviderQueueTelemetrySummary.cs)
* Retained payload telemetry: [RadarProcessingRetainedPayloadTelemetrySummary.cs](../../../src/Domain/Processing/Retention/Telemetry/RadarProcessingRetainedPayloadTelemetrySummary.cs)
* Product capacity evidence: [RadarProcessingProductionPipelineCapacityEvidence.cs](../../../src/Infrastructure/Processing/ProductPipeline/Telemetry/RadarProcessingProductionPipelineCapacityEvidence.cs)
* Сценарії навантаження: [radarpulse-product-demo.ps1](../../../scripts/radarpulse-product-demo.ps1)
* Projection hot path: [ArchiveTwoRadarEventBatchProjector.cs](../../../src/Infrastructure/Archive/Archive2/Projectors/ArchiveTwoRadarEventBatchProjector/ArchiveTwoRadarEventBatchProjector.cs)
* Leased batch delivery: [RadarEventBatchBuilder.Build.cs](../../../src/Domain/Streaming/Batches/Services/RadarEventBatchBuilder/RadarEventBatchBuilder.Build.cs)
* Retained pooled-copy: [RadarProcessingRetainedPayloadFactory.PooledCopy.cs](../../../src/Infrastructure/Processing/Retention/Services/RadarProcessingRetainedPayloadFactory/RadarProcessingRetainedPayloadFactory.PooledCopy.cs)
* CLI benchmark tests: [RadarPulseCliProcessingBenchmarkTests.cs](../../../tests/RadarPulse.Tests/Presentation/Cli/Benchmarks/RadarPulseCliProcessingBenchmarkTests.cs)
* Throughput/allocation benchmark evidence: [Milestone 004 closeout](../../milestones/004-processing-core-input-contract-closeout.md)

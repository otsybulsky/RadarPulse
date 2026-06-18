# Додаток 6: Як відтворити лабораторний стенд на Windows

Цей додаток потрібен для людини, яка не хоче вірити книзі на слово. Вона бере чисту копію репозиторію на Windows, ставить залежності, завантажує локальний NEXRAD-кеш, запускає перевірки й дивиться, чи система справді поводиться так, як описано в розділах.

Це не інструкція продукційного розгортання. Тут немає Kubernetes, TLS, зовнішнього брокера, бази даних чи маршрутизації сповіщень. Мета інша: **відтворити лабораторний стенд RadarPulse**, на якому можна перевірити розбір архівів, контракт потокової обробки, рантайм обробки, демо продукту і частину доказів продуктивності.

Linux/macOS/WSL2 маршрут винесено в [Додаток 7](appendix_g_lab_stand_linux.md). Команди й синтаксис там не є перекладом PowerShell один-в-один; це окремий Bash-маршрут з тією самою доказовою структурою.

---

## Е.1. Що саме ми відтворюємо

Лабораторний стенд складається з чотирьох речей:

```text
копія репозиторію
  -> залежності .NET і Node/npm
  -> локальний NEXRAD-кеш у data/nexrad
  -> команди перевірки CLI/archive/product
  -> необов'язковий demo-пакет Operator UI
```

Ключова межа: `data/nexrad` не є частиною Git-репозиторію. Це локальний корпус даних, який треба завантажити з публічного AWS Open Data bucket `unidata-nexrad-level2` через RadarPulse CLI.

Детермінована схема кешу:

```text
data/nexrad/level2/{yyyy}/{MM}/{dd}/{radarId}/{fileName}
```

Приклад одного файлу:

```text
data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06
```

Поруч із новими завантаженнями створюється супровідний metadata-файл:

```text
data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06.metadata.json
```

---

## Е.2. Передумови

Після встановлення Git спершу треба отримати сам репозиторій. Для відтворюваного стенда краще робити `git clone`, а не завантажувати ZIP з GitHub: команди нижче очікують нормальну Git-копію, у якій можна перевірити `git status`, подивитися remote і зафіксувати стан робочого дерева.

```powershell
git clone https://github.com/otsybulsky/RadarPulse.git
Set-Location RadarPulse
```

Якщо перевіряється fork або приватна копія, URL може бути іншим, але далі команди мають запускатися з кореня репозиторію, де лежить `RadarPulse.sln`.

На машині мають бути:

```text
Git
.NET SDK 10.0 або новіший сумісний SDK
Node.js 20.19.0+ для OperatorUi або Node.js 22.12.0+/24.0.0+
npm; OperatorUi фіксує npm 11.12.1 у packageManager
Windows PowerShell 5.1 або PowerShell 7
доступ до мережі й public AWS Open Data
достатньо місця на диску для вибраного NEXRAD-кешу
```

Версії не є декоративними. Усі C# проєкти таргетять `net10.0`, тому .NET 8/9 SDK не є достатнім для збірки. Operator UI побудований на Angular 21 і Vite 7; їхні `engines` вимагають Node.js `^20.19.0 || ^22.12.0 || >=24.0.0`. `packageManager` у `src/Presentation/OperatorUi/package.json` фіксує `npm@11.12.1`; якщо локальний npm інший, це треба записати в доказовий пакет разом із `node --version`.

Для швидкого smoke-cache достатньо кількох сотень мегабайт. Для кешу, аналогічного авторському milestone-корпусу, потрібно приблизно 5 GB плюс запас під metadata, артефакти збірки й журнали продуктивності.

Перевірка базової копії репозиторію:

```powershell
git --version
dotnet --info
node --version
npm --version
git status --short
```

Встановлення frontend-залежностей:

```powershell
Push-Location src/Presentation/OperatorUi
npm install
Pop-Location
```

Перший restore/build:

```powershell
dotnet restore RadarPulse.sln
dotnet build RadarPulse.sln -c Release --no-restore
```

У командах нижче використовується проект CLI:

```text
src/Presentation/RadarPulse.Cli/RadarPulse.Cli.csproj
```

---

## Е.3. Малий smoke-cache: швидка перевірка, що конвеєр живий

Починати треба не з повного кешу. Спочатку завантажуємо кілька файлів KTLX за одну дату й перевіряємо, що завантаження, огляд кешу, розпакування, форма replay і product archive run працюють.

Створити manifest для перших двох файлів:

```powershell
dotnet run -c Release --project src/Presentation/RadarPulse.Cli/RadarPulse.Cli.csproj -- `
  archive list `
  --date 2026-05-04 `
  --radar KTLX `
  --max-files 2 `
  --manifest data/manifests/2026-05-04-KTLX-smoke.json
```

Завантажити smoke-cache:

```powershell
dotnet run -c Release --project src/Presentation/RadarPulse.Cli/RadarPulse.Cli.csproj -- `
  archive download `
  --manifest data/manifests/2026-05-04-KTLX-smoke.json `
  --output data/nexrad `
  --concurrency 4
```

Перевірити, що кеш бачиться:

```powershell
dotnet run -c Release --project src/Presentation/RadarPulse.Cli/RadarPulse.Cli.csproj -- `
  archive inspect `
  --cache data/nexrad `
  --date 2026-05-04 `
  --radar KTLX `
  --max-files 2
```

Перевірити розпакування:

```powershell
dotnet run -c Release --project src/Presentation/RadarPulse.Cli/RadarPulse.Cli.csproj -- `
  archive validate decompress `
  --cache data/nexrad `
  --radar KTLX `
  --max-files 2
```

Перевірити форму replay:

```powershell
dotnet run -c Release --project src/Presentation/RadarPulse.Cli/RadarPulse.Cli.csproj -- `
  archive validate replay-shape `
  --cache data/nexrad `
  --radar KTLX `
  --max-files 2 `
  --parallelism 4 `
  --decompressor radarpulse
```

Запустити product pipeline на одному реальному archive-файлі:

```powershell
dotnet run -c Release --project src/Presentation/RadarPulse.Cli/RadarPulse.Cli.csproj -- `
  product pipeline run-archive `
  --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 `
  --run-id archive-smoke `
  --parallelism 4 `
  --decompressor radarpulse `
  --handlers counter-checksum
```

Якщо цей маршрут проходить, локальний стенд уже довів базову функціональність: пошук публічного архіву, детермінований запис кешу, розпакування, replay-проєкцію, шлях [`RadarEventBatch`](../../../../src/Domain/Streaming/Batches/Models/RadarEventBatch.cs) і product-facing archive run.

---

## Е.4. Кеш, еквівалентний авторському корпусу

У milestone 016/017 авторський локальний кеш мав такий контур:

| Корпус | Файли | Байти |
| :--- | ---: | ---: |
| `data/nexrad/level2/2026/05/04/KTLX` | 244 | `1_347_625_897` |
| `data/nexrad/level2/2026/05/04/KINX` | 462 | `1_404_452_903` |
| `data/nexrad/level2/2026/05/05/KTLX` | 848 | `2_232_493_336` |
| **Разом** | `1_554` | `4_984_572_136` |

Це не магічний корпус. Це просто зафіксований набір історичних NEXRAD Level II архівів, який дає достатньо великий локальний стенд для перевірок продуктивності на рівні кешу й регресійних перевірок.

Завантажити його краще через manifest-и, щоб спочатку побачити кількість файлів і байти.

```powershell
dotnet run -c Release --project src/Presentation/RadarPulse.Cli/RadarPulse.Cli.csproj -- `
  archive list --date 2026-05-04 --radar KTLX `
  --manifest data/manifests/2026-05-04-KTLX.json

dotnet run -c Release --project src/Presentation/RadarPulse.Cli/RadarPulse.Cli.csproj -- `
  archive list --date 2026-05-04 --radar KINX `
  --manifest data/manifests/2026-05-04-KINX.json

dotnet run -c Release --project src/Presentation/RadarPulse.Cli/RadarPulse.Cli.csproj -- `
  archive list --date 2026-05-05 --radar KTLX `
  --manifest data/manifests/2026-05-05-KTLX.json
```

Після цього завантажити кожен manifest:

```powershell
dotnet run -c Release --project src/Presentation/RadarPulse.Cli/RadarPulse.Cli.csproj -- `
  archive download --manifest data/manifests/2026-05-04-KTLX.json `
  --output data/nexrad --concurrency 8

dotnet run -c Release --project src/Presentation/RadarPulse.Cli/RadarPulse.Cli.csproj -- `
  archive download --manifest data/manifests/2026-05-04-KINX.json `
  --output data/nexrad --concurrency 8

dotnet run -c Release --project src/Presentation/RadarPulse.Cli/RadarPulse.Cli.csproj -- `
  archive download --manifest data/manifests/2026-05-05-KTLX.json `
  --output data/nexrad --concurrency 8
```

Команда download має показати:

```text
Required download bytes: ...
Available disk bytes: ...
Downloaded files: ...
Skipped files: ...
Downloaded bytes: ...
Skipped bytes: ...
```

Якщо запуск повторити, більшість файлів має перейти в `Skipped files`, бо cache mapper бачить існуючі файли й metadata.

Перевірити контури cache:

```powershell
dotnet run -c Release --project src/Presentation/RadarPulse.Cli/RadarPulse.Cli.csproj -- `
  archive inspect --cache data/nexrad --date 2026-05-04 --radar KTLX

dotnet run -c Release --project src/Presentation/RadarPulse.Cli/RadarPulse.Cli.csproj -- `
  archive inspect --cache data/nexrad --date 2026-05-04 --radar KINX

dotnet run -c Release --project src/Presentation/RadarPulse.Cli/RadarPulse.Cli.csproj -- `
  archive inspect --cache data/nexrad --date 2026-05-05 --radar KTLX
```

Якщо кількість файлів відрізняється від таблиці вище, це треба записати як відмінність корпусу. Твердження про продуктивність у книзі прив'язані до зафіксованого локального контуру кешу й апаратного середовища, а не до абстрактного “будь-якого NEXRAD дня”.

---

## Е.5. Функціональна перевірка маршруту архіву

Після завантаження кешу треба довести, що архіви не просто лежать на диску, а проходять шлях parser/replay.

Перевірка розпакування на перших 20 файлах KTLX:

```powershell
dotnet run -c Release --project src/Presentation/RadarPulse.Cli/RadarPulse.Cli.csproj -- `
  archive validate decompress `
  --cache data/nexrad `
  --radar KTLX `
  --max-files 20
```

Replay-shape validation:

```powershell
dotnet run -c Release --project src/Presentation/RadarPulse.Cli/RadarPulse.Cli.csproj -- `
  archive validate replay-shape `
  --cache data/nexrad `
  --radar KTLX `
  --max-files 20 `
  --parallelism 24 `
  --decompressor radarpulse
```

Replay одного файла:

```powershell
dotnet run -c Release --project src/Presentation/RadarPulse.Cli/RadarPulse.Cli.csproj -- `
  archive replay `
  --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 `
  --parallelism 24 `
  --decompressor radarpulse
```

Replay кешу:

```powershell
dotnet run -c Release --project src/Presentation/RadarPulse.Cli/RadarPulse.Cli.csproj -- `
  archive replay `
  --cache data/nexrad `
  --date 2026-05-04 `
  --radar KTLX `
  --max-files 20 `
  --parallelism 24 `
  --decompressor radarpulse
```

Ці команди не є доказом продуктивності. Вони потрібні для функціональної впевненості: файли вибрані, розпаковані, проскановані, спроєктовані й відтворені через replay.

---

## Е.6. Коротка перевірка продуктивності: не плутати smoke-перевірку з твердженням

Після функціональних перевірок можна запускати короткі команди перевірки продуктивності. Вони потрібні, щоб побачити форму рантайму на машині рецензента, але не замінюють milestone performance gates.

Stream benchmark на кеші:

```powershell
dotnet run --no-build -c Release --project src/Presentation/RadarPulse.Cli/RadarPulse.Cli.csproj -- `
  archive benchmark stream `
  --cache data/nexrad `
  --date 2026-05-04 `
  --radar KTLX `
  --max-files 20 `
  --iterations 1 `
  --warmup-iterations 0 `
  --parallelism 24 `
  --decompressor radarpulse
```

Коротка перевірка processing/rebalance:

```powershell
dotnet run --no-build -c Release --project src/Presentation/RadarPulse.Cli/RadarPulse.Cli.csproj -- `
  processing benchmark rebalance-archive `
  --cache data/nexrad `
  --date 2026-05-04 `
  --radar KTLX `
  --max-files 20 `
  --mode rebalance `
  --iterations 1 `
  --warmup-iterations 0 `
  --parallelism 24 `
  --partitions 24 `
  --shards 4
```

Коротка перевірка ordered archive processing:

```powershell
dotnet run --no-build -c Release --project src/Presentation/RadarPulse.Cli/RadarPulse.Cli.csproj -- `
  processing benchmark ordered-archive-processing `
  --cache data/nexrad `
  --date 2026-05-04 `
  --radar KTLX `
  --max-files 20 `
  --iterations 1 `
  --warmup-iterations 0 `
  --parallelism 24 `
  --partitions 24 `
  --shards 4
```

Для повторення рядків повного кешу з книги рецензент може підняти `--max-files` до `1000000`, але це вже довга performance-команда, чутлива до заліза, температури CPU, фонових процесів, диска й точного контуру кешу.

---

## Е.7. Доказ продуктивності: як отримати не скріншот, а доказ

Коротка перевірка продуктивності відповідає на просте питання: “чи benchmark взагалі запускається на цій машині?”. Доказ продуктивності відповідає на інше питання: “яку цифру можна показати зовнішньому рецензенту разом із корпусом, межею апаратного середовища, контрольною сумою, алокаціями й сирим журналом?”. Це різні рівні довіри.

У RadarPulse доказ продуктивності має складатися з п'яти артефактів:

```text
знімок середовища
журнал Release-збірки
контур кешу й журнал валідації
журнали end-to-end benchmark для повного кешу
синтетичні benchmark-журнали тільки для processing
```

Починати варто з окремої директорії. Вона не є частиною Git-репозиторію; це локальний доказовий пакет, який можна прикріпити до рецензії, milestone або матеріалів захисту роботи.

```powershell
$Stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$PerfRoot = "data/perf/reviewer-$Stamp"
New-Item -ItemType Directory -Force $PerfRoot | Out-Null
"Корінь доказів продуктивності: $PerfRoot" | Tee-Object -FilePath "$PerfRoot/README.txt"
```

Зафіксувати revision і чистоту worktree:

```powershell
git rev-parse HEAD | Tee-Object -FilePath "$PerfRoot/git-head.txt"
git status --short | Tee-Object -FilePath "$PerfRoot/git-status.txt"
```

Якщо `git status --short` не порожній, це не автоматично погано. Але рецензент має бачити, чи цифри зібрані з чистого commit, чи з локальними змінами.

Зафіксувати рантайм і машину:

```powershell
dotnet --info | Tee-Object -FilePath "$PerfRoot/dotnet-info.txt"

Get-CimInstance Win32_Processor |
  Select-Object Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed |
  Format-List |
  Out-String |
  Tee-Object -FilePath "$PerfRoot/cpu.txt"

Get-CimInstance Win32_PhysicalMemory |
  Measure-Object Capacity -Sum |
  Select-Object Count, Sum |
  Format-List |
  Out-String |
  Tee-Object -FilePath "$PerfRoot/memory.txt"

Get-CimInstance Win32_LogicalDisk |
  Select-Object DeviceID, FileSystem, Size, FreeSpace |
  Format-Table -AutoSize |
  Out-String |
  Tee-Object -FilePath "$PerfRoot/disks.txt"
```

Зібрати Release binary один раз і далі запускати саме його, щоб сирі журнали не змішували benchmark із шумом `dotnet run`, build і restore:

```powershell
dotnet build RadarPulse.sln -c Release --no-restore /p:UseSharedCompilation=false 2>&1 |
  Tee-Object -FilePath "$PerfRoot/build-release.log"

$Cli = Get-ChildItem -Path src/Presentation/RadarPulse.Cli/bin/Release -Recurse -Filter RadarPulse.Cli.dll |
  Sort-Object FullName -Descending |
  Select-Object -First 1 -ExpandProperty FullName

"CLI binary: $Cli" | Tee-Object -FilePath "$PerfRoot/cli-binary.txt"
```

Далі зафіксувати контур кешу. Без цього цифра “530M payload values/s” висить у повітрі: невідомо, які файли, які радари, скільки skipped/published, скільки payload values.

```powershell
dotnet $Cli archive inspect --cache data/nexrad --date 2026-05-04 --radar KTLX 2>&1 |
  Tee-Object -FilePath "$PerfRoot/cache-inspect-2026-05-04-KTLX.log"

dotnet $Cli archive inspect --cache data/nexrad --date 2026-05-04 --radar KINX 2>&1 |
  Tee-Object -FilePath "$PerfRoot/cache-inspect-2026-05-04-KINX.log"

dotnet $Cli archive inspect --cache data/nexrad --date 2026-05-05 --radar KTLX 2>&1 |
  Tee-Object -FilePath "$PerfRoot/cache-inspect-2026-05-05-KTLX.log"
```

Для швидкого запуску рецензента можна лишити `--max-files 20`. Для доказу, який має стояти поруч із книгою, треба проходити повний контур кешу:

```powershell
$MaxFiles = 1000000
```

Перед benchmark-ами варто зафіксувати replay-shape validation на тому самому контурі кешу. Інакше benchmark може бути швидким, але рецензент не побачить, що форма потоку пройшла окрему перевірку коректності.

```powershell
dotnet $Cli archive validate replay-shape `
  --cache data/nexrad `
  --max-files $MaxFiles `
  --parallelism 24 `
  --decompressor radarpulse 2>&1 |
  Tee-Object -FilePath "$PerfRoot/archive-replay-shape-validation.log"
```

Перший доказовий запуск — normalized stream benchmark. Він доводить швидкість і профіль алокацій переходу з archive replay до stream contract [`RadarEventBatch`](../../../../src/Domain/Streaming/Batches/Models/RadarEventBatch.cs), без handler/runtime layers.

```powershell
dotnet $Cli archive benchmark stream `
  --cache data/nexrad `
  --max-files $MaxFiles `
  --iterations 1 `
  --warmup-iterations 0 `
  --parallelism 24 `
  --decompressor radarpulse 2>&1 |
  Tee-Object -FilePath "$PerfRoot/archive-stream-cache.log"
```

У цьому журналі рецензент має шукати:

```text
Examined files per iteration
Skipped files per iteration
Published files per iteration
Stream events per iteration
Payload values per iteration
Raw value checksum per iteration
Elapsed ms
Stream events/s
Payload values/s
Allocated bytes
Allocated bytes / stream event
Allocated bytes / payload value
```

Другий доказовий запуск — full-cache rebalance matrix. Він показує не тільки пропускну здатність, а й поведінку provider/default contour проти borrowed fallback, validation profile, retained payload telemetry і allocation attribution.

Default rollout contour:

```powershell
dotnet $Cli processing benchmark rebalance-archive `
  --cache data/nexrad `
  --max-files $MaxFiles `
  --mode all `
  --execution async `
  --workers 4 `
  --iterations 1 `
  --warmup-iterations 0 `
  --parallelism 24 `
  --partitions 24 `
  --shards 4 `
  --validation-profile benchmark 2>&1 |
  Tee-Object -FilePath "$PerfRoot/rebalance-archive-default.log"
```

Контур запозиченого fallback/oracle:

```powershell
dotnet $Cli processing benchmark rebalance-archive `
  --cache data/nexrad `
  --max-files $MaxFiles `
  --mode all `
  --provider blocking-borrowed `
  --execution async `
  --workers 4 `
  --iterations 1 `
  --warmup-iterations 0 `
  --parallelism 24 `
  --partitions 24 `
  --shards 4 `
  --validation-profile benchmark 2>&1 |
  Tee-Object -FilePath "$PerfRoot/rebalance-archive-blocking-borrowed.log"
```

Тут важливі не тільки `End-to-end payload values/s`. Доказ має містити:

```text
Default selected contour
Provider overlap evidence contour
Validation: succeeded
Processing completeness: succeeded
Processing validation failed batches: 0
Failed migrations: 0
End-to-end elapsed ms
Processing callback elapsed ms
Replay and batch construction elapsed ms
End-to-end stream events/s
Processing stream events/s
End-to-end allocated bytes
Processing callback allocated bytes
Replay and batch construction allocated bytes
Retained payload pool misses
```

Третій доказовий запуск — ordered archive processing with handlers. Це ближче до product-facing runtime path: active batches, handler delta/merge, custom handler state, final checksum і completeness.

```powershell
foreach ($Handlers in @("none", "counter-checksum", "counter-checksum-heavy")) {
  foreach ($Active in @(1, 4)) {
    $Log = "$PerfRoot/ordered-archive-$Handlers-active-$Active.log"
    dotnet $Cli processing benchmark ordered-archive-processing `
      --cache data/nexrad `
      --max-files $MaxFiles `
      --active-batches $Active `
      --handlers $Handlers `
      --iterations 1 `
      --warmup-iterations 0 `
      --parallelism 24 `
      --partitions 24 `
      --shards 4 `
      --decompressor radarpulse 2>&1 |
      Tee-Object -FilePath $Log
  }
}
```

У цих журналах рецензент має перевірити:

```text
Processing path
Handler set
Ordered active batch capacity
Processing completeness: succeeded
Processing failed batches: 0
Processing validation failed batches: 0
Final processed stream events
Final processed payload values
Final raw value checksum
Final processing checksum
End-to-end payload values/s
End-to-end allocated bytes / stream event
```

Четвертий доказовий запуск — синтетичний benchmark тільки для processing. Він потрібен, щоб не змішувати archive replay, розпакування, сканування Archive II, нормалізацію ідентичності й побудову batch-ів із власне рушієм обробки. Саме цей контур найкраще підтримує стримане твердження “world-class technology”, але тільки в межах локального workload.

`--workers` і `--queue-capacity` дозволені тільки для `--mode async`, тому цикл збирає аргументи явно:

```powershell
foreach ($Mode in @("sequential", "partitioned", "async")) {
  foreach ($Handlers in @("none", "counter-checksum", "counter-checksum-heavy")) {
    $Log = "$PerfRoot/synthetic-$Mode-$Handlers.log"
    $Args = @(
      "processing", "benchmark", "synthetic",
      "--sources", "46080",
      "--batches", "256",
      "--events-per-batch", "4096",
      "--payload-values", "64",
      "--partitions", "24",
      "--shards", "4",
      "--iterations", "5",
      "--warmup-iterations", "2",
      "--mode", $Mode,
      "--handlers", $Handlers
    )

    if ($Mode -eq "async") {
      $Args += @("--workers", "4", "--queue-capacity", "8")
    }

    dotnet $Cli @Args 2>&1 | Tee-Object -FilePath $Log
  }
}
```

У синтетичних журналах рецензент має шукати:

```text
Measured contour: RadarProcessingCore over prebuilt RadarEventBatch
Excluded work
Execution mode
Handler set
Source count
Total stream events
Total payload values
Validation checksum
Stream events/s
Payload values/s
Allocated bytes / stream event
Worker failed batches: 0
Worker failed items: 0
Async validation: yes
Sync comparison checksum
Async comparison checksum
```

Після цього доказовий пакет має мати таку мінімальну форму:

```text
data/perf/reviewer-<timestamp>/
  README.txt
  git-head.txt
  git-status.txt
  dotnet-info.txt
  cpu.txt
  memory.txt
  disks.txt
  build-release.log
  cli-binary.txt
  cache-inspect-*.log
  archive-replay-shape-validation.log
  archive-stream-cache.log
  rebalance-archive-default.log
  rebalance-archive-blocking-borrowed.log
  ordered-archive-*-active-*.log
  synthetic-*.log
```

Цей пакет уже можна читати як інженерний доказ. Якщо потрібно перетворити його на milestone-документ, таблиця має виводитися з сирих журналів, а не з пам'яті:

| Рядок доказу | Основні поля |
| :--- | :--- |
| Archive stream | файли, значення корисного навантаження, elapsed ms, stream events/s, payload values/s, allocated bytes/value |
| Rebalance archive | контур провайдера, режим, валідація, повнота обробки, end-to-end пропускна здатність, пропускна здатність processing, атрибуція алокацій |
| Ordered archive processing | набір обробників, активні batch-и, фінальні контрольні суми, повнота, пропускна здатність, алокації на stream event |
| Synthetic processing | режим, набір обробників, контрольна сума валідації, stream events/s, payload values/s, allocated bytes/event, async validation |

Правильне формулювання після такого запуску:

```text
На цій машині, на цьому commit і на цьому контурі кешу Release benchmark
показав <N> stream events/s і <M> payload values/s для виміряного шляху.
Запуск містив докази checksum/completeness/allocation і не є сертифікацією
для інших машин.
```

Неправильне формулювання:

```text
RadarPulse завжди обробляє <M> payload values/s.
Це доводить продукційну пропускну здатність.
Це доводить, що система швидша за названі зовнішні системи.
```

Linux/macOS/WSL2 не має бути короткою приміткою в кінці Windows-інструкції. Повний Bash-маршрут із `tee`, `find`, shell arrays і діагностикою платформи винесено в [Додаток 7](appendix_g_lab_stand_linux.md).

Цей додаток не вимагає, щоб рецензент повторив авторські цифри. Він вимагає, щоб рецензент міг отримати власні сирі журнали й чесно порівняти форму доказу з історичним milestone evidence: [036 performance evidence](../../../milestones/036-clean-architecture-hardening-performance-evidence.md).

---

## Е.8. Пакет демо продукту: перевірка UI та API без підміни доказу кешу

Пакетний demo script перевіряє product surface: HTTP host, Angular UI, readiness, local history, browser smoke і focused API gates. Він не завантажує NEXRAD-кеш і не є archive performance gate.

Windows:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/radarpulse-product-demo.ps1 verify
```

Linux/macOS/WSL2 product-demo route описано окремо в [Додатку 7](appendix_g_lab_stand_linux.md).

Ручний локальний запуск:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/radarpulse-product-demo.ps1 paths
powershell -ExecutionPolicy Bypass -File scripts/radarpulse-product-demo.ps1 reset-history
powershell -ExecutionPolicy Bypass -File scripts/radarpulse-product-demo.ps1 start
```

В іншому терміналі:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/radarpulse-product-demo.ps1 readiness
powershell -ExecutionPolicy Bypass -File scripts/radarpulse-product-demo.ps1 demo -RunId product-demo
powershell -ExecutionPolicy Bypass -File scripts/radarpulse-product-demo.ps1 history
```

Відкрити UI:

```text
http://127.0.0.1:5129
```

Що це доводить:

```text
збірка, тести й smoke Angular
same-origin доставка RadarPulse.Http
product API, readiness і history
детермінований синтетичний product run
панель діагностики, capacity і read-model
```

Що це не доводить:

```text
завантаження NEXRAD-кешу
коректність розбору архіву на повному корпусі
500M+ payload values/s на машині рецензента
готовність до продукційного розгортання
```

Саме тому цей додаток тримає два маршрути окремо: cache/bootstrap для archive evidence і package verify для product/demo evidence.

---

## Е.9. Мінімальна карта “усе працює”

| Крок | Команда | Ознака успіху |
| :--- | :--- | :--- |
| Збірка | `dotnet build RadarPulse.sln -c Release --no-restore` | Solution збирається |
| UI-залежності | `npm install` у [`src/Presentation/OperatorUi`](../../../../src/Presentation/OperatorUi) | `node_modules` встановлено без фатальних помилок |
| Маніфест | `archive list --manifest ...` | Є summary файлів/байтів і JSON manifest |
| Завантаження | `archive download --manifest ...` | Файли завантажені або пропущені; немає preflight failure |
| Огляд кешу | `archive inspect --cache ...` | Cache summary показує вибрані файли |
| Розпакування | `archive validate decompress ...` | Валідація завершується без помилки |
| Форма replay | `archive validate replay-shape ...` | Replay shape validation завершується |
| Product archive | `product pipeline run-archive ...` | Run готовий або повідомляє явну причину блокування |
| Демо-пакет | `radarpulse-product-demo.ps1 verify` | Пакетна перевірка проходить |
| Коротка перевірка продуктивності | `archive benchmark stream ...` | Надруковано summary пропускної здатності й алокацій |
| Доказ продуктивності | `Tee-Object` маршрут з Е.7 | Сирі журнали містять середовище, контур кешу, контрольні суми, пропускну здатність і алокації |

Якщо якийсь крок падає, це не треба ховати. Лабораторний стенд цінний саме тим, що відмова має короткий шлях: manifest, download, cache path, decompressor, replay shape, product surface або benchmark contour.

---

## Е.10. Межі відповідальності цього додатка

Цей додаток не робить нових тверджень про продуктивність. Він дає маршрут відтворення.

Не заявляється:

```text
що публічний AWS archive завжди має однакову доступність у момент запуску
що рецензент отримає ті самі цифри пропускної здатності на іншому CPU/SSD/OS
що smoke-cache еквівалентний full-cache performance gate
що product demo script завантажує або валідовує NEXRAD-корпус
що local archive replay є справжнім прийманням живої радарної мережі
```

Твердження, яке цей додаток дозволяє:

```text
Рецензент може підняти локальний Windows-стенд, створити детермінований
NEXRAD-кеш, запустити archive validation, перевірити product demo і вибрати
обмежену коротку перевірку продуктивності або команди доказу продуктивності без приватних
інструкцій автора.
```

Це практичний еквівалент хорошого інженерного рукостискання: ось корпус, ось команди, ось межі, ось де починається чесний доказ.

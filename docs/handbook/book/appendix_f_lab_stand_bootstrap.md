# Додаток Е: Як відтворити лабораторний стенд на Windows

Цей додаток потрібен для людини, яка не хоче вірити книзі на слово. Вона бере чистий checkout на Windows, ставить залежності, завантажує локальний NEXRAD cache, запускає перевірки й дивиться, чи система справді поводиться так, як описано в розділах.

Це не production deployment guide. Тут немає Kubernetes, TLS, зовнішнього брокера, бази даних чи alert routing. Мета інша: **відтворити лабораторний стенд RadarPulse**, на якому можна перевірити archive parsing, streaming contract, processing runtime, product demo і частину performance evidence.

Linux/macOS/WSL2 маршрут винесено в [Додаток Є](appendix_g_lab_stand_linux.md). Команди й синтаксис там не є перекладом PowerShell один-в-один; це окремий Bash runbook з тією самою доказовою структурою.

---

## E.1. Що саме ми відтворюємо

Лабораторний стенд складається з чотирьох речей:

```text
repository checkout
  -> .NET + Node/npm dependencies
  -> local NEXRAD cache under data/nexrad
  -> CLI/archive/product verification commands
  -> optional Operator UI demo package
```

Ключова межа: `data/nexrad` не є частиною Git-репозиторію. Це локальний корпус даних, який треба завантажити з public AWS Open Data bucket `unidata-nexrad-level2` через RadarPulse CLI.

Детермінований cache layout:

```text
data/nexrad/level2/{yyyy}/{MM}/{dd}/{radarId}/{fileName}
```

Приклад одного файлу:

```text
data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06
```

Поруч із новими завантаженнями створюється sidecar metadata:

```text
data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06.metadata.json
```

---

## E.2. Передумови

На машині мають бути:

```text
.NET SDK for the solution target framework
Node.js/npm for OperatorUi
Windows PowerShell 5.1 or PowerShell 7
network access to public AWS Open Data
enough disk space for the chosen NEXRAD cache
```

Для швидкого smoke-cache достатньо кількох сотень мегабайт. Для cache, аналогічного авторському milestone-корпусу, потрібно приблизно 5 GB плюс запас під metadata, build artifacts і performance logs.

Перевірка базового checkout:

```powershell
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

## E.3. Малий smoke-cache: швидка перевірка, що pipeline живий

Починати треба не з повного cache. Спочатку завантажуємо кілька файлів KTLX за одну дату й перевіряємо, що download, inspect, decompression, replay-shape і product archive run працюють.

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

Перевірити, що cache бачиться:

```powershell
dotnet run -c Release --project src/Presentation/RadarPulse.Cli/RadarPulse.Cli.csproj -- `
  archive inspect `
  --cache data/nexrad `
  --date 2026-05-04 `
  --radar KTLX `
  --max-files 2
```

Перевірити decompression:

```powershell
dotnet run -c Release --project src/Presentation/RadarPulse.Cli/RadarPulse.Cli.csproj -- `
  archive validate decompress `
  --cache data/nexrad `
  --radar KTLX `
  --max-files 2
```

Перевірити replay shape:

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

Якщо цей маршрут проходить, локальний стенд уже довів базову функціональність: public archive discovery, deterministic cache write, decompression, replay projection, [`RadarEventBatch`](../../../src/Domain/Streaming/Batches/Models/RadarEventBatch.cs) path і product-facing archive run.

---

## E.4. Author-equivalent cache: корпус, схожий на той, що використовувався в performance gates

У milestone 016/017 авторський локальний cache мав такий контур:

| Corpus | Files | Bytes |
| :--- | ---: | ---: |
| `data/nexrad/level2/2026/05/04/KTLX` | 244 | `1_347_625_897` |
| `data/nexrad/level2/2026/05/04/KINX` | 462 | `1_404_452_903` |
| `data/nexrad/level2/2026/05/05/KTLX` | 848 | `2_232_493_336` |
| **Total** | `1_554` | `4_984_572_136` |

Це не магічний corpus. Це просто зафіксований набір історичних NEXRAD Level II архівів, який дає достатньо великий локальний стенд для cache-level performance і regression checks.

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

Якщо file counts відрізняються від таблиці вище, це треба записати як відмінність corpus-а. Performance claims у книзі прив'язані до зафіксованого локального cache/hardware contour, а не до абстрактного “будь-якого NEXRAD дня”.

---

## E.5. Функціональна перевірка archive path

Після завантаження cache треба довести, що архіви не просто лежать на диску, а проходять parser/replay path.

Decompression validation на перших 20 файлах KTLX:

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

Single-file replay:

```powershell
dotnet run -c Release --project src/Presentation/RadarPulse.Cli/RadarPulse.Cli.csproj -- `
  archive replay `
  --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 `
  --parallelism 24 `
  --decompressor radarpulse
```

Cache replay:

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

Ці команди не є performance proof. Вони потрібні для функціональної впевненості: files selected, decompressed, scanned, projected and replayed.

---

## E.6. Performance smoke: не плутати smoke з claim-ом

Після функціональних перевірок можна запускати короткі performance smoke-команди. Вони потрібні, щоб побачити форму runtime-а на машині reviewer-а, але не замінюють milestone performance gates.

Stream benchmark на cache:

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

Processing/rebalance smoke:

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

Ordered archive processing smoke:

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

Для повторення cache-wide rows із книги reviewer може підняти `--max-files` до `1000000`, але це вже довга performance-команда, чутлива до заліза, температури CPU, фонових процесів, диска й точного cache contour.

---

## E.7. Performance evidence: як отримати не скріншот, а доказ

Performance smoke відповідає на просте питання: “чи benchmark взагалі запускається на цій машині?”. Performance evidence відповідає на інше питання: “яку цифру можна показати зовнішньому reviewer-у, разом із corpus, hardware boundary, checksum, allocation і raw log?”. Це різні рівні довіри.

У RadarPulse доказ продуктивності має складатися з п'яти артефактів:

```text
environment snapshot
Release build log
cache contour and validation log
full-cache end-to-end benchmark logs
processing-only synthetic benchmark logs
```

Починати варто з окремої директорії. Вона не є частиною Git-репозиторію; це локальний evidence bundle, який можна прикріпити до review, milestone або матеріалів захисту роботи.

```powershell
$Stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$PerfRoot = "data/perf/reviewer-$Stamp"
New-Item -ItemType Directory -Force $PerfRoot | Out-Null
"Performance evidence root: $PerfRoot" | Tee-Object -FilePath "$PerfRoot/README.txt"
```

Зафіксувати revision і чистоту worktree:

```powershell
git rev-parse HEAD | Tee-Object -FilePath "$PerfRoot/git-head.txt"
git status --short | Tee-Object -FilePath "$PerfRoot/git-status.txt"
```

Якщо `git status --short` не порожній, це не автоматично погано. Але reviewer має бачити, чи цифри зібрані з clean commit, чи з локальними змінами.

Зафіксувати runtime і машину:

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

Зібрати Release binary один раз і далі запускати саме його, щоб raw logs не змішували benchmark із `dotnet run` build/restore шумом:

```powershell
dotnet build RadarPulse.sln -c Release --no-restore /p:UseSharedCompilation=false 2>&1 |
  Tee-Object -FilePath "$PerfRoot/build-release.log"

$Cli = Get-ChildItem -Path src/Presentation/RadarPulse.Cli/bin/Release -Recurse -Filter RadarPulse.Cli.dll |
  Sort-Object FullName -Descending |
  Select-Object -First 1 -ExpandProperty FullName

"CLI binary: $Cli" | Tee-Object -FilePath "$PerfRoot/cli-binary.txt"
```

Далі зафіксувати cache contour. Без цього цифра “530M payload values/s” висить у повітрі: невідомо, які файли, які радари, скільки skipped/published, скільки payload values.

```powershell
dotnet $Cli archive inspect --cache data/nexrad --date 2026-05-04 --radar KTLX 2>&1 |
  Tee-Object -FilePath "$PerfRoot/cache-inspect-2026-05-04-KTLX.log"

dotnet $Cli archive inspect --cache data/nexrad --date 2026-05-04 --radar KINX 2>&1 |
  Tee-Object -FilePath "$PerfRoot/cache-inspect-2026-05-04-KINX.log"

dotnet $Cli archive inspect --cache data/nexrad --date 2026-05-05 --radar KTLX 2>&1 |
  Tee-Object -FilePath "$PerfRoot/cache-inspect-2026-05-05-KTLX.log"
```

Для швидкого reviewer-run можна лишити `--max-files 20`. Для доказу, який має стояти поруч із книгою, треба проходити повний cache contour:

```powershell
$MaxFiles = 1000000
```

Перед benchmark-ами варто зафіксувати replay-shape validation на тому самому cache contour. Інакше benchmark може бути швидким, але reviewer не побачить, що stream shape пройшов окрему correctness перевірку.

```powershell
dotnet $Cli archive validate replay-shape `
  --cache data/nexrad `
  --max-files $MaxFiles `
  --parallelism 24 `
  --decompressor radarpulse 2>&1 |
  Tee-Object -FilePath "$PerfRoot/archive-replay-shape-validation.log"
```

Перший evidence run — normalized stream benchmark. Він доводить швидкість і allocation profile переходу з archive replay до [`RadarEventBatch`](../../../src/Domain/Streaming/Batches/Models/RadarEventBatch.cs) stream contract, без handler/runtime layers.

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

У цьому log reviewer має шукати:

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

Другий evidence run — full-cache rebalance matrix. Він показує не тільки throughput, а й поведінку provider/default contour проти borrowed fallback, validation profile, retained payload telemetry і allocation attribution.

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

Borrowed fallback/oracle contour:

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

Тут важливі не тільки `End-to-end payload values/s`. Evidence має містити:

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

Третій evidence run — ordered archive processing with handlers. Це ближче до product-facing runtime path: active batches, handler delta/merge, custom handler state, final checksum і completeness.

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

У цих logs reviewer має перевірити:

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

Четвертий evidence run — processing-only synthetic benchmark. Він потрібен, щоб не змішувати archive replay, decompression, Archive II scanning, identity normalization і batch construction із власне processing engine. Саме цей контур найкраще підтримує restrained “world-class technology” claim, але тільки в межах локального workload.

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

У synthetic logs reviewer має шукати:

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

Після цього evidence bundle має мати таку мінімальну форму:

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

Цей bundle уже можна читати як інженерний доказ. Якщо потрібно перетворити його на milestone-документ, таблиця має виводитися з raw logs, а не з пам'яті:

| Evidence row | Primary fields |
| :--- | :--- |
| Archive stream | files, payload values, elapsed ms, stream events/s, payload values/s, allocated bytes/value |
| Rebalance archive | provider contour, mode, validation, processing completeness, end-to-end throughput, processing throughput, allocation attribution |
| Ordered archive processing | handler set, active batches, final checksums, completeness, throughput, allocation per stream event |
| Synthetic processing | mode, handler set, validation checksum, stream events/s, payload values/s, allocated bytes/event, async validation |

Правильне формулювання після такого запуску:

```text
On this machine, at this commit, over this cache contour, Release benchmark
logs show <N> stream events/s and <M> payload values/s for the measured path.
The run included checksum/completeness/allocation evidence and is not a
cross-machine certification.
```

Неправильне формулювання:

```text
RadarPulse always processes <M> payload values/s.
This proves production throughput.
This proves the system is faster than named external systems.
```

Linux/macOS/WSL2 не має бути короткою приміткою в кінці Windows-інструкції. Повний Bash-маршрут із `tee`, `find`, shell arrays і platform diagnostics винесено в [Додаток Є](appendix_g_lab_stand_linux.md).

Цей додаток не вимагає, щоб reviewer повторив авторські цифри. Він вимагає, щоб reviewer міг отримати власні raw logs і чесно порівняти форму доказу з історичним milestone evidence: [036 performance evidence](../../milestones/036-clean-architecture-hardening-performance-evidence.md).

---

## E.8. Product demo package: перевірка UI/API без підміни cache proof

Пакетний demo script перевіряє product surface: HTTP host, Angular UI, readiness, local history, browser smoke і focused API gates. Він не завантажує NEXRAD cache і не є archive performance gate.

Windows:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/radarpulse-product-demo.ps1 verify
```

Linux/macOS/WSL2 product-demo route описано окремо в [Додатку Є](appendix_g_lab_stand_linux.md).

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
Angular build/test/smoke
same-origin RadarPulse.Http delivery
product API/readiness/history
deterministic synthetic product run
diagnostics/capacity/read-model cockpit
```

Що це не доводить:

```text
NEXRAD cache download
archive parsing correctness over full corpus
500M+ payload values/s на машині reviewer-а
production deployment readiness
```

Саме тому цей додаток тримає два маршрути окремо: cache/bootstrap для archive evidence і package verify для product/demo evidence.

---

## E.9. Мінімальна карта “усе працює”

| Крок | Команда | Ознака успіху |
| :--- | :--- | :--- |
| Restore/build | `dotnet build RadarPulse.sln -c Release --no-restore` | Solution builds |
| UI dependencies | `npm install` у [`src/Presentation/OperatorUi`](../../../src/Presentation/OperatorUi) | `node_modules` встановлено без фатальних помилок |
| Manifest | `archive list --manifest ...` | Є file/byte summary і JSON manifest |
| Download | `archive download --manifest ...` | Files downloaded or skipped; no preflight failure |
| Inspect | `archive inspect --cache ...` | Cache summary shows selected files |
| Decompress | `archive validate decompress ...` | Validation completes without failure |
| Replay shape | `archive validate replay-shape ...` | Replay shape validation completes |
| Product archive | `product pipeline run-archive ...` | Run is ready or reports explicit blocking reason |
| Demo package | `radarpulse-product-demo.ps1 verify` | Packaged verification passed |
| Performance smoke | `archive benchmark stream ...` | Throughput/allocation summary printed |
| Performance evidence | `Tee-Object` route from E.7 | Raw logs include environment, cache contour, checksums, throughput and allocation |

Якщо якийсь крок падає, це не треба ховати. Лабораторний стенд цінний саме тим, що відмова має короткий шлях: manifest, download, cache path, decompressor, replay shape, product surface або benchmark contour.

---

## E.10. Межі відповідальності цього додатка

Цей додаток не робить нових performance claims. Він дає маршрут відтворення.

Не claim-иться:

```text
що public AWS archive завжди має однакову доступність у момент запуску
що reviewer отримає ті самі throughput цифри на іншому CPU/SSD/OS
що smoke-cache еквівалентний full-cache performance gate
що product demo script завантажує або валідовує NEXRAD corpus
що local archive replay є true live radar network ingestion
```

Claim, який цей додаток дозволяє:

```text
Reviewer can bootstrap a Windows local lab stand, create a deterministic NEXRAD
cache, run archive validation, run product demo verification, and choose
bounded performance smoke or performance evidence commands without asking the
author for private setup steps.
```

Це практичний еквівалент хорошого інженерного рукостискання: ось корпус, ось команди, ось межі, ось де починається чесний доказ.

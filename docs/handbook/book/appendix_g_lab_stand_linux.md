# Додаток Є: Як відтворити лабораторний стенд на Linux/macOS/WSL2

Цей додаток є окремим Bash-маршрутом для reviewer-а, який запускає RadarPulse не з Windows PowerShell, а з Linux, macOS або WSL2. Він має ту саму доказову структуру, що й [Windows-додаток](appendix_f_lab_stand_bootstrap.md): checkout, залежності, public NEXRAD cache, archive validation, product demo verification і performance evidence bundle.

Це не production deployment guide. Тут немає Kubernetes, TLS, зовнішнього брокера, бази даних чи alert routing. Мета простіша і сильніша для технічного review: стороння людина має отримати локальний стенд і raw performance logs без приватних інструкцій автора.

---

## Є.1. Що саме має збігатися з Windows-маршрутом

Платформа змінює shell syntax, filesystem behavior, scheduler noise і hardware counters. Вона не повинна змінювати доказову карту:

```text
repository checkout
  -> .NET + Node/npm dependencies
  -> local NEXRAD cache under data/nexrad
  -> archive validation commands
  -> product demo verification
  -> raw performance evidence bundle under data/perf
```

Кеш має той самий layout:

```text
data/nexrad/level2/{yyyy}/{MM}/{dd}/{radarId}/{fileName}
```

Приклад:

```text
data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06
```

Якщо Windows і Linux reviewer-и завантажують той самий corpus, вони мають отримати ту саму логічну форму стенда: manifest-и, file counts, replay shape, checksums і benchmark output fields. Throughput може відрізнятися. Це нормально; performance claim лишається hardware/corpus-bound.

---

## Є.2. Передумови

На машині мають бути:

```text
.NET SDK for the solution target framework
Node.js/npm for OperatorUi
Bash
git
network access to public AWS Open Data
enough disk space for the chosen NEXRAD cache
```

Перевірити базове середовище:

```bash
dotnet --info
node --version
npm --version
git status --short
uname -a
```

Підготувати змінні, які далі використовуються в командах:

```bash
CliProject="src/Presentation/RadarPulse.Cli/RadarPulse.Cli.csproj"
CacheRoot="data/nexrad"
ManifestRoot="data/manifests"
mkdir -p "$ManifestRoot"
```

Встановити frontend-залежності:

```bash
pushd src/Presentation/OperatorUi
npm install
popd
```

Зібрати solution:

```bash
dotnet restore RadarPulse.sln
dotnet build RadarPulse.sln -c Release --no-restore
```

---

## Є.3. Малий smoke-cache

Починати треба з малого corpus-а. Він швидко перевіряє, що public archive discovery, download, cache layout, decompression, replay shape і product archive path живі.

Створити manifest для перших двох KTLX файлів:

```bash
dotnet run -c Release --project "$CliProject" -- \
  archive list \
  --date 2026-05-04 \
  --radar KTLX \
  --max-files 2 \
  --manifest "$ManifestRoot/2026-05-04-KTLX-smoke.json"
```

Завантажити smoke-cache:

```bash
dotnet run -c Release --project "$CliProject" -- \
  archive download \
  --manifest "$ManifestRoot/2026-05-04-KTLX-smoke.json" \
  --output "$CacheRoot" \
  --concurrency 4
```

Перевірити cache:

```bash
dotnet run -c Release --project "$CliProject" -- \
  archive inspect \
  --cache "$CacheRoot" \
  --date 2026-05-04 \
  --radar KTLX \
  --max-files 2
```

Перевірити decompression:

```bash
dotnet run -c Release --project "$CliProject" -- \
  archive validate decompress \
  --cache "$CacheRoot" \
  --radar KTLX \
  --max-files 2
```

Перевірити replay shape:

```bash
dotnet run -c Release --project "$CliProject" -- \
  archive validate replay-shape \
  --cache "$CacheRoot" \
  --radar KTLX \
  --max-files 2 \
  --parallelism 4 \
  --decompressor radarpulse
```

Запустити product pipeline на одному реальному archive-файлі:

```bash
dotnet run -c Release --project "$CliProject" -- \
  product pipeline run-archive \
  --file "$CacheRoot/level2/2026/05/04/KTLX/KTLX20260504_000245_V06" \
  --run-id archive-smoke \
  --parallelism 4 \
  --decompressor radarpulse \
  --handlers counter-checksum
```

Якщо цей маршрут проходить, Linux/macOS/WSL2 стенд уже довів базову функціональність. Далі має сенс завантажувати більший corpus.

---

## Є.4. Author-equivalent cache

У milestone 016/017 авторський локальний cache мав такий контур:

| Corpus | Files | Bytes |
| :--- | ---: | ---: |
| `data/nexrad/level2/2026/05/04/KTLX` | 244 | `1_347_625_897` |
| `data/nexrad/level2/2026/05/04/KINX` | 462 | `1_404_452_903` |
| `data/nexrad/level2/2026/05/05/KTLX` | 848 | `2_232_493_336` |
| **Total** | `1_554` | `4_984_572_136` |

Створити manifest-и:

```bash
dotnet run -c Release --project "$CliProject" -- \
  archive list --date 2026-05-04 --radar KTLX \
  --manifest "$ManifestRoot/2026-05-04-KTLX.json"

dotnet run -c Release --project "$CliProject" -- \
  archive list --date 2026-05-04 --radar KINX \
  --manifest "$ManifestRoot/2026-05-04-KINX.json"

dotnet run -c Release --project "$CliProject" -- \
  archive list --date 2026-05-05 --radar KTLX \
  --manifest "$ManifestRoot/2026-05-05-KTLX.json"
```

Завантажити corpus:

```bash
dotnet run -c Release --project "$CliProject" -- \
  archive download --manifest "$ManifestRoot/2026-05-04-KTLX.json" \
  --output "$CacheRoot" --concurrency 8

dotnet run -c Release --project "$CliProject" -- \
  archive download --manifest "$ManifestRoot/2026-05-04-KINX.json" \
  --output "$CacheRoot" --concurrency 8

dotnet run -c Release --project "$CliProject" -- \
  archive download --manifest "$ManifestRoot/2026-05-05-KTLX.json" \
  --output "$CacheRoot" --concurrency 8
```

Перевірити контури:

```bash
dotnet run -c Release --project "$CliProject" -- \
  archive inspect --cache "$CacheRoot" --date 2026-05-04 --radar KTLX

dotnet run -c Release --project "$CliProject" -- \
  archive inspect --cache "$CacheRoot" --date 2026-05-04 --radar KINX

dotnet run -c Release --project "$CliProject" -- \
  archive inspect --cache "$CacheRoot" --date 2026-05-05 --radar KTLX
```

Якщо file counts відрізняються від таблиці, це треба записати в evidence bundle. Це не провал; це зміна corpus-а, яку reviewer має бачити.

---

## Є.5. Функціональна archive validation

Decompression validation:

```bash
dotnet run -c Release --project "$CliProject" -- \
  archive validate decompress \
  --cache "$CacheRoot" \
  --radar KTLX \
  --max-files 20
```

Replay-shape validation:

```bash
dotnet run -c Release --project "$CliProject" -- \
  archive validate replay-shape \
  --cache "$CacheRoot" \
  --radar KTLX \
  --max-files 20 \
  --parallelism 24 \
  --decompressor radarpulse
```

Single-file replay:

```bash
dotnet run -c Release --project "$CliProject" -- \
  archive replay \
  --file "$CacheRoot/level2/2026/05/04/KTLX/KTLX20260504_000245_V06" \
  --parallelism 24 \
  --decompressor radarpulse
```

Cache replay:

```bash
dotnet run -c Release --project "$CliProject" -- \
  archive replay \
  --cache "$CacheRoot" \
  --date 2026-05-04 \
  --radar KTLX \
  --max-files 20 \
  --parallelism 24 \
  --decompressor radarpulse
```

Ці команди не є performance proof. Вони доводять, що downloaded files проходять parser/replay path.

---

## Є.6. Performance smoke

Stream benchmark на малому cache contour:

```bash
dotnet run --no-build -c Release --project "$CliProject" -- \
  archive benchmark stream \
  --cache "$CacheRoot" \
  --date 2026-05-04 \
  --radar KTLX \
  --max-files 20 \
  --iterations 1 \
  --warmup-iterations 0 \
  --parallelism 24 \
  --decompressor radarpulse
```

Processing/rebalance smoke:

```bash
dotnet run --no-build -c Release --project "$CliProject" -- \
  processing benchmark rebalance-archive \
  --cache "$CacheRoot" \
  --date 2026-05-04 \
  --radar KTLX \
  --max-files 20 \
  --mode rebalance \
  --iterations 1 \
  --warmup-iterations 0 \
  --parallelism 24 \
  --partitions 24 \
  --shards 4
```

Ordered archive processing smoke:

```bash
dotnet run --no-build -c Release --project "$CliProject" -- \
  processing benchmark ordered-archive-processing \
  --cache "$CacheRoot" \
  --date 2026-05-04 \
  --radar KTLX \
  --max-files 20 \
  --iterations 1 \
  --warmup-iterations 0 \
  --parallelism 24 \
  --partitions 24 \
  --shards 4
```

Smoke показує, що benchmark path запускається. Він не замінює evidence bundle.

---

## Є.7. Performance evidence bundle

Створити evidence root:

```bash
PerfRoot="data/perf/reviewer-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$PerfRoot"
printf 'Performance evidence root: %s\n' "$PerfRoot" | tee "$PerfRoot/README.txt"
```

Зафіксувати Git revision і worktree:

```bash
git rev-parse HEAD | tee "$PerfRoot/git-head.txt"
git status --short | tee "$PerfRoot/git-status.txt"
```

Зафіксувати runtime і platform diagnostics:

```bash
dotnet --info | tee "$PerfRoot/dotnet-info.txt"
uname -a | tee "$PerfRoot/os.txt"

{
  if command -v lscpu >/dev/null 2>&1; then
    lscpu
  else
    sysctl -a 2>/dev/null | grep -E '^(machdep.cpu|hw.ncpu|hw.memsize)' || true
  fi
} | tee "$PerfRoot/cpu.txt"

{
  if command -v free >/dev/null 2>&1; then
    free -h
  else
    vm_stat 2>/dev/null || true
  fi
} | tee "$PerfRoot/memory.txt"

{
  df -h . data 2>/dev/null || df -h .
} | tee "$PerfRoot/disks.txt"
```

Зібрати Release binary один раз:

```bash
dotnet build RadarPulse.sln -c Release --no-restore /p:UseSharedCompilation=false 2>&1 |
  tee "$PerfRoot/build-release.log"

Cli="$(find src/Presentation/RadarPulse.Cli/bin/Release -name RadarPulse.Cli.dll | sort | tail -n 1)"
test -n "$Cli" || { echo "RadarPulse.Cli.dll not found"; exit 1; }
printf 'CLI binary: %s\n' "$Cli" | tee "$PerfRoot/cli-binary.txt"
```

Зафіксувати cache contour:

```bash
dotnet "$Cli" archive inspect --cache "$CacheRoot" --date 2026-05-04 --radar KTLX 2>&1 |
  tee "$PerfRoot/cache-inspect-2026-05-04-KTLX.log"

dotnet "$Cli" archive inspect --cache "$CacheRoot" --date 2026-05-04 --radar KINX 2>&1 |
  tee "$PerfRoot/cache-inspect-2026-05-04-KINX.log"

dotnet "$Cli" archive inspect --cache "$CacheRoot" --date 2026-05-05 --radar KTLX 2>&1 |
  tee "$PerfRoot/cache-inspect-2026-05-05-KTLX.log"
```

Для quick reviewer-run можна поставити `MaxFiles=20`. Для full-cache evidence route:

```bash
MaxFiles=1000000
```

Replay-shape validation на тому самому contour:

```bash
dotnet "$Cli" archive validate replay-shape \
  --cache "$CacheRoot" \
  --max-files "$MaxFiles" \
  --parallelism 24 \
  --decompressor radarpulse 2>&1 |
  tee "$PerfRoot/archive-replay-shape-validation.log"
```

Normalized stream benchmark:

```bash
dotnet "$Cli" archive benchmark stream \
  --cache "$CacheRoot" \
  --max-files "$MaxFiles" \
  --iterations 1 \
  --warmup-iterations 0 \
  --parallelism 24 \
  --decompressor radarpulse 2>&1 |
  tee "$PerfRoot/archive-stream-cache.log"
```

Full-cache rebalance default contour:

```bash
dotnet "$Cli" processing benchmark rebalance-archive \
  --cache "$CacheRoot" \
  --max-files "$MaxFiles" \
  --mode all \
  --execution async \
  --workers 4 \
  --iterations 1 \
  --warmup-iterations 0 \
  --parallelism 24 \
  --partitions 24 \
  --shards 4 \
  --validation-profile benchmark 2>&1 |
  tee "$PerfRoot/rebalance-archive-default.log"
```

Borrowed fallback/oracle contour:

```bash
dotnet "$Cli" processing benchmark rebalance-archive \
  --cache "$CacheRoot" \
  --max-files "$MaxFiles" \
  --mode all \
  --provider blocking-borrowed \
  --execution async \
  --workers 4 \
  --iterations 1 \
  --warmup-iterations 0 \
  --parallelism 24 \
  --partitions 24 \
  --shards 4 \
  --validation-profile benchmark 2>&1 |
  tee "$PerfRoot/rebalance-archive-blocking-borrowed.log"
```

Ordered archive processing with handlers:

```bash
for Handlers in none counter-checksum counter-checksum-heavy; do
  for Active in 1 4; do
    Log="$PerfRoot/ordered-archive-$Handlers-active-$Active.log"
    dotnet "$Cli" processing benchmark ordered-archive-processing \
      --cache "$CacheRoot" \
      --max-files "$MaxFiles" \
      --active-batches "$Active" \
      --handlers "$Handlers" \
      --iterations 1 \
      --warmup-iterations 0 \
      --parallelism 24 \
      --partitions 24 \
      --shards 4 \
      --decompressor radarpulse 2>&1 |
      tee "$Log"
  done
done
```

Processing-only synthetic benchmark:

```bash
for Mode in sequential partitioned async; do
  for Handlers in none counter-checksum counter-checksum-heavy; do
    Log="$PerfRoot/synthetic-$Mode-$Handlers.log"
    Args=(
      processing benchmark synthetic
      --sources 46080
      --batches 256
      --events-per-batch 4096
      --payload-values 64
      --partitions 24
      --shards 4
      --iterations 5
      --warmup-iterations 2
      --mode "$Mode"
      --handlers "$Handlers"
    )

    if [ "$Mode" = "async" ]; then
      Args+=(--workers 4 --queue-capacity 8)
    fi

    dotnet "$Cli" "${Args[@]}" 2>&1 | tee "$Log"
  done
done
```

Мінімальна форма evidence bundle:

```text
data/perf/reviewer-<timestamp>/
  README.txt
  git-head.txt
  git-status.txt
  dotnet-info.txt
  os.txt
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

Правильне формулювання після запуску:

```text
On this Linux/macOS/WSL2 machine, at this commit, over this cache contour,
Release benchmark logs show <N> stream events/s and <M> payload values/s for
the measured path. The run includes checksum/completeness/allocation evidence
and is not a cross-machine certification.
```

---

## Є.8. Product demo package

Package verify:

```bash
bash scripts/radarpulse-product-demo.sh verify
```

Ручний запуск:

```bash
bash scripts/radarpulse-product-demo.sh paths
bash scripts/radarpulse-product-demo.sh reset-history
bash scripts/radarpulse-product-demo.sh start
```

В іншому терміналі:

```bash
bash scripts/radarpulse-product-demo.sh readiness
bash scripts/radarpulse-product-demo.sh demo --run-id product-demo
bash scripts/radarpulse-product-demo.sh history
```

Відкрити UI:

```text
http://127.0.0.1:5129
```

Performance evidence від product demo не залежить: package script перевіряє API/UI/readiness, а не NEXRAD throughput.

---

## Є.9. Мінімальна карта “усе працює”

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
| Demo package | `bash scripts/radarpulse-product-demo.sh verify` | Packaged verification passed |
| Performance smoke | `archive benchmark stream ...` | Throughput/allocation summary printed |
| Performance evidence | `tee` route from Є.7 | Raw logs include environment, cache contour, checksums, throughput and allocation |

---

## Є.10. Межі відповідальності цього додатка

Не claim-иться:

```text
що public AWS archive завжди має однакову доступність у момент запуску
що Linux/macOS/WSL2 дасть ті самі throughput цифри, що Windows
що smoke-cache еквівалентний full-cache performance gate
що product demo script завантажує або валідовує NEXRAD corpus
що local archive replay є true live radar network ingestion
```

Claim, який цей додаток дозволяє:

```text
Reviewer can bootstrap a Linux/macOS/WSL2 local lab stand, create a
deterministic NEXRAD cache, run archive validation, run product demo
verification, and collect raw performance evidence without asking the author
for private setup steps.
```

Разом із [Windows-додатком](appendix_f_lab_stand_bootstrap.md) це закриває platform portability на рівні лабораторного review: дві оболонки, одна доказова структура, одна межа claims.

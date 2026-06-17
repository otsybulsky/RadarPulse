# Додаток Є: Як відтворити лабораторний стенд на Linux/macOS/WSL2

Цей додаток є окремим Bash-маршрутом для рецензента, який запускає RadarPulse не з Windows PowerShell, а з Linux, macOS або WSL2. Він має ту саму доказову структуру, що й [Windows-додаток](appendix_f_lab_stand_bootstrap.md): копія репозиторію, залежності, публічний NEXRAD-кеш, archive validation, перевірка product demo і доказовий пакет продуктивності.

Це не інструкція продукційного розгортання. Тут немає Kubernetes, TLS, зовнішнього брокера, бази даних чи маршрутизації сповіщень. Мета простіша і сильніша для технічної рецензії: стороння людина має отримати локальний стенд і сирі журнали продуктивності без приватних інструкцій автора.

---

## Є.1. Що саме має збігатися з Windows-маршрутом

Платформа змінює синтаксис shell, поведінку файлової системи, шум планувальника й апаратні лічильники. Вона не повинна змінювати доказову карту:

```text
копія репозиторію
  -> залежності .NET і Node/npm
  -> локальний NEXRAD-кеш у data/nexrad
  -> команди archive validation
  -> перевірка product demo
  -> сирий доказовий пакет продуктивності у data/perf
```

Кеш має ту саму схему:

```text
data/nexrad/level2/{yyyy}/{MM}/{dd}/{radarId}/{fileName}
```

Приклад:

```text
data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06
```

Якщо Windows і Linux-рецензенти завантажують той самий корпус, вони мають отримати ту саму логічну форму стенда: manifest-и, кількість файлів, форму replay, контрольні суми й поля benchmark-виводу. Пропускна здатність може відрізнятися. Це нормально; твердження про продуктивність лишається обмеженим апаратним середовищем і корпусом.

---

## Є.2. Передумови

Після встановлення Git спершу треба отримати сам репозиторій. Для доказового стенда краще робити `git clone`, а не завантажувати ZIP з GitHub: команди нижче очікують нормальну Git-копію, у якій можна перевірити `git status`, remote і точний стан робочого дерева.

```bash
git clone https://github.com/otsybulsky/RadarPulse.git
cd RadarPulse
```

Якщо перевіряється fork або приватна копія, URL може бути іншим, але далі команди мають запускатися з кореня репозиторію, де лежить `RadarPulse.sln`.

На машині мають бути:

```text
.NET SDK 10.0 або новіший сумісний SDK
Node.js 20.19.0+ для OperatorUi або Node.js 22.12.0+/24.0.0+
npm; OperatorUi фіксує npm 11.12.1 у packageManager
Bash
git
доступ до мережі й public AWS Open Data
достатньо місця на диску для вибраного NEXRAD-кешу
```

Версії тут є частиною відтворюваності. Усі C# проєкти таргетять `net10.0`, тому .NET 8/9 SDK не є достатнім для збірки. Operator UI побудований на Angular 21 і Vite 7; їхні `engines` вимагають Node.js `^20.19.0 || ^22.12.0 || >=24.0.0`. `packageManager` у `src/Presentation/OperatorUi/package.json` фіксує `npm@11.12.1`; якщо локальний npm інший, це треба записати в доказовий пакет разом із `node --version`.

Перевірити базове середовище:

```bash
git --version
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

## Є.3. Малий smoke-cache: швидка перевірка кешу

Починати треба з малого корпусу. Він швидко перевіряє, що пошук публічного архіву, завантаження, схема кешу, розпакування, форма replay і маршрут product archive живі.

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

Перевірити кеш:

```bash
dotnet run -c Release --project "$CliProject" -- \
  archive inspect \
  --cache "$CacheRoot" \
  --date 2026-05-04 \
  --radar KTLX \
  --max-files 2
```

Перевірити розпакування:

```bash
dotnet run -c Release --project "$CliProject" -- \
  archive validate decompress \
  --cache "$CacheRoot" \
  --radar KTLX \
  --max-files 2
```

Перевірити форму replay:

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

Якщо цей маршрут проходить, Linux/macOS/WSL2 стенд уже довів базову функціональність. Далі має сенс завантажувати більший корпус.

---

## Є.4. Кеш, еквівалентний авторському корпусу

У milestone 016/017 авторський локальний кеш мав такий контур:

| Корпус | Файли | Байти |
| :--- | ---: | ---: |
| `data/nexrad/level2/2026/05/04/KTLX` | 244 | `1_347_625_897` |
| `data/nexrad/level2/2026/05/04/KINX` | 462 | `1_404_452_903` |
| `data/nexrad/level2/2026/05/05/KTLX` | 848 | `2_232_493_336` |
| **Разом** | `1_554` | `4_984_572_136` |

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

Завантажити корпус:

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

Якщо кількість файлів відрізняється від таблиці, це треба записати в доказовий пакет. Це не провал; це зміна корпусу, яку рецензент має бачити.

---

## Є.5. Функціональна валідація архіву

Перевірка розпакування:

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

Ці команди не є доказом продуктивності. Вони доводять, що завантажені файли проходять шлях parser/replay.

---

## Є.6. Коротка перевірка продуктивності

Stream benchmark на малому контурі кешу:

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

Коротка перевірка processing/rebalance:

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

Коротка перевірка ordered archive processing:

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

Коротка перевірка показує, що benchmark path запускається. Вона не замінює доказовий пакет.

---

## Є.7. Доказовий пакет продуктивності

Створити кореневу директорію доказів:

```bash
PerfRoot="data/perf/reviewer-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$PerfRoot"
printf 'Корінь доказів продуктивності: %s\n' "$PerfRoot" | tee "$PerfRoot/README.txt"
```

Зафіксувати Git revision і worktree:

```bash
git rev-parse HEAD | tee "$PerfRoot/git-head.txt"
git status --short | tee "$PerfRoot/git-status.txt"
```

Зафіксувати рантайм і діагностику платформи:

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

Зафіксувати контур кешу:

```bash
dotnet "$Cli" archive inspect --cache "$CacheRoot" --date 2026-05-04 --radar KTLX 2>&1 |
  tee "$PerfRoot/cache-inspect-2026-05-04-KTLX.log"

dotnet "$Cli" archive inspect --cache "$CacheRoot" --date 2026-05-04 --radar KINX 2>&1 |
  tee "$PerfRoot/cache-inspect-2026-05-04-KINX.log"

dotnet "$Cli" archive inspect --cache "$CacheRoot" --date 2026-05-05 --radar KTLX 2>&1 |
  tee "$PerfRoot/cache-inspect-2026-05-05-KTLX.log"
```

Для швидкого запуску рецензента можна поставити `MaxFiles=20`. Для повного доказового маршруту по кешу:

```bash
MaxFiles=1000000
```

Replay-shape validation на тому самому контурі:

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

Контур запозиченого fallback/oracle:

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

Синтетичний benchmark тільки для processing:

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

Мінімальна форма доказового пакета:

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
На цій Linux/macOS/WSL2 машині, на цьому commit і на цьому контурі кешу
Release benchmark показав <N> stream events/s і <M> payload values/s для
виміряного шляху. Запуск містив докази checksum/completeness/allocation
і не є сертифікацією для інших машин.
```

---

## Є.8. Пакет демо продукту

Пакетна перевірка:

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

Доказ продуктивності не залежить від product demo: пакетний script перевіряє API/UI/readiness, а не NEXRAD throughput.

---

## Є.9. Мінімальна карта “усе працює”

| Крок | Команда | Ознака успіху |
| :--- | :--- | :--- |
| Збірка | `dotnet build RadarPulse.sln -c Release --no-restore` | Solution збирається |
| UI-залежності | `npm install` у [`src/Presentation/OperatorUi`](../../../src/Presentation/OperatorUi) | `node_modules` встановлено без фатальних помилок |
| Маніфест | `archive list --manifest ...` | Є summary файлів/байтів і JSON manifest |
| Завантаження | `archive download --manifest ...` | Файли завантажені або пропущені; немає preflight failure |
| Огляд кешу | `archive inspect --cache ...` | Cache summary показує вибрані файли |
| Розпакування | `archive validate decompress ...` | Валідація завершується без помилки |
| Форма replay | `archive validate replay-shape ...` | Replay shape validation завершується |
| Product archive | `product pipeline run-archive ...` | Run готовий або повідомляє явну причину блокування |
| Демо-пакет | `bash scripts/radarpulse-product-demo.sh verify` | Пакетна перевірка проходить |
| Коротка перевірка продуктивності | `archive benchmark stream ...` | Надруковано summary пропускної здатності й алокацій |
| Доказ продуктивності | `tee` маршрут з Є.7 | Сирі журнали містять середовище, контур кешу, контрольні суми, пропускну здатність і алокації |

---

## Є.10. Межі відповідальності цього додатка

Не заявляється:

```text
що публічний AWS archive завжди має однакову доступність у момент запуску
що Linux/macOS/WSL2 дасть ті самі цифри пропускної здатності, що Windows
що smoke-cache еквівалентний full-cache performance gate
що product demo script завантажує або валідовує NEXRAD-корпус
що local archive replay є справжнім прийманням живої радарної мережі
```

Твердження, яке цей додаток дозволяє:

```text
Рецензент може підняти локальний Linux/macOS/WSL2 стенд, створити
детермінований NEXRAD-кеш, запустити archive validation, перевірити
product demo і зібрати сирий доказ продуктивності без приватних
інструкцій автора.
```

Разом із [Windows-додатком](appendix_f_lab_stand_bootstrap.md) це закриває переносимість між платформами на рівні лабораторної рецензії: дві оболонки, одна доказова структура, одна межа тверджень.

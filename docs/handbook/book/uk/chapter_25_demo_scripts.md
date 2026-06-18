# Розділ 25: Демо-пакет під ключ

Фінальний доказ проекту не в тому, що автор може запустити систему на своїй машині. Доказ починається тоді, коли сторонній архітектор отримує повторюваний маршрут: зібрати, перевірити, побачити readiness, зламати очевидні місця і зрозуміти межі claims без приватних пояснень.

Для цього в RadarPulse з'явився комплексний автоматизований скрипт — [`radarpulse-product-demo.ps1`](../../../../scripts/radarpulse-product-demo.ps1) (Віхи `032`, `033`). Він не заморожує час і не підміняє реальність, а фіксує release-freeze протокол, який можна повторити на звичайній машині.

Окремі практичні маршрути для повного лабораторного стенда — від чистого checkout до завантаженого `data/nexrad`, archive validation і performance evidence — винесено в [Додаток 6 для Windows](appendix_f_lab_stand_bootstrap.md) і [Додаток 7 для Linux/macOS/WSL2](appendix_g_lab_stand_linux.md). Цей розділ описує demo/package протокол; платформені додатки закривають cache bootstrap і performance proof.

---

## 25.1. Спектр інструментів: Пульт керування скрипта

Скрипт [`radarpulse-product-demo.ps1`](../../../../scripts/radarpulse-product-demo.ps1) (і його аналог для Unix-систем [`radarpulse-product-demo.sh`](../../../../scripts/radarpulse-product-demo.sh)) оркеструє локальний product surface: Angular UI, HTTP host, demo/readiness endpoints і verify gates. Доменне ядро проходить через build/tests, а не запускається як окремий сервіс. Замість того, щоб змушувати розробника вручну прописувати шляхи, збирати Angular-компоненти через npm, налаштовувати змінні середовища та запускати dotnet-сервер, скрипт надає зручний набір команд через параметр `$Command`:

* **`paths`:** Виводить на екран повну мапу робочих папок проекту: repository root, dist-папку UI, шлях до файлу історії та робочу директорію `.tmp`.
* **`reset-history`:** Безпечно видаляє попередній файл історії `radarpulse-product-history.json`, яким керує [`RadarPulseProductFileRunHistoryStore`](../../../../src/Infrastructure/Product/History/Stores/RadarPulseProductFileRunHistoryStore/RadarPulseProductFileRunHistoryStore.cs), у межах дозволеної демо-директорії, готуючи чистий аркуш для нового експерименту.
* **`start`:** Компілює Angular SPA, прописує змінні середовища для підключення дистрибутива до сервера і запускає хост Minimal API на вказаному порті.
* **`readiness`:** Опитує BFF-ендпоінт готовності і виводить детальний звіт про статус API, історії та статичних файлів.
* **`demo`:** Ініціює тестовий прогін із параметрами `sourceCount`, `batchCount`, `eventsPerBatch` і вибраним набором аналітичних handler-ів.
* **`history`:** Показує зведену статистику щодо завантажених та виконаних розслідувань.
* **`verify`:** Запускає повний цикл верифікації системи.

Давайте подивимося, як скрипт налаштовує оточення при запуску локального хосту:

```powershell
function Start-LocalProductHost {
    param([pscustomobject]$Paths)

    # Створюємо тимчасову робочу папку, якщо її немає
    New-Item -ItemType Directory -Force -Path $Paths.DemoRoot | Out-Null

    # Будуємо фронтенд, якщо користувач не попросив пропустити цей крок
    if (-not $SkipUiBuild) {
        Invoke-CheckedProcess -Executable "npm" -CommandArguments @("run", "build") -WorkingDirectory $Paths.OperatorUiProject
    }

    # Записуємо змінні середовища для .NET процесу
    $env:RadarPulse__ProductHttp__HistoryPath = $Paths.HistoryPath
    $env:RadarPulse__ProductHttp__UseInMemoryHistory = "false"
    $env:RadarPulse__ProductHttp__EnableOperatorUiStaticFiles = "true"
    $env:RadarPulse__ProductHttp__OperatorUiStaticAssetPath = $Paths.OperatorUiDist

    Write-Host "Starting RadarPulse.Http at $($Paths.Url)"
    Write-Host "History path: $($Paths.HistoryPath)"
    Write-Host "Operator UI static asset path: $($Paths.OperatorUiDist)"

    # Запускаємо сервер
    & dotnet run --project $Paths.ProductHttpProject --urls $Paths.Url
    exit $LASTEXITCODE
}
```

Через змінні середовища `RadarPulse__ProductHttp__...` скрипт інжектує конфігурацію безпосередньо у конфігураційний шар ASP.NET Core, який читає [`RadarPulseProductHttpOptions`](../../../../src/Presentation/RadarPulse.Http/Product/Options/RadarPulseProductHttpOptions.cs). Це дозволяє тестувати систему у контрольованому локальному стані без необхідності редагувати конфігураційні файли appsettings.json.

---

## 25.2. Конвеєр верифікації: Команда `verify`

Найважливішим інструментом скрипта є команда `verify`. Це наш внутрішній суд присяжних. Вона запускає восьмиетапний конвеєр перевірки, який не прощає жодної помилки. Якщо хоча б один крок завершується невдачею, весь процес зупиняється з кодом помилки `1`.

Давайте розберемо етапи цієї перевірки в деталях:

```powershell
function Invoke-PackagedVerify {
    param([pscustomobject]$Paths)

    $testProject = Join-DemoPath -Root $Paths.RepositoryRoot -Segments @("tests", "RadarPulse.Tests", "RadarPulse.Tests.csproj")
    $solution = Join-Path $Paths.RepositoryRoot "RadarPulse.sln"

    # Окремі фільтри для архітектурного gate і продуктового HTTP/API gate
    $focusedFilter = "FullyQualifiedName~RadarPulseProductHttpHostTests|FullyQualifiedName~RadarPulseProductHttpControlTests|FullyQualifiedName~RadarPulseProductPipelineApiContractTests"
    $architectureFilter = "FullyQualifiedName~RadarPulseArchitectureTests"

    # Етап 1: Тестування фронтенду
    Write-VerifyStep "Angular unit tests"
    Invoke-CheckedProcess -Executable "npm" -CommandArguments @("test", "--", "--watch=false") -WorkingDirectory $Paths.OperatorUiProject

    # Етап 2: Збирання дистрибутива Angular
    Write-VerifyStep "Angular production build"
    Invoke-CheckedProcess -Executable "npm" -CommandArguments @("run", "build") -WorkingDirectory $Paths.OperatorUiProject

    # Етап 3: Браузерні смоук-тести
    Write-VerifyStep "Operator UI browser smoke"
    Invoke-CheckedProcess -Executable "npm" -CommandArguments @("run", "smoke") -WorkingDirectory $Paths.OperatorUiProject

    # Етап 4: Інтеграційні смоук-тести в режимі Hosted Same-Origin
    Write-VerifyStep "Hosted same-origin browser smoke"
    Invoke-CheckedProcess -Executable "npm" -CommandArguments @("run", "smoke:hosted") -WorkingDirectory $Paths.OperatorUiProject

    # Етап 5: Відновлення .NET залежностей
    Write-VerifyStep ".NET dependency restore"
    Invoke-CheckedProcess -Executable "dotnet" -CommandArguments @("restore", $solution, "--force") -WorkingDirectory $Paths.RepositoryRoot

    # Етап 6: Архітектурний gate меж шарів
    Write-VerifyStep ".NET architecture boundary gate"
    Invoke-CheckedProcess -Executable "dotnet" -CommandArguments @(
        "test",
        $testProject,
        "-c",
        "Release",
        "--no-restore",
        "--filter",
        $architectureFilter
    ) -WorkingDirectory $Paths.RepositoryRoot

    # Етап 7: Продуктові HTTP/API/readiness гейти
    Write-VerifyStep "Focused .NET product HTTP/API/readiness Release gate"
    Invoke-CheckedProcess -Executable "dotnet" -CommandArguments @(
        "test",
        $testProject,
        "-c",
        "Release",
        "--no-restore",
        "--filter",
        $focusedFilter
    ) -WorkingDirectory $Paths.RepositoryRoot

    # Етап 8: Фінальна Release-компіляція рішення
    Write-VerifyStep ".NET Release build"
    Invoke-CheckedProcess -Executable "dotnet" -CommandArguments @(
        "build",
        $solution,
        "-c",
        "Release",
        "--no-restore"
    ) -WorkingDirectory $Paths.RepositoryRoot

    Write-Host ""
    Write-Host "Packaged verification passed."
}
```

Чому цей конвеєр є настільки суворим?
1. **Ніяких здогадок:** Він проганяє не лише юніт-тести Angular, а й повноцінні браузерні смоук-тести (Smoke Tests) за допомогою Playwright. Це перевіряє інтерактивні елементи та зв'язки між Signal-полями й BFF-ендпоінтами у справжньому Chromium, а не в уявному “воно має працювати”.
2. **Суворий релізний режим (Strict Release Mode):** Ми компілюємо і тестуємо C#-проект у конфігурації `Release` із ключем `--no-restore` після примусового відновлення залежностей. Це знижує ризик «забутих» пакетів та брудних метаданих проекту.
3. **Архітектурні гейти:** Окремий `$architectureFilter` запускає `RadarPulseArchitectureTests` і перевіряє межі шарів. Продуктовий `$focusedFilter` після цього окремо перевіряє HTTP-host, control endpoints і pipeline API contracts. Так ми не змішуємо structural proof із продуктовою smoke/API перевіркою.

---

## 25.3. Що ми справді заморожуємо

Після опису `verify` легко зробити неправильний висновок: ніби одна зелена команда повністю заморожує і поведінку системи, і фізику вимірювань. Насправді вона заморожує інше — маршрут перевірки, набір гейтів і межі відповідальності.

У будь-якій високопродуктивній системі вимірювання продуктивності лишається тонкою справою: реальний `Stopwatch`, `TimeProvider.System`, фонова активність ОС і температура процесора можуть дати різницю між двома запусками. І саме тому ми не видаємо поточний demo script за лабораторію з віртуальним часом.

У RadarPulse сьогодні заморожено не годинник, а протокол:
* **Команди:** `verify` завжди проходить той самий маршрут: Angular tests, production build, browser smoke, hosted smoke, .NET restore, architecture boundary gate, focused product/API/readiness gate, Release build.
* **Робочий простір:** історія demo-run-ів живе в контрольованій `.tmp/product-demo` директорії, а reset не виходить за її межі.
* **Доказова поверхня:** readiness, warnings і non-claims говорять рецензенту, що саме перевірено, а що свідомо не заявляється.

Абстракція часу через injectable `TimeProvider` для latency buckets або deterministic virtual-time сценаріїв була б хорошим наступним hardening кроком. Але в поточній книзі це більше не звучить як виконана фіча. Поточний доказ сильніший саме тому, що він не ховає jitter: performance gates називають свої corpus/hardware межі, а demo package перевіряє повторюваність процедур, не симулюючи ідеальний всесвіт.

---

## 25.4. Заморозка коду та готовність до захисту

У професійній розробці існує термін **«Freeze Mode»** (режим заморозки коду). Це стан перед фінальним релізом, коли додавання нових функцій суворо заборонено, а всі зусилля спрямовані на стабілізацію та перевірку працездатності існуючого коду. Скрипт [`radarpulse-product-demo.ps1`](../../../../scripts/radarpulse-product-demo.ps1) — це інструмент, який фіксує цей режим і робить його перевірюваним.

Якщо будь-який крок конвеєра `verify` зазнає невдачі — розслідування вважається проваленим, а код — не готовим до демонстрації. Завдяки цьому ми отримуємо готовність до технічного захисту: покупець системи, інвестор чи головний архітектор може завантажити репозиторій, запустити одну команду `verify` і отримати не обіцянку, а пакет доказів: **тести пройдено, архітектурні гейти на місці, межі відповідальності названо, demo path зібраний у відтворюваний протокол**. Справу закрито.

## 25.5. Чеклист рецензента

Для незалежного експерта, який має лише 15 хвилин на перевірку архітектури та кодової бази RadarPulse, ми підготували цей бліц-чеклист. Він дозволяє швидко переконатися, що проект відповідає заявленим інженерним стандартам:

1. **Контракт даних та пам'ять (Розділ 3):**
   * [ ] Перевірте файл [RadarStreamEvent.cs](../../../../src/Domain/Streaming/Streams/Models/RadarStreamEvent.cs). Переконайтеся, що структура має атрибут `[StructLayout(LayoutKind.Sequential, Size = 64)]` та не містить вкладених посилальних типів (наприклад, `byte[]`).
   * [ ] Перевірте використання `ArrayPool` у парсері. Переконайтеся, що всі орендовані буфери гарантовано повертаються в пул у блоках `finally`.
2. **Чиста архітектура та захист меж (Розділ 4, 5):**
   * [ ] Переконайтеся, що шар `Domain` не посилається на зовнішні інфраструктурні пакети чи шари `Infrastructure` / `Cli`.
   * [ ] Знайдіть ручні архітектурні тести [RadarPulseArchitectureTests.cs](../../../../tests/RadarPulse.Tests/Architecture/RadarPulseArchitectureTests.cs), які захищають ці правила на рівні білда.
3. **Обробка помилок та зупинка без прихованої неправди (Fail-Closed; Розділ 20):**
   * [ ] Перевірте реалізацію аварійного відсікання. Переконайтеся, що при fail-closed прийом нової роботи закривається, активна робота завершується або скасовується кооперативно, а причина блокування лишається видимою для оператора.
4. **Запуск автоматичного аудиту:**
   * [ ] Виконайте команду автоматичної верифікації всього рішення:
     ```powershell
     powershell -ExecutionPolicy Bypass -File scripts/radarpulse-product-demo.ps1 verify
     ```
     Переконайтеся, що Angular SPA збирається, а архітектурний, продуктовий і browser smoke-гейти проходять успішно.

---

## 25.6. Межі відповідальності системи

Щоб запобігти нереалістичним очікуванням під час аудиту промислової придатності RadarPulse, ми чітко окреслюємо межі відповідальності нашого локального Standalone-рішення:

* **Масштабування в межах одного вузла (Single-Node Focus):** RadarPulse оптимізовано для витискання максимальної швидкодії з багатоядерного процесора Ryzen 9 на одній фізичній машині. Raft і Paxos — це реальні протоколи розподіленого консенсусу: вони потрібні там, де кілька вузлів мають домовитися про спільний стан, лідера або порядок записів попри збої мережі й окремих машин. У цьому проекті вони не реалізовані й не замасковані під інші назви: система не містить вбудованого consensus layer чи автоматичного шардингу між серверами.
* **Гарантія транзакційної стійкості (Durability Limits):** Локальне сховище історії записує повний JSON-документ через тимчасовий файл і `File.Move(..., overwrite: true)`. Це достатньо для demo history/restart recovery, але це не WAL, не `fsync`-сертифікація і не журналювання кожного payload-value на швидкості benchmark-контуру.
* **Спрощений контур споживання даних:** Демо-версія системи зчитує локально підготовлені бінарні NEXRAD-файли. Підключення до реальних джерел NOAA в реальному часі (наприклад, AWS S3 buckets) вимагає окремого зовнішнього агента-завантажувача, що виходить за рамки продуктивності обчислювального ядра.
* **Пропускна здатність UI-клієнтів:** Operator UI (Angular) розрахований на роботу в кабіні одного або кількох операторів-аналітиків. Без використання зовнішніх зворотних проксі-серверів (Nginx/Yarp) локальний BFF-сервер не зможе ефективно обслуговувати тисячі одночасних веб-клієнтів.

---

## Матеріали справи

### 1. Вердикт детективів
Розробка консольного PowerShell/Bash дистрибутива під ключ (Віхи `032`-`033`). Для демонстрації та верифікації системи створено скрипти [`radarpulse-product-demo.ps1`](../../../../scripts/radarpulse-product-demo.ps1) і [`radarpulse-product-demo.sh`](../../../../scripts/radarpulse-product-demo.sh) з командою `verify`, що автоматично збирає Angular, запускає unit-тести, browser smoke-тести, .NET gates і перевіряє локальний demo/readiness маршрут.

#### Чому демо стало протоколом, а не інструкцією
Демо можна було зібрати як набір інструкцій у README, але тоді кожен рецензент став би сам собі release engineer. Можна було загорнути все в Docker, але для стенда технічного захисту це додало б ще один шар, який приховує реальні команди .NET, Angular і файлового сховища. Скрипт під ключ обрано як протокол допиту системи: одна команда піднімає, перевіряє й показує межі відповідальності. Ціна вибору — треба підтримувати cross-platform shell сценарії; виграш — демонстрація відтворюється без усних пояснень.

### 2. Закони фізики рантайму
* **Кросплатформеність**: Скрипти PowerShell (`.ps1`) та Bash (`.sh`) мають вести рецензента еквівалентним маршрутом і запускати ті самі gates на Windows та WSL2/Linux.
* **Режим заморозки (Freeze Mode)**: Демо-пакет працює як локальний release-freeze протокол без зовнішніх runtime-сервісів на кшталт брокера, бази даних чи хмарної панелі керування (cloud control plane).

### 3. Патологоанатомічний звіт
* **Помилка верифікації**: Будь-яке падіння тесту (Angular, .NET або UI-smoke) миттєво зупиняє загальний скрипт верифікації з кодом завершення не нуль (Exit Code != 0).

### 4. Слід доказової бази
* Скрипт демонстрації для Windows/PowerShell: [radarpulse-product-demo.ps1](../../../../scripts/radarpulse-product-demo.ps1)
* Скрипт демонстрації для Linux/macOS/WSL2: [radarpulse-product-demo.sh](../../../../scripts/radarpulse-product-demo.sh)
* Звіт про готовність: [product-demo-readiness.md](../../../product-demo-readiness.md)

### 5. Протокол допиту процесу
Запуск повного циклу автоматичної верифікації демо-пакету:
```powershell
powershell -ExecutionPolicy Bypass -File scripts/radarpulse-product-demo.ps1 verify
```

```bash
bash scripts/radarpulse-product-demo.sh verify
```

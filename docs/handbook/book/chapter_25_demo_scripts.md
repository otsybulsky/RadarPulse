# Розділ 25: Демо-пакет під ключ

Коли розслідування завершено, свідки опитані, а речові докази розкладені по сейфах, детектив не може просто кинути папки на стіл шерифа у безладді. Справу потрібно підготувати до суду. Потрібно скласти детальний опис кожного папірця, перевірити цілісність печаток на конвертах, переконатися, що плівки з записами допитів не розмагнітилися, а протоколи підписані належними особами. Якщо захист знайде хоча б одну незакриту формальність (наприклад, відсутність підпису на ордері або недійсний сертифікат лабораторії), весь обвинувальний вирок розвалиться за секунду.

У нашому інженерному детективі фінальним акордом є збирання та перевірка всієї системи RadarPulse перед здачею в експлуатацію. Щоб сторонній архітектор чи лід-розробник не збирав доказову картину вручну з десятків команд, ми розробили комплексний автоматизований скрипт — `radarpulse-product-demo.ps1` (Віхи `032`, `033`). Він служить головним аудитором архітектури: не заморожує час і не підміняє реальність, а фіксує release-freeze протокол, який можна повторити на звичайній машині.

Окремі практичні маршрути для повного лабораторного стенда — від чистого checkout до завантаженого `data/nexrad`, archive validation і performance evidence — винесено в [Додаток Е для Windows](appendix_f_lab_stand_bootstrap.md) і [Додаток Є для Linux/macOS/WSL2](appendix_g_lab_stand_linux.md). Цей розділ описує demo/package протокол; платформені додатки закривають cache bootstrap і performance proof.

---

## 25.1. Спектр інструментів: Пульт керування скрипта

Скрипт `radarpulse-product-demo.ps1` (і його аналог для Unix-систем `radarpulse-product-demo.sh`) об'єднує всі компоненти системи — від фронтенду до доменного ядра — в єдиний керований контур. Замість того, щоб змушувати розробника вручну прописувати шляхи, збирати Angular-компоненти через npm, налаштовувати змінні середовища та запускати dotnet-сервер, скрипт надає зручний набір команд через параметр `$Command`:

* **`paths`:** Виводить на екран повну мапу робочих папок проекту ( Repository Root, Dist-папку UI, шлях до бази даних історії та робочу директорію `.tmp`).
* **`reset-history`:** Безпечно видаляє попередній файл історії `radarpulse-product-history.json` у межах дозволеної демо-директорії, готуючи чистий аркуш для нового експерименту.
* **`start`:** Компілює Angular SPA, прописує змінні середовища для підключення дистрибутива до сервера і запускає хост Minimal API на вказаному порті.
* **`readiness`:** Опитує BFF-ендпоінт готовності і виводить детальний звіт про статус API, історії та статичних файлів.
* **`demo`:** Ініціює тестовий прогін із передачею конфігурації воркерів та аналітичних обробників.
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

Через змінні середовища `RadarPulse__ProductHttp__...` скрипт інжектує конфігурацію безпосередньо у конфігураційний шар ASP.NET Core. Це дозволяє нам тестувати систему у строго детермінованому стані без необхідності редагувати конфігураційні файли `appsettings.json`, які лежать у Git.

---

## 25.2. Конвеєр верифікації: Команда `verify`

Найважливішим інструментом скрипта є команда `verify`. Це наш внутрішній суд присяжних. Вона запускає п'ятиетапний конвеєр перевірки, який не прощає жодної помилки. Якщо хоча б один крок завершується невдачею, весь процес зупиняється з кодом помилки `1`.

Давайте розберемо етапи цієї перевірки в деталях:

```powershell
function Invoke-PackagedVerify {
    param([pscustomobject]$Paths)

    $testProject = Join-DemoPath -Root $Paths.RepositoryRoot -Segments @("tests", "RadarPulse.Tests", "RadarPulse.Tests.csproj")
    $solution = Join-Path $Paths.RepositoryRoot "RadarPulse.sln"

    # Фільтр для запуску лише перевірених продуктових та архітектурних тестів
    $focusedFilter = "FullyQualifiedName~RadarPulseProductHttpHostTests|FullyQualifiedName~RadarPulseProductHttpControlTests|FullyQualifiedName~RadarPulseProductPipelineApiContractTests"

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

    # Етап 5: Відновлення та верифікація .NET збірки
    Write-VerifyStep ".NET dependency restore"
    Invoke-CheckedProcess -Executable "dotnet" -CommandArguments @("restore", $solution, "--force") -WorkingDirectory $Paths.RepositoryRoot

    # Етап 6: Архітектурні та продуктові гейти
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

    # Етап 7: Фінальна Release-компіляція рішення
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
2. **Strict Release Mode:** Ми компілюємо і тестуємо C#-проект у конфігурації `Release` із ключем `--no-restore` після примусового відновлення залежностей. Це знижує ризик «забутих» пакетів та брудних метаданих проекту.
3. **Архітектурні гейти:** Тести із фільтру `$focusedFilter` перевіряють дотримання контрактів API, коректність ініціалізації хосту та відповідність обмежень нашої системи (наприклад, перевірка того, що доменні об'єкти не виходять за межі шару обробки).

---

## 25.3. Що ми справді заморожуємо

У будь-якій високопродуктивній системі вимірювання продуктивності — тонка справа. Реальний `Stopwatch`, `TimeProvider.System`, фонова активність ОС і температура процесора можуть дати різницю між двома запусками. І саме тому ми не видаємо поточний demo script за лабораторію з віртуальним часом.

У RadarPulse сьогодні заморожено не годинник, а протокол:
* **Команди:** `verify` завжди проходить той самий маршрут: Angular tests, production build, browser smoke, hosted smoke, .NET restore, focused product/API/readiness gate, Release build.
* **Робочий простір:** історія demo-run-ів живе в контрольованій `.tmp/product-demo` директорії, а reset не виходить за її межі.
* **Доказова поверхня:** readiness, warnings і non-claims говорять рецензенту, що саме перевірено, а що свідомо не заявляється.

Абстракція часу через injectable `TimeProvider` для latency buckets або deterministic virtual-time сценаріїв була б хорошим наступним hardening кроком. Але в поточній книзі це більше не звучить як виконана фіча. Поточний доказ сильніший саме тому, що він не ховає jitter: performance gates називають свої corpus/hardware межі, а demo package перевіряє повторюваність процедур, не симулюючи ідеальний всесвіт.

---

## 25.4. Заморозка коду та готовність портфоліо

У професійній розробці існує термін **«Freeze Mode»** (режим заморозки коду). Це стан перед фінальним релізом, коли додавання нових функцій суворо заборонено, а всі зусилля спрямовані на стабілізацію та перевірку працездатності існуючого коду. Скрипт `radarpulse-product-demo.ps1` — це інструмент, який фіксує цей режим і робить його перевірюваним.

Якщо будь-який крок конвеєра `verify` зазнає невдачі — розслідування вважається проваленим, а код — не готовим до демонстрації. Завдяки цьому ми отримуємо «портфоліо-готовність» (Portfolio Readiness). Покупець нашої системи, інвестор чи головний архітектор може завантажити репозиторій, запустити одну команду `verify` і отримати не обіцянку, а пакет доказів: **тести пройдено, архітектурні гейти на місці, межі відповідальності названо, demo path зібраний у відтворюваний протокол**. Справу закрито.
## 25.5. Чеклист рецензента (Reviewer Checklist)

Для незалежного експерта, який має лише 15 хвилин на перевірку архітектури та кодової бази RadarPulse, ми підготували цей бліц-чеклист. Він дозволяє швидко переконатися, що проект відповідає заявленим інженерним стандартам:

1. **Контракт даних та пам'ять (Розділ 3):**
   * [ ] Перевірте файл [RadarStreamEvent.cs](../../../src/Domain/Streaming/Streams/Models/RadarStreamEvent.cs). Переконайтеся, що структура має атрибут `[StructLayout(LayoutKind.Sequential, Size = 64)]` та не містить вкладених посилальних типів (наприклад, `byte[]`).
   * [ ] Перевірте використання `ArrayPool` у парсері. Переконайтеся, що всі орендовані буфери гарантовано повертаються в пул у блоках `finally`.
2. **Чиста архітектура та захист меж (Розділ 4, 5):**
   * [ ] Переконайтеся, що шар `Domain` не посилається на зовнішні інфраструктурні пакети чи шари `Infrastructure` / `Cli`.
   * [ ] Знайдіть архітектурні тести (ArchUnit або аналогічні), які захищають ці правила на рівні білда.
3. **Обробка помилок та Fail-Closed (Розділ 20):**
   * [ ] Перевірте реалізацію аварійного відсікання. Переконайтеся, що при помилці валідації вхідного батча черга блокується, а всі активні фонові воркери отримують сигнал скасування через `CancellationTokenSource`.
4. **Запуск автоматичного аудиту:**
   * [ ] Виконайте команду автоматичної верифікації всього рішення:
     ```powershell
     powershell -ExecutionPolicy Bypass -File scripts/radarpulse-product-demo.ps1 verify
     ```
     Переконайтеся, що Angular SPA збирається без попереджень, а всі продуктові та інтеграційні гейти проходять успішно.

---

## 25.6. Межі відповідальності системи (Known Non-Claims)

Щоб запобігти нереалістичним очікуванням під час аудиту промислової придатності RadarPulse, ми чітко окреслюємо межі відповідальності нашого локального Standalone-рішення:

* **Масштабування в межах одного вузла (Single-Node Focus):** RadarPulse оптимізовано для витискання максимальної швидкодії з багатоядерного процесора Ryzen 9 на одній фізичній машині. Система не містить вбудованих інструментів розподіленого консенсусу (на кшталт Raft/Paxos) чи автоматичного шардингу між серверами.
* **Гарантія транзакційної стійкості (Durability Limits):** Локальне сховище історії записує повний JSON-документ через тимчасовий файл і `File.Move(..., overwrite: true)`. Це достатньо для demo history/restart recovery, але це не WAL, не `fsync`-сертифікація і не база даних для кожної окремої точки на частоті 500M+/сек.
* **Спрощений контур споживання даних:** Демо-версія системи зчитує локально підготовлені бінарні NEXRAD-файли. Підключення до реальних джерел NOAA в реальному часі (наприклад, AWS S3 buckets) вимагає окремого зовнішнього агента-завантажувача, що виходить за рамки продуктивності обчислювального ядра.
* **Пропускна здатність UI-клієнтів:** Operator UI (Angular) розрахований на роботу в кабіні одного або кількох операторів-аналітиків. Без використання зовнішніх зворотних проксі-серверів (Nginx/Yarp) локальний BFF-сервер не зможе ефективно обслуговувати тисячі одночасних веб-клієнтів.

---

## 🔍 Матеріали справи (Investigation Case Files)

### 1. Вердикт детективів (Decision Trace & Rationale)
Розробка консольного PowerShell/Bash дистрибутива під ключ (Віхи `032`-`033`). Для демонстрації та верифікації системи створено скрипт `radarpulse-product-demo.ps1` з командою `verify`, що автоматично збирає Angular, запускає тести, проганяє локальні дистрибутиви та робить перевірку дискового сховища.

#### Чому демо стало протоколом, а не інструкцією
Демо можна було зібрати як набір інструкцій у README, але тоді кожен рецензент став би сам собі release engineer. Можна було загорнути все в Docker, але для портфоліо-стенда це додало б ще один шар, який приховує реальні команди .NET, Angular і файлового сховища. Скрипт під ключ обрано як протокол допиту системи: одна команда піднімає, перевіряє й показує межі відповідальності. Ціна вибору — треба підтримувати cross-platform shell сценарії; виграш — демонстрація відтворюється без усних пояснень.

### 2. Закони фізики рантайму (System Invariants)
* **Кросплатформеність**: Скрипти PowerShell (`.ps1`) та Bash (`.sh`) мають видавати ідентичні результати на Windows та WSL2/Linux.
* **Режим заморозки (Freeze Mode)**: Демо-пакет працює як локальний release-freeze протокол без зовнішніх runtime-сервісів на кшталт брокера, бази даних чи cloud control plane.

### 3. Патологоанатомічний звіт (Failure Modes & Recovery)
* **Помилка верифікації**: Будь-яке падіння тесту (Angular, .NET або UI-smoke) миттєво зупиняє загальний скрипт верифікації з незручним кодом помилки (Exit Code != 0).

### 4. Слід доказової бази (Implementation & Tests)
* Скрипт демонстрації: [radarpulse-product-demo.ps1](../../../scripts/radarpulse-product-demo.ps1)
* Звіт про готовність: [product-demo-readiness.md](../../product-demo-readiness.md)

### 5. Протокол допиту процесу (Verification Commands)
Запуск повного циклу автоматичної верифікації демо-пакету:
```powershell
powershell -ExecutionPolicy Bypass -File scripts/radarpulse-product-demo.ps1 verify
```

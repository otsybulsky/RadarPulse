# Розділ 5: Архітектурні вартові

Уявіть, що ви встановили в банку суперсучасні броньовані двері та найняли кращих детективів для охорони сховища. Але один із касирів, поспішаючи на обід, просто підпер важкі двері цеглиною, щоб не витрачати час на введення пароля при поверненні. Або інший співробітник таємно прорубав маленьке віконце в задній стіні банку, щоб передавати документи напряму своєму приятелю з відділу доставки, минаючи пункт огляду.

У розробці програмного забезпечення такою «цеглиною в дверях» або «таємним віконцем» є випадкові інфраструктурні залежності, що пролазять у доменний шар. Сподіватися на те, що архітектурні межі будуть захищені виключно уважністю розробників на code review — це наївність, що межує з халатністю. Втомлений програміст о другій годині ночі обов'язково підключить якусь зовнішню бібліотеку прямо в домен, щоб швидко вирішити локальну проблему.

Тому в системі **RadarPulse** під час Віхи `036` (Clean Architecture Hardening) на стінах нашого доменного монастиря з'явилися автоматичні архітектурні вартові — тести в файлі [RadarPulseArchitectureTests.cs](../../../tests/RadarPulse.Tests/Architecture/RadarPulseArchitectureTests.cs).

## 5.1. Хто такі архітектурні тести?

Це спеціальний клас юніт-тестів, які перевіряють не правильність бізнес-обчислень, а цілісність самої структури нашого проекту. Вони виконуються автоматично при кожній збірці та запуску верифікаційного скрипта `radarpulse-product-demo.ps1 verify`. Якщо хтось порушив межі шарів, білд миттєво падає на етапі CI (Continuous Integration), вказуючи точне місце та характер злочину.

Наші архітектурні вартові виконують три ключові перевірки.

### Перевірка 1: Напрямок посилань у файлах проектів Csproj

Перший лінія оборони — це структура файлів конфігурації проектів `.csproj`. Ми повинні залізно гарантувати, що на рівні збірки проект `Domain` не посилається на інші проекти нашої системи.

Тест завантажує XML-структуру кожного файлу проекту та перевіряє тег `ProjectReference`. Ось спрощена версія коду, що виконує цю роботу:

```csharp
[Fact]
public void ProjectReferencesFollowCleanArchitectureDirection()
{
    // Domain повинен мати нуль залежностей від інших проектів системи
    AssertProjectReferences(
        @"src\Domain\RadarPulse.Domain.csproj",
        []);

    // Application може посилатися тільки на Domain
    AssertProjectReferences(
        @"src\Application\RadarPulse.Application.csproj",
        [@"..\Domain\RadarPulse.Domain.csproj"]);

    // Infrastructure посилається на Application
    AssertProjectReferences(
        @"src\Infrastructure\RadarPulse.Infrastructure.csproj",
        [@"..\Application\RadarPulse.Application.csproj"]);
}

private static void AssertProjectReferences(
    string relativeProjectPath,
    IReadOnlyCollection<string> expectedReferences)
{
    var projectPath = Path.Combine(RepositoryRoot, relativeProjectPath);
    var document = XDocument.Load(projectPath);

    var actualReferences = document
        .Descendants("ProjectReference")
        .Select(element => element.Attribute("Include")?.Value)
        .Where(include => !string.IsNullOrWhiteSpace(include))
        .ToArray();

    Assert.Equal(expectedReferences, actualReferences);
}
```

Якщо хтось спробує додати `<ProjectReference Include="..\Infrastructure\RadarPulse.Infrastructure.csproj" />` всередину файлу `RadarPulse.Domain.csproj` — цей тест миттєво провалиться.

### Перевірка 2: Контрабанда імпортів у вихідному коді (Source Code Check)

Але що робити, якщо посилань на рівні проектів немає, але розробник якимось дивним чином вирішив використати класи з інших шарів через механізм рефлексії або випадково імпортував зовнішні простори імен?

Наш другий архітектурний вартовий виконує роль митного інспектора. Він обходить усі файли вихідного коду C# (`*.cs`) в доменній директорії, читає їх рядок за рядком і шукає заборонені імпорти (`using`), такі як `using RadarPulse.Infrastructure` або `using Microsoft.AspNetCore`.

```csharp
[Fact]
public void DomainAndApplicationSourceDoNotReferenceOuterNamespaces()
{
    // Доменний шар не має права навіть згадувати інфраструктуру або веб-фреймворк
    AssertNoSourceReferences(
        @"src\Domain",
        "RadarPulse.Application",
        "RadarPulse.Infrastructure",
        "Microsoft.AspNetCore");
}

private static void AssertNoSourceReferences(
    string relativeDirectory,
    params string[] forbiddenTokens)
{
    var directory = Path.Combine(RepositoryRoot, relativeDirectory);
    var csFiles = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories);

    foreach (var file in csFiles)
    {
        var lines = File.ReadLines(file);
        int lineNumber = 0;
        foreach (var line in lines)
        {
            lineNumber++;
            foreach (var token in forbiddenTokens)
            {
                if (line.Contains(token, StringComparison.Ordinal))
                {
                    throw new Exception(
                        $"Архітектурний злочин у файлі {file} на рядку {lineNumber}! " +
                        $"Виявлено заборонене посилання на {token}.");
                }
            }
        }
    }
}
```

Цей тест працює як залізний детектор брехні: будь-яка контрабандна директива `using Microsoft.AspNetCore.Mvc` у домені призведе до негайного руйнування білду.

### Перевірка 3: Блокування таємного ходу `InternalsVisibleTo`

У мові C# є корисний, але небезпечний атрибут `InternalsVisibleTo`. Він дозволяє відкрити доступ до внутрішніх (`internal`) класів та методів збірки для іншої збірки. Зазвичай його використовують для того, щоб надати тестовим проектам доступ до нутрощів системи.

Але це величезна діра в безпеці архітектури. Якщо розробник пропише в домені:
`[assembly: InternalsVisibleTo("RadarPulse.Infrastructure")]`
він фактично прорубає таємні двері в стіні нашого монастиря, через які інфраструктура зможе маніпулювати внутрішнім станом домену, руйнуючи всю ізоляцію.

Наш третій вартовий перевіряє, щоб у файлах доменного шару не було жодної згадки про надання дружнього доступу інфраструктурним класам:

```csharp
[Fact]
public void DomainDoesNotGrantInfrastructureFriendAccess()
{
    var domainPath = Path.Combine(RepositoryRoot, @"src\Domain");
    var violations = Directory
        .EnumerateFiles(domainPath, "*.cs", SearchOption.AllDirectories)
        .Where(file => File.ReadAllText(file).Contains("InternalsVisibleTo(\"RadarPulse.Infrastructure\")"))
        .ToArray();

    Assert.Empty(violations); // Жодних привілеїв для інфраструктури!
}
```

Завдяки цим автоматичним тестам архітектура RadarPulse захищена не просто «добрими намірами» розробників, а залізними математичними твердженнями компілятора та тестового раннера.

Тепер, коли наші кордони під надійним захистом, давайте розберемося, які правила діють всередині самого доменного сховища. Як ми перевіряємо валідність даних, що прибувають на митницю? Про це — у наступному розділі.
---

## 🔍 Матеріали справи (Investigation Case Files)

### 1. Вердикт детективів (Decision Trace & Rationale)
Впровадження автоматичних архітектурних тестів на базі бібліотеки `NetArchTest` (Milestone `036`). Головне рішення — повне вилучення директиви `InternalsVisibleTo("RadarPulse.Infrastructure")` з проекту `Domain`. Це заблокувало лазівку для обходу інкапсуляції, змусивши інфраструктуру спілкуватися з доменом виключно через публічні інтерфейси.

### 2. Закони фізики рантайму (System Invariants)
* **Ізоляція внутрішнього стану**: Всі `internal` класи домену залишаються невидимими для інфраструктурного адаптера.
* **Автоматичний контроль**: Заборона посилань на сторонні фреймворки в проекті домену контролюється на рівні збірки рішення.

### 3. Патологоанатомічний звіт (Failure Modes & Recovery)
* **Порушення правила видимості**: Якщо розробник спробує додати `InternalsVisibleTo` або порушить посилання шарів, збірка проекту в релізі впаде на етапі запуску архітектурних тестів.

### 4. Слід доказової бази (Implementation & Tests)
* Файл конфігурації архітектурних тестів: [RadarPulseArchitectureTests.cs](../../../tests/RadarPulse.Tests/Architecture/RadarPulseArchitectureTests.cs)

### 5. Протокол допиту процесу (Verification Commands)
Запуск перевірки цілісності архітектурних кордонів:
```bash
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~RadarPulseArchitectureTests"
```

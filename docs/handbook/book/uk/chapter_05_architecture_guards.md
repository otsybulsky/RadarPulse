# Розділ 5: Архітектурні вартові

Уявіть, що ви встановили в банку суперсучасні броньовані двері та поставили найкращих детективів охороняти сховище. Але один із касирів, поспішаючи на обід, просто підпер важкі двері цеглиною, щоб не витрачати час на введення пароля при поверненні. Або інший співробітник таємно прорубав маленьке віконце в задній стіні банку, щоб передавати документи напряму своєму приятелю з відділу доставки, минаючи пункт огляду.

У розробці програмного забезпечення такою «цеглиною в дверях» або «таємним віконцем» є випадкові інфраструктурні залежності, що пролазять у доменний шар. Сподіватися на те, що архітектурні межі будуть захищені виключно уважністю розробників на code review — це наївність, що межує з халатністю. Втомлений програміст о другій годині ночі обов'язково підключить якусь зовнішню бібліотеку прямо в домен, щоб швидко вирішити локальну проблему.

Тому в системі **RadarPulse** під час Віхи `036` (Clean Architecture Hardening) на стінах нашого доменного монастиря з'явилися автоматичні архітектурні вартові — тести в файлі [RadarPulseArchitectureTests.cs](../../../../tests/RadarPulse.Tests/Architecture/RadarPulseArchitectureTests.cs).

## 5.1. Хто такі архітектурні тести?

Це спеціальний клас юніт-тестів, які перевіряють не правильність бізнес-обчислень, а цілісність самої структури нашого проекту. Вони виконуються під час окремого архітектурного gate і входять до packaged verify-команд `radarpulse-product-demo.ps1 verify` та `radarpulse-product-demo.sh verify`. Якщо хтось порушив межі шарів, верифікаційний gate падає з конкретним місцем і характером порушення.

Нижче — три базові guardrails кордонів. Після них розглянемо ще один клас перевірок: аналіз API-контрактів через рефлексію.

### Перевірка 1: Напрямок посилань у файлах проектів Csproj

Перша лінія оборони — це структура файлів конфігурації проектів `.csproj`. Ми фіксуємо весь граф project references: `Domain` не посилається на інші проекти системи, `Application` посилається тільки на `Domain`, `Infrastructure` — на `Application`, а `Presentation` збирає `Application` та `Infrastructure`.

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

Якщо хтось спробує додати `<ProjectReference Include="..\Infrastructure\RadarPulse.Infrastructure.csproj" />` всередину файлу [`RadarPulse.Domain.csproj`](../../../../src/Domain/RadarPulse.Domain.csproj) — цей тест миттєво провалиться.

### Перевірка 2: контрабанда імпортів у вихідному коді

Але що робити, якщо посилань на рівні проектів немає, а розробник випадково імпортував зовнішні простори імен або залишив у коді прямі згадки про зовнішній шар?

Наш другий архітектурний вартовий виконує роль митного інспектора. Він обходить файли вихідного коду C# (`*.cs`) у внутрішніх шарах `Domain` та `Application`, читає їх рядок за рядком і шукає заборонені текстові токени: `RadarPulse.Infrastructure`, `RadarPulse.Http`, `RadarPulse.Cli` або `Microsoft.AspNetCore`. Це не замінює повний статичний аналізатор C#, але добре ловить практичний клас порушень: випадкові `using`, прямі згадки зовнішніх namespace-ів і службові рядки, які не мають з'являтися всередині внутрішніх шарів.

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

Цей тест працює як детектор контрабанди для явних згадок у коді: будь-яка директива `using Microsoft.AspNetCore.Mvc` у домені перетвориться на відтворюване падіння архітектурного gate.

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

Завдяки цим автоматичним тестам архітектура RadarPulse захищена не лише «добрими намірами» розробників, а перевірками, які можна запустити локально й у CI.

## 5.2. Рефлексія .NET та автоматичний аналіз API-контрактів

Вартові нашого монастиря не обмежуються простою перевіркою XML-файлів проектів та пошуком ключових слів у тексті. Для найбільш тонких перевірок ми задіємо **рефлексію .NET (Reflection)**. Це дозволяє тестам аналізувати скомпільовані типи, конструктори та приватні поля прямо під час виконання.

Ця перевірка виконується у тесті [`ProductApiBoundaryIsOwnedByApplication`](../../../../tests/RadarPulse.Tests/Architecture/RadarPulseArchitectureTests.cs) та [`ProductApiContractDependsOnFocusedApplicationPorts`](../../../../tests/RadarPulse.Tests/Architecture/RadarPulseArchitectureTests.cs):
1. **Перевірка власності збірки:** Ми за допомогою рефлексії перевіряємо, що всі ключові інтерфейси (такі як [`IRadarPulseProductPipelineApi`](../../../../src/Application/Product/Pipeline/Contracts/RadarPulseProductPipelineContracts.cs) або [`IRadarPulseProductPipelineHistoryService`](../../../../src/Application/Product/Pipeline/Contracts/RadarPulseProductPipelineContracts.cs)) належать виключно збірці [`RadarPulse.Application`](../../../../src/Application/RadarPulse.Application.csproj). Це блокує можливість випадкового винесення бізнес-портів в інфраструктуру.
2. **Аналіз конструкторів та приватних полів:** Тест аналізує параметри конструктора класу [`RadarPulseProductPipelineApiContract`](../../../../src/Application/Product/Pipeline/Contracts/RadarPulseProductPipelineContracts.cs). Ми переконуємося, що він приймає лише сфокусовані доменні інтерфейси-порти, але не має прямих посилань на загальні сервіси-реалізації. Більше того, рефлексія обходить приватні поля (`BindingFlags.NonPublic | BindingFlags.Instance`), перевіряючи, що всередині класу не приховано жодної несанкціонованої залежності.

Такий динамічний аналіз скомпілованих збірок дає нам перевірювану впевненість у тому, що інтерфейсні контракти залишаються чистими та сфокусованими, а інфраструктурні деталі не просочуються через лазівки обходу типізації C#.

Тепер, коли наші кордони під надійним захистом, давайте розберемося, які правила діють всередині самого доменного сховища. Як ми перевіряємо валідність даних, що прибувають на митницю? Про це — у наступному розділі.
---

## Матеріали справи

### 1. Вердикт детективів
Впровадження автоматичних архітектурних тестів без зовнішньої бібліотеки `NetArchTest` (Milestone `036`). `NetArchTest` — це окрема .NET-бібліотека для декларативної перевірки архітектурних правил над збірками, але в RadarPulse вона не підключена. Наші вартові реалізовані вручну в [`RadarPulseArchitectureTests`](../../../../tests/RadarPulse.Tests/Architecture/RadarPulseArchitectureTests.cs): вони читають `.csproj` через `System.Xml.Linq`, обходять `.cs` файли в пошуку заборонених namespace-ів і використовують reflection для перевірки API-контрактів. Головне рішення — повне вилучення директиви `InternalsVisibleTo("RadarPulse.Infrastructure")` з проекту `Domain`. Це заблокувало лазівку для обходу інкапсуляції, змусивши інфраструктуру спілкуватися з доменом виключно через публічні інтерфейси.

#### Чому правила охороняють тести, а не пам'ять команди
Можна було залишити архітектурні правила в README і сподіватися на дисципліну рев'ю. Можна було дозволити `InternalsVisibleTo` як «тимчасовий службовий прохід», але такі проходи рідко закриваються самі. Ми обрали автоматичних вартових, бо межа архітектури має падати в тестах так само швидко, як падає помилкова бізнес-логіка. Ціна вибору — частина тестів перевіряє структуру, а не поведінку; виграш — порушення шарів стає не думкою рецензента, а відтворюваним фактом.

### 2. Закони фізики рантайму
* **Ізоляція внутрішнього стану**: Всі `internal` класи домену залишаються невидимими для інфраструктурного адаптера.
* **Автоматичний контроль**: Заборона посилань на зовнішні шари й веб-фреймворки у внутрішніх проектах контролюється архітектурним gate-ом і packaged verify-командами.

### 3. Патологоанатомічний звіт
* **Порушення правила видимості**: Якщо розробник спробує додати `InternalsVisibleTo` або порушить посилання шарів, architecture gate впаде під час запуску `RadarPulseArchitectureTests`; packaged verify зупиниться на кроці `.NET architecture boundary gate`.

### 4. Слід доказової бази
* Файл конфігурації архітектурних тестів: [RadarPulseArchitectureTests.cs](../../../../tests/RadarPulse.Tests/Architecture/RadarPulseArchitectureTests.cs)

### 5. Протокол допиту процесу
Запуск перевірки цілісності архітектурних кордонів:
```bash
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~RadarPulseArchitectureTests"
```

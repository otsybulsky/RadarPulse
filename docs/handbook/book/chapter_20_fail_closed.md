# Розділ 20: Запобіжник: зупинка без прихованої неправди (Fail-Closed)

Коли у складних, багатопотокових системах обробки великих даних стається збій, вони часто поводяться вкрай небезпечно. Вони продовжують приймати нові завдання від клієнтів, засмічують диски гігабайтами ідентичних логів помилок, безконтрольно накопичують гігабайти неробочих буферів в оперативній пам'яті (memory leak) та марно спалюють процесорний час на заздалегідь приречені обчислення. Це класична поведінка патерну **Fail-Open** (відкритий збій).

У системі RadarPulse реалізовано протилежний інженерний патерн — **зупинка без прихованої неправди (Fail-Closed)** (закритий збій, затверджений під час віхи `023`). Його мета не в тому, щоб виглядати героїчно. Він робить просту й дорогу річ: закриває прийом на вхідних межах, не дає помилковому результату пройти далі та змушує систему довести, що орендовані ресурси повернуті або що помилка очищення стала видимою.

---

## 20.1. Патерн герметичного відсіку

Уявіть себе капітаном підводного човна, який зазнав торпедної атаки. У правому борту утворилася пробоїна, вода хлинула у відсік №3. Що ви зробите?

Ви не будете тримати двері відчиненими й паралельно відкачувати воду, сподіваючись, що все якось стабілізується. Ви ізолюєте відсік. Так, частина корабля тимчасово недоступна, але вода не отримує права пройти в решту системи.

Це і є філософія fail-closed у RadarPulse. Якщо під час валідації радарного батча (наприклад, через порушення контрольної суми) або в процесі обчислень на фоновому потоці виникає критичний виняток, система не намагається «згладити кути». Вона діє за протоколом екстреної евакуації:

1. **Блокування входу (Closing the Intake):**
   Прийом нової роботи закривається на межі провайдера й керування (provider/control), а не прапорцем усередині durable-черги. [`RadarProcessingOwnedBatchQueue`](../../../src/Infrastructure/Processing/Queueing/Services/RadarProcessingOwnedBatchQueue/RadarProcessingOwnedBatchQueue.Lifecycle.cs) вміє переходити у closed/faulted стан (закритий або аварійний) і відхиляти нові enqueue/publish-операції. Fallback на product-рівні через [`RadarProcessingProductionPipelineControlCoordinator`](../../../src/Infrastructure/Processing/ProductPipeline/Services/RadarProcessingProductionPipelineControlCoordinator.cs) окремо фіксує режими `StopAccepting`, `DrainAccepted`, `CancelOpenAndRelease` або `RejectUnsafeFallback`. Durable-черга при цьому не є універсальним "закритим шлюзом": вона зберігає доказовий стан envelope-ів і робить його видимим для recovery.
2. **Кооперативне скасування воркерів (Cooperative Worker Cancellation):**
   Паралельні контури отримують `CancellationToken`, а archive/runtime loops регулярно перевіряють його на межах файлів, записів, батчів і dispatch-операцій. Важлива межа чесності: сам [`BZip2Workspace`](../../../src/Infrastructure/Archive/Compression/ReusableArchiveBZip2Decompressor/ReusableArchiveBZip2Decompressor.Workspace.cs) не приймає `CancellationToken` на кожне читання біта. Скасування працює на межі оркестрації (orchestration boundary), де ми можемо безпечно не починати наступний шмат роботи або не публікувати результат, який уже став неактуальним.
3. **Контрольоване очікування зливу (Graceful Shutdown & Task Drain):**
   Система не просто кидає фонові потоки напризволяще. Група асинхронних воркерів (Async worker group) має життєвий цикл для stop/dispose і може чекати завершення робіт через `Task.WhenAll`; окремо dispatch-рівень має політику тайм-ауту для batch-роботи. Це не універсальний менеджер production shutdown, але це достатній runtime-контракт для книги: прийом закрито, активна робота або завершується, або явно позначається як canceled/failed, а результат без права на фіксацію (`commit`) не просочується далі.
4. **Аварійне очищення буферів (Returning Buffers Cleanly):**
   Це найважливіша детективна складова. Оскільки воркери орендували великі масиви байтів та подій із загального пулу (`ArrayPool`), при виникненні аварії виникає ризик витоку пам'яті. Runtime-контури мають `finally`/`Dispose`-шляхи для активних дельт, completion-об'єктів і retained resources; readiness-логіка після цього дивиться не на добрі наміри, а на метрики. Якщо термінальний retained pressure не повернувся до нуля або release завершився помилкою, система не оголошує себе готовою. Успіх cleanup — це не побажання в тексті, а інваріант, який має бути доведений.
5. **Фіксація стану аварії (Readiness State):**
   Система фіксує опис помилки, blocking state, blocking sequence або рекомендацію оператору, а загальна готовність стає `IsReady = false`. Персистентний адаптер записує стан через temp-file replacement; це дає зрозумілий restart recovery шлях без обіцянки WAL/fsync-довговічності кожної мікроподії.

---

## 20.2. Чому ми вбили автоматичний відкат (No Silent Fallback)

Під час обговорення архітектури віхи `023` виникла спокуса реалізувати механізм «м'якого відновлення» — автоматичного переходу на спрощений режим обробки, наприклад тихий borrowed-provider fallback без використання прийнятого контуру retained resources.

Ми відкинули цю пропозицію не з любові до аварійних банерів, а через її приховану ціну:

```text
[Збій валідації батча]
         │
         ├──► [Автоматичний відкат (Silent Fallback)] ──► Приховування помилки, корупція даних! (REJECTED)
         │
         └──► [Аварійне блокування (Fail-Closed)] ─────► Зупинка, збереження доказів, безпека! (ACCEPTED)
```

Чому автоматичний безшумний відкат є злом?
* **Приховування доказів:** Якщо система тихо переключиться на альтернативний режим обробки, основний збій легко стане невидимим у звичайному робочому потоці (workflow). Помилка маскується, а розслідування починається пізніше й з гіршими даними.
* **Каскадне руйнування:** Намагаючись обійти помилку, система може продовжити записувати некоректно обчислені дані downstream-споживачам, тобто наступним споживачам у ланцюжку, що призведе до лавиноподібного зараження всієї аналітичної бази.

Наше правило: **якщо сталася помилка валідації черги, вона має залишатися видимою і переводити pipeline у заблокований стан.** Доступність (availability) у цьому місці поступається коректності (correctness), бо помилкова метрика дорожча за зупинку.

---

## 20.3. Очищення як доказ, а не обіцянка

На рівні тестів fail-closed не є одним монолітним сценарієм. Він розкладений на кілька доказових контурів:

* пошкоджений або несумісний durable-файл не запускає recovery, а дає `IsReady = false` і рекомендацію `InspectDurableAdapter`;
* відкриту роботу (open work) можна зупинити, злити або скасувати через product control, не втрачаючи видимий durable state;
* аварійна provider queue (faulted provider queue) відхиляє пізніший enqueue, а вже прийнятий залишок може отримати `SkippedAfterFault`;
* production gate перевіряє, що в успішному прийнятому контурі термінальні `TerminalRetainedBatchCount` і `TerminalRetainedPayloadBytes` дорівнюють нулю;
* якщо release/retention дає помилку або залишає terminal retained pressure, readiness має блокувати систему замість того, щоб оголосити її здоровою.

Головний критерій тут такий: **нульовий terminal retained pressure є умовою готовності, а не декоративною метрикою.** Якщо після аварійного або контрольованого закриття в системі лишилися утримані батчі чи payload-байти, це не "майже успіх". Це доказ блокування (blocking evidence), який має привести оператора до очищення або release-дії.

Тому fail-closed у RadarPulse захищає систему не лише від логічного хаосу, а й від фізичного виснаження ресурсів комп'ютера. Але він робить це чесно: не заявляє "усе гарантовано очистилося", а змушує систему показати доказ очищення або залишитися заблокованою.

---

## 🔍 Матеріали справи (Investigation Case Files)

### 1. Вердикт детективів (Decision Trace & Rationale)
Реалізація принципу безпечного відключення — **зупинка без прихованої неправди (Fail-Closed)** (Віха `023`). При критичній помилці вхідний контур перестає приймати нову роботу, durable state лишається видимим для recovery, а очищення ресурсів стає умовою готовності системи.

#### Чому система закривається, а не прикидається живою
Найпривабливіша версія для демонстрації — тихий fallback: пропустити зламаний пакет, перейти в простіший режим і не тривожити оператора. Але така система виглядає живою саме тоді, коли вже втратила правду. Інша крайність — миттєво вбити процес, залишивши воркерів і буфери з пулу в невідомому стані. Fail-closed обирає контрольоване закриття: нові справи не приймаються на вхідній межі, активні доробляються або коректно відпускають ресурси, причина блокується в telemetry. Ціна вибору — доступність поступається коректності; виграш — помилкова метрика не потрапляє у фінальний стан.

### 2. Закони фізики рантайму (System Invariants)
* **Retained Pressure при зупинці**: Кінцевий тиск утримуваної пам'яті має дорівнювати 0 байтів для ready-стану; ненульове значення є доказом блокування (blocking evidence).
* **Блокування прийому**: Новий батч відхиляється на межі провайдера й керування (provider/control), коли runtime-контур закритий, faulted або переведений у fallback/control mode.

### 3. Патологоанатомічний звіт (Failure Modes & Recovery)
* **Критичний збій pipeline**: При невідновлюваній помилці система закриває вхідні канали, зливає або скасовує активну роботу та переходить у захищений офлайн-режим із видимою причиною блокування.

### 4. Слід доказової бази (Implementation & Tests)
* Product control fallback: [RadarProcessingProductionPipelineControlCoordinator.cs](../../../src/Infrastructure/Processing/ProductPipeline/Services/RadarProcessingProductionPipelineControlCoordinator.cs)
* Provider queue lifecycle: [RadarProcessingOwnedBatchQueue.Lifecycle.cs](../../../src/Infrastructure/Processing/Queueing/Services/RadarProcessingOwnedBatchQueue/RadarProcessingOwnedBatchQueue.Lifecycle.cs)
* Тести аварійного завершення: [RadarProcessingProductionPipelineFallbackTests.cs](../../../tests/RadarPulse.Tests/Processing/ProductPipeline/RadarProcessingProductionPipelineFallbackTests.cs)
* Тести відновлення та зливу: [RadarProcessingProductionPipelineRecoveryTests.cs](../../../tests/RadarPulse.Tests/Processing/ProductPipeline/RadarProcessingProductionPipelineRecoveryTests.cs)
* Тести production gate та terminal retained pressure: [RadarProcessingProductionPipelineGateTests.cs](../../../tests/RadarPulse.Tests/Processing/ProductPipeline/RadarProcessingProductionPipelineGateTests.cs)
* Тести faulted queue / skipped-after-fault: [RadarProcessingQueuedRebalanceSessionTests.DrainFailureModes.cs](../../../tests/RadarPulse.Tests/Processing/Rebalance/RadarProcessingQueuedRebalanceSessionTests/RadarProcessingQueuedRebalanceSessionTests.DrainFailureModes.cs)

### 5. Протокол допиту процесу (Verification Commands)
Запуск верифікації сценаріїв безпечного аварійного відключення:
```bash
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~ProductionPipelineFallbackTests|FullyQualifiedName~ProductionPipelineRecoveryTests|FullyQualifiedName~ProductionPipelineGateTests"
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~RadarProcessingQueuedRebalanceSessionTests"
```

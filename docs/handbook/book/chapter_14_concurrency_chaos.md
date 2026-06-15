# Розділ 14: Хаос на іподромі паралелізму (Concurrency Chaos)

Коли ви запускаєте однопотокову обробку великих масивів даних, система схожа на камерний оркестр: усе входить у партитуру по черзі, кожна нота має своє місце. Але щойно ми відкриваємо паралелізм, порядок завершення перестає бути природним наслідком порядку входу. Воркери стартують із різною роботою, на різних ядрах, із різною поведінкою кешу, і фінішують так, як їм дозволяє runtime.

Паралельні обчислення дають швидкість, але забирають природну послідовність. Як тільки кілька воркерів починають обробляти радарні батчі одночасно, система стикається з феноменом позачергового завершення (out-of-order completion): пізніший batch може бути готовий раніше за попередній. Це серйозна загроза для будь-якої системи, що вимагає математичної та хронологічної точності.

---

## 14.1. Чому коні приходять врозкид: Анатомія часового розриву

Уявімо фізичну картину обробки метеорологічних радарів NEXRAD Level II. Наш вхідний потік розділений на окремі батчі stream events, зібрані з радарного архіву разом із payload slices. Кожному батчу присвоєно залізний порядковий номер провайдера — [`Provider Sequence`](../../../src/Domain/Processing/Queueing/Models/RadarProcessingQueuedBatchSequence.cs). На вході вони мають чіткий порядок: батч №1, №2, №3, №4.

Ми створюємо пул із кількох паралельних воркерів і роздаємо їм ці завдання. Здавалося б, оскільки завдання однотипні, вони мали б завершуватися приблизно одночасно. Але на практиці воркер, який отримав батч №4, може фінішувати на 100 мілісекунд раніше, ніж воркер, який мучиться з батчем №2. Чому так відбувається?

Детективне розслідування поведінки нашої системи під навантаженням виявило три головні причини цього хаосу:

1. **Нерівномірність корисного навантаження (Payload Variance):**
   Це найважливіший фактор. Метеорологічний радар сканує простір навколо себе, і різні фрагменти архівного потоку можуть містити різну кількість payload values, source-и з різною щільністю подій і різні обсяги маршрутизації. Воркеру доводиться читати payload slices, рахувати метрики та контрольні суми, маршрутизувати source-и по partitions/shards і формувати per-batch delta. Якщо батч №2 має більше роботи, він природно фінішує пізніше.
   У той же час батч №4 може містити легший payload. Воркер проходить менше значень, створює меншу дельту й завершує роботу раніше.

2. **Ігри планувальника ОС та ThreadPool (.NET Work Stealing):**
   Операційна система розподіляє процесорний час між потоками на власний розсуд, враховуючи наявність віртуальних логічних ядер (Hyper-Threading/SMT). Вона може перервати потік на одному ядрі для обслуговування системного переривання, уповільнивши обробку батчу №2.
   Навіть коли наша власна mailbox-модель чітко визначає, хто читає й хто пише, нижче все одно лишається планувальник ОС, `ThreadPool`, системні переривання та конкуренція за логічні ядра. Це не “помилка” runtime. Це нормальна ціна concurrency: виконання можна розпаралелити, але порядок завершення треба проектувати окремо.

3. **Архітектура пам'яті та кеш-промахи (CPU Cache and Memory Bottlenecks):**
   Сучасні багатоядерні процесори надзвичайно залежать від локального кешу (L1/L2/L3). Якщо воркери на різних ядрах намагаються зчитувати дані, які перетинаються в оперативній пам'яті, виникає конкуренція за шину даних. Один воркер змушений простоювати в очікуванні завантаження даних із повільної RAM, тоді як інший успішно читає все з гарячого кешу другого рівня і завершує роботу достроково.

В результаті воркер із батчем №4 може принести дельту раніше, ніж воркер із батчем №2. І саме тут архітектура має вирішити: ми плутаємо порядок, блокуємо всіх або вводимо окремий commit gate.

---

## 14.2. Небезпека позачергового комміту

Що станеться, якщо ми піддамося спокусі та дозволимо воркерам записувати результати обробки в спільне ядро ([`RadarProcessingCore`](../../../src/Domain/Processing/Core/Services/RadarProcessingCore/RadarProcessingCore.cs)) за принципом «хто перший встав, того й капці»?

На перший погляд це виглядає швидко: ніхто нікого не чекає, CPU зайнятий, результат публікується одразу. Але така швидкість купує неправильну властивість. Ми оптимізуємо час фінішу окремого воркера, а система потребує коректного фінального стану.

Дозволивши позачергові комміти (out-of-order commits), ми одразу руйнуємо кілька фундаментальних інваріантів системи:

* **Корупція часової послідовності (Chronological State Corruption):**
  Радарні вимірювання — це строго послідовні події в часі. Стан кожної радарної точки (джерела) оновлюється на основі попередніх вимірювань. Якщо ми застосуємо зміни від батча №4 (який стався пізніше в часі) до того, як застосуємо зміни від батча №2, ми отримаємо часовий парадокс. Система порахує метрики майбутнього стану на основі минулого, а потім спробує «втиснути» дані з минулого поверх майбутнього. Це призводить до порушення монотонності міток часу (source-local timestamp ordering violation). Наш детективний часовий ланцюжок розпадається.

* **Невизначеність кумулятивних показників (Nondeterministic Cumulative Metrics):**
  Уявіть, що ви ведете аудит фінансових транзакцій. Якщо ви почнете додавати відсотки на баланс рахунку до того, як на нього надійде саме тіло депозиту, підсумкова сума буде непередбачуваною. У RadarPulse накопичуються сумарні лічильники подій, суми контрольних сум (`RawValueChecksum`) та інші метрики. Якщо порядок коммітів змінюється від запуску до запуску через випадкові фактори планувальника ОС, контрольні суми перестануть сходитися. Кожен запуск програми видаватиме новий математичний результат. Детермінізм зникає.

* **Ризик під час міграції топології (Topology Migration Risk):**
  У контурах rebalance RadarPulse підтримує динамічну зміну карти розподілу шардів. Якщо топологія системи оновиться на кроці №3, а ми запишемо результати кроку №4, вирахувані на старій топології, ми отримаємо корупцію адресації подій. Дані просто «потечуть» не в ті шарди. Для rebalance це окрема наступна ordered-межа: topology/rebalance commit має отримати такий самий захист порядку, як і звичайний processing commit.

Отже, наш вердикт однозначний: **позачерговий запис результатів у спільне ядро заборонено.** Ми повинні зберегти повну свободу паралельних обчислень на етапі розрахунків, але гарантувати чітку provider-sequence послідовність на етапі фіксації результатів. Нам потрібен той, хто впорядкує цей хаос на фініші.

---

## 14.3. Commit gate: фінішна пряма з порядком

Рішення RadarPulse не в тому, щоб змусити воркерів фінішувати в правильному порядку. Це було б марною боротьбою з runtime. Рішення в тому, щоб розділити **compute** і **commit**.

На compute-етапі воркер отримує batch і рахує non-mutating delta: [`ComputeProcessingDelta()`](../../../src/Domain/Processing/Core/Services/RadarProcessingCore/RadarProcessingCore.Deltas.cs) будує результат роботи для batch-а, але не змінює спільний `RadarProcessingCore`. Це принципова межа: паралельні воркери можуть працювати одночасно, бо вони не переписують shared state.

Коли воркер завершує роботу, його результат потрапляє не прямо в core, а в [`RadarProcessingOrderedResultCoordinator`](../../../src/Infrastructure/Processing/Async/Services/RadarProcessingOrderedResultCoordinator.cs). Coordinator дивиться на `Provider Sequence` і публікує тільки той результат, чия черга справді настала. Якщо batch №4 готовий раніше, а batch №2 ще рахується, результат №4 чекає в pending buffer. Він не має права стати видимим раніше.

На commit-етапі commit path у [`RadarProcessingCore.Deltas.cs`](../../../src/Domain/Processing/Core/Services/RadarProcessingCore/RadarProcessingCore.Deltas.cs) перевіряє або застосовує delta проти поточного committed state: source-local timestamp ordering, topology version і shape contracts. Лише після цього shared state мутує, cumulative result публікується назовні, а retained resources можуть бути звільнені.

У скороченому вигляді це виглядає так:

```text
accepted batch
  -> provider sequence assigned
  -> worker computes non-mutating delta
  -> completion may arrive out of order
  -> ordered coordinator waits for next sequence
  -> ordered commit mutates shared state
  -> result becomes externally visible
```

Тому ключова формула цього розділу проста: **completion order is not commit order**. Паралельність отримує свободу на обчисленнях, але не отримує права самостійно змінювати спільний стан.

---

## 🔍 Матеріали справи (Investigation Case Files)

### 1. Вердикт детективів (Decision Trace & Rationale)
Дозвіл паралельного виконання батчів воркерами без взаємних блокувань (Milestone `021`). Воркери можуть завершувати обробку в іншому порядку через різний обсяг payload, scheduler noise і cache behavior. Тому RadarPulse розділив non-mutating compute і provider-sequence ordered commit: completion order не стає commit order.

#### Чому паралельність не отримала право комміту
Послідовна обробка була найпростішим способом зберегти порядок: один коридор, одна черга, жодних сюрпризів. Але тоді доступні CPU cores стояли б без роботи. Інший шлях — дозволити воркерам коммітити одразу після завершення, але це руйнує часовий ланцюг доказів. Ми обрали паралельне compute з окремим відновленням порядку на виході. Ціна вибору — потрібен coordinator і буфер для ранніх результатів; виграш — CPU працює паралельно, а фінальний стан лишається детермінованим.

### 2. Закони фізики рантайму (System Invariants)
* **Без блокувань в обчисленнях**: Воркери не мають права очікувати один одного під час розрахунку радарних дельт.
* **Commit тільки за provider sequence**: Shared `RadarProcessingCore` мутує лише через ordered commit, а не в порядку завершення воркерів.
* **Видимий runtime envelope**: `active=4` має зберігати детермінізм, completeness і clean retained pressure; elapsed/allocation ratio порівнюються з `active=1` як gate без обіцянки лінійного speedup.

### 3. Патологоанатомічний звіт (Failure Modes & Recovery)
* **Затримка важкого воркера**: Якщо воркер №2 застряг на важкому батчі, воркер №3 завершує роботу швидше, але його результат накопичується в pending buffer coordinator-а, щоб не порушити часову лінію.

### 4. Слід доказової бази (Implementation & Tests)
* Опис рантайму: [processing-runtime.md](../processing-runtime.md)
* Ordered result coordinator: [RadarProcessingOrderedResultCoordinator.cs](../../../src/Infrastructure/Processing/Async/Services/RadarProcessingOrderedResultCoordinator.cs)
* Ordered concurrent drain: [RadarProcessingQueuedProcessingSession.DrainOrderedConcurrent.cs](../../../src/Infrastructure/Processing/Queueing/Services/RadarProcessingQueuedProcessingSession/RadarProcessingQueuedProcessingSession.DrainOrderedConcurrent.cs)
* Non-mutating delta / ordered commit: [RadarProcessingCore.Deltas.cs](../../../src/Domain/Processing/Core/Services/RadarProcessingCore/RadarProcessingCore.Deltas.cs)
* Тести coordinator-а: [RadarProcessingOrderedResultCoordinatorTests.cs](../../../tests/RadarPulse.Tests/Processing/Async/RadarProcessingOrderedResultCoordinatorTests.cs)
* Тести ordered concurrent session: [RadarProcessingQueuedProcessingSessionOrderedConcurrentTests.cs](../../../tests/RadarPulse.Tests/Processing/Queueing/RadarProcessingQueuedProcessingSessionOrderedConcurrentTests.cs)

### 5. Протокол допиту процесу (Verification Commands)
Тестування паралельного виконання воркерів під навантаженням:
```bash
dotnet test tests/RadarPulse.Tests/RadarPulse.Tests.csproj --filter "FullyQualifiedName~RadarProcessingOrderedResultCoordinatorTests|FullyQualifiedName~RadarProcessingQueuedProcessingSessionOrderedConcurrentTests"
```

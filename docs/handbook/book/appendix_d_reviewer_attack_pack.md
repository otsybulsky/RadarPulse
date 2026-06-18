# Додаток 4: Набір атак рецензента

Цей додаток написаний для сильного рецензента. Його мета — не захищати книгу від критики, а зробити критику швидкою, точною і корисною.

Якщо після читання книги експерт не має питань, книга або занадто поверхова, або занадто самовпевнена. RadarPulse має витримати інший режим: рецензент атакує найсильніші твердження, а автор веде його до коду, тестів, перевірок і меж заявленого без довгих пояснень.

Повна змодельована сесія з уточнювальними питаннями, вердиктом і нотатками про продукційні докази винесена в [Додаток 5](appendix_e_simulated_hostile_reviewer_transcript.md). Цей набір атак лишається короткою картою питань; Додаток 5 показує, як має звучати захист.

## 30-хвилинний маршрут рецензента

Цей маршрут потрібен перед довгими додатками. Він не замінює лабораторний запуск і не просить вірити цифрам на слово. Його задача простіша: за пів години зрозуміти, чи книга варта глибокого рецензування, і які саме твердження треба атакувати першими.

| Час | Що відкрити | Що перевірити |
| :--- | :--- | :--- |
| 0-4 хв | [Передмова](preface_executive_verdict.md) | Чи заявлено межу: локальний лабораторний стенд, а не продукційна хмарна платформа |
| 4-9 хв | [Додаток 2](appendix_b_claim_evidence_matrix.md) | Чи кожне сильне твердження має код, тест, вимірювання й межу; чи незаявлені твердження названі явно |
| 9-16 хв | [Розділ 3](chapter_03_radar_batch.md) і [Розділ 12](chapter_12_pooled_copy.md) | Чи історія продуктивності спирається на компонування пам'яті, утримане володіння і докази віх, а не на лозунг “швидко” |
| 16-23 хв | [Розділ 16](chapter_16_mutable_core.md), [Розділ 17](chapter_17_stale_recompute.md), [Розділ 26](chapter_26_observability_logging.md) | Чи паралельність, топологія і діагностика мають мислення про режими відмов, а не тільки щасливий шлях |
| 23-30 хв | Цей набір атак нижче; за потреби — тільки секції `E.9`/`Є.9` в інструкціях для платформи | Які два-три питання варто поставити автору наживо і чи є маршрут до відтворення без приватних інструкцій |

Якщо після цього рецензент бачить тільки красиві слова, книгу треба зупиняти. Якщо бачить повторюваний ланцюг `твердження -> код -> тест -> вимірювання -> межа`, довгі додатки мають сенс: вони вже не “документація заради документації”, а протокол незалежної перевірки.

## Швидкий маршрут атаки

```text
[1. Вибрати сильне твердження]
              |
              v
[2. Знайти його в Додатку 2]
              |
              v
[3. Відкрити кодовий контракт]
              |
              v
[4. Запустити або переглянути сфокусовані тести]
              |
              v
[5. Перевірити вимірювання з віхи]
              |
              v
       +----------------+
       | Межа чесна?   |
       +----------------+
          | так      | ні
          v          v
 [Прийняти       [Вимагати м'якше
  твердження]     формулювання або
                  нового доказу]
```

## Найсильніші питання рецензента

| Атака | Що саме перевіряє | Куди йти в книзі | Куди йти в коді/доказах | Очікувана відповідь автора |
| :--- | :--- | :--- | :--- | :--- |
| “500M+ значень/с звучить як маркетинг. Де сирі докази?” | Чи пропускна здатність має віху, корпус і межу апаратного середовища | [Розділ 3](chapter_03_radar_batch.md), [Додаток 2](appendix_b_claim_evidence_matrix.md) | [закриття віхи 004](../../milestones/004-processing-core-input-contract-closeout.md), [RadarStreamContractTests.cs](../../../tests/RadarPulse.Tests/Streaming/Streams/RadarStreamContractTests.cs) | Це локальний бенчмарк на конкретному корпусі й апаратному середовищі, не універсальна сертифікація |
| “Я хочу сам зібрати логи продуктивності, а не читати вашу віху” | Чи історія продуктивності повторюється без автора на обраній платформі | [Додаток 6](appendix_f_lab_stand_bootstrap.md), [Додаток 7](appendix_g_lab_stand_linux.md), [Додаток 2](appendix_b_claim_evidence_matrix.md) | [CLI потокового бенчмарку архіву](../../../src/Presentation/RadarPulse.Cli/EntryPoint/RadarPulseCliApplication/ArchiveBenchmarkCliApplication/ArchiveBenchmarkCliApplication.StreamCommand.cs), [ProcessingBenchmarkCliApplication.cs](../../../src/Presentation/RadarPulse.Cli/EntryPoint/RadarPulseCliApplication/ProcessingBenchmarkCliApplication.cs), [докази продуктивності віхи 036](../../milestones/036-clean-architecture-hardening-performance-evidence.md) | Рецензент створює `data/perf/reviewer-*`, фіксує контур середовища, збірки й кешу та збирає сирі логи через `Tee-Object` на Windows або `tee` на Linux/macOS/WSL2; цифри лишаються локальними доказами |
| “Чому не zero-copy cast — пряме трактування байтів NEXRAD без копіювання?” | Чи автор розуміє межу бінарного формату | [Розділ 3](chapter_03_radar_batch.md) | [RadarEventBatchBuilder.cs](../../../src/Domain/Streaming/Batches/Services/RadarEventBatchBuilder/RadarEventBatchBuilder.cs), [RadarEventBatchBuilderTests](../../../tests/RadarPulse.Tests/Streaming/Batches/RadarEventBatchBuilderTests) | NEXRAD має байтовий порядок, змінне корисне навантаження і межу схеми; збирач нормалізує формат у доменний контракт |
| “98.97% зменшення алокацій не ховає інші витрати пам'яті?” | Чи твердження не ширше за вимірювання | [Розділ 11](chapter_11_allocation_anomaly.md), [Розділ 12](chapter_12_pooled_copy.md) | [перевірка продуктивності віхи 010](../../milestones/010-owned-provider-overlap-cost-reduction-performance-gate.md), [RadarProcessingRetainedPayloadFactory.PooledCopy.cs](../../../src/Infrastructure/Processing/Retention/Services/RadarProcessingRetainedPayloadFactory/RadarProcessingRetainedPayloadFactory.PooledCopy.cs) | Твердження тільки про контур утриманого корисного навантаження, не про загальні алокації процесу |
| “Чому active=4, тобто чотири активні воркери, не заявлено як прискорення?” | Чи автор не продає паралельність неправильно | [Розділ 14](chapter_14_concurrency_chaos.md), [Розділ 16](chapter_16_mutable_core.md) | [матриця віхи 021](../../milestones/021-ordered-concurrent-runtime-archive-processing-ordered-full-cache-performance-matrix.md), [project-progress.md](../../project-progress.md) | Основне твердження — коректність і обмежений податок; прискорення залежить від форми вузького місця |
| “Як ви довели, що криза спільного стану була реальною?” | Чи проблему не вигадано після факту | [Розділ 16](chapter_16_mutable_core.md) | [Віха 021: криза спільного стану](../../milestones/021-ordered-concurrent-runtime-archive-processing-slice-3-blocker.md), [RadarProcessingBatchDelta.cs](../../../src/Domain/Processing/Core/Models/RadarProcessingBatchDelta.cs) | Старий дизайн змішував обчислення і мутацію; дельта й впорядкована фіксація розірвали цей шлях |
| “Що стається, якщо топологія змінюється під час обчислення?” | Чи міграція топології має історію коректності | [Розділ 17](chapter_17_stale_recompute.md) | [матриця вузького місця обробки віхи 022](../../milestones/022-ordered-rebalance-topology-commit-processing-bottleneck-performance-matrix.md), [закриття віхи 022](../../milestones/022-ordered-rebalance-topology-commit-closeout.md) | Стара дельта не фіксується; перерахунок має виміряну ціну алокацій і диспетчеризації |
| “DurableEnvelope — це саморобна Kafka?” | Чи локальна черга не видається за брокер | [Розділ 18](chapter_18_durable_envelope.md), [Розділ 19](chapter_19_file_store.md) | [RadarProcessingDurableEnvelopeQueue.cs](../../../src/Infrastructure/Processing/Durable/Services/RadarProcessingDurableEnvelopeQueue/RadarProcessingDurableEnvelopeQueue.cs), [RadarProcessingFileDurableEnvelopeStore.cs](../../../src/Infrastructure/Processing/Durable/Stores/RadarProcessingFileDurableEnvelopeStore.cs) | Ні. Це брокер-нейтральний скінченний автомат і локальний файловий адаптер; продукційний адаптер брокера є окремим кроком посилення |
| “Файлове сховище справді стійке?” | Чи файлова персистентність не перебільшена | [Розділ 19](chapter_19_file_store.md), [Додаток 3](appendix_c_production_hardening.md) | [RadarProcessingFileDurableEnvelopeStore.cs](../../../src/Infrastructure/Processing/Durable/Stores/RadarProcessingFileDurableEnvelopeStore.cs), [закриття віхи 026](../../milestones/026-persistent-durable-adapter-readiness-closeout.md) | Це заміна через тимчасовий файл для локального перезапуску й відновлення, не WAL/fsync/стійкість бази даних |
| “Fail-closed — зупинка замість небезпечного продовження — не просто вбиває доступність?” | Чи автор розуміє компроміс між коректністю й доступністю | [Розділ 20](chapter_20_fail_closed.md) | [RadarProcessingProductionPipelineFallbackTests.cs](../../../tests/RadarPulse.Tests/Processing/ProductPipeline/RadarProcessingProductionPipelineFallbackTests.cs), [RadarProcessingProductionPipelineRecoveryTests.cs](../../../tests/RadarPulse.Tests/Processing/ProductPipeline/RadarProcessingProductionPipelineRecoveryTests.cs) | Так, доступність свідомо поступається коректності, бо хибна метрика дорожча за зупинку |
| “Користувацькі обробники не повертають хаос спільного стану?” | Чи модель розширення має контракт ізоляції | [Розділ 21](chapter_21_custom_handlers.md), [Розділ 22](chapter_22_delta_merge.md) | [IRadarSourceProcessingHandler.cs](../../../src/Domain/Processing/Handlers/Contracts/IRadarSourceProcessingHandler.cs), [RadarProcessingMvpRuntimePlan.cs](../../../src/Infrastructure/Processing/Runtime/Models/RadarProcessingMvpRuntimePlan.cs), [RadarProcessingHandlerDeltaMergeCoordinatorTests.cs](../../../tests/RadarPulse.Tests/Processing/Handlers/RadarProcessingHandlerDeltaMergeCoordinatorTests.cs) | Режим обробника керує шляхом рантайму: запасний шлях лише на знімку, блокування непідтриманого режиму, злитна дельта або злиття |
| “BFF/UI не приховують продукційні прогалини?” | Чи продуктова поверхня чесно називає незаявлені твердження | [Розділ 23](chapter_23_bff_shield.md), [Розділ 24](chapter_24_operator_ui.md) | [RadarPulseProductDemoReadiness.cs](../../../src/Presentation/RadarPulse.Http/Product/Readiness/RadarPulseProductDemoReadiness.cs), [RadarPulseProductHttpControlTests.cs](../../../tests/RadarPulse.Tests/Product/Http/RadarPulseProductHttpControlTests.cs), [app.spec.ts](../../../src/Presentation/OperatorUi/src/app/app.spec.ts) | BFF/UI є локальною операторською панеллю продукту; полотно наживо, автентифікація/TLS, оптимізація трафіку й публічний хостинг не заявлені |
| “Чи можна відтворити демо без автора?” | Чи книга завершується виконуваним сценарієм | [Розділ 25](chapter_25_demo_scripts.md) | [radarpulse-product-demo.ps1](../../../scripts/radarpulse-product-demo.ps1), [radarpulse-product-demo.sh](../../../scripts/radarpulse-product-demo.sh), [product-demo-readiness.md](../../product-demo-readiness.md) | Одна команда має провести рецензента маршрутом збірка/тести/швидка перевірка/готовність |
| “Де ваші продукційні логи?” | Чи спостережуваність не підмінена `Console.WriteLine` або красивим UI | [Розділ 26](chapter_26_observability_logging.md), [Додаток 3](appendix_c_production_hardening.md) | [RadarProcessingRunDiagnosticsReadModel.cs](../../../src/Application/Processing/ReadModels/RadarProcessingRunDiagnosticsReadModel.cs), [RadarProcessingProviderQueueTelemetrySummary.cs](../../../src/Domain/Processing/Queueing/Telemetry/RadarProcessingProviderQueueTelemetrySummary.cs), [RadarProcessingProductionPipelineOperatorSummary.Blocking.cs](../../../src/Infrastructure/Processing/ProductPipeline/Models/RadarProcessingProductionPipelineOperatorSummary/RadarProcessingProductionPipelineOperatorSummary.Blocking.cs) | Продукційне логування не заявлене; доведено типізований контракт діагностики й готовності, який має стати джерелом структурованих логів, метрик і трасувань |
| “Чи можна відтворити ваш кеш NEXRAD з нуля?” | Чи докази продуктивності й рантайму не залежать від приватної папки автора | [Додаток 6](appendix_f_lab_stand_bootstrap.md), [Додаток 7](appendix_g_lab_stand_linux.md), [Розділ 1](chapter_01_lab_table.md), [Розділ 25](chapter_25_demo_scripts.md) | [ArchiveCliApplication.Historical.cs](../../../src/Presentation/RadarPulse.Cli/EntryPoint/RadarPulseCliApplication/ArchiveCliApplication/ArchiveCliApplication.Historical.cs), [RadarPulseCliUsage.cs](../../../src/Presentation/RadarPulse.Cli/EntryPoint/RadarPulseCliApplication/RadarPulseCliUsage.cs), [історичний завантажувач віхи 001](../../milestones/001-historical-loader.md) | Так: публічні AWS Open Data -> маніфест -> детермінований `data/nexrad`; Windows і Linux мають окремі інструкції, а пропускна здатність лишається залежною від апаратного середовища й корпусу |

## Питання, на які автор не повинен відповідати “вже зроблено”

| Питання | Чесна відповідь |
| :--- | :--- |
| “Чи є продукційна автентифікація і TLS?” | Ні. Є локальна same-origin доставка, де UI та API мають одне походження; продукційна безпека — окремий крок посилення |
| “Чи є централізоване логування/OpenTelemetry?” | Ні. Є контракт діагностики, готовності й місткості; експортери та схема трасувань/логів — окремий крок посилення спостережуваності |
| “Чи є адаптер Kafka/RabbitMQ?” | Ні. Є брокер-нейтральна семантика конверта і локальний файловий адаптер |
| “Чи є приймання даних наживо з радарної мережі?” | Ні. Є архівний сценарій із повторним програванням і місце для адаптера приймання |
| “Чи потрібен приватний авторський кеш?” | Ні. Додатки Е/Є описують публічне початкове завантаження NEXRAD для Windows і Linux/macOS/WSL2; точні цифри продуктивності все одно залежать від корпусу й апаратного середовища |
| “Чи є справжня розподілена доставка рівно один раз?” | Ні. Є семантика “щонайменше один раз”, ідемпотентність, fail-closed мислення і явне незаявлене твердження |
| “Чи є радарне полотно 60 FPS?” | Ні. Поточний UI — панель читання моделей; візуалізація наживо потребує окремої перевірки DTO, транспорту й браузера |
| “Чи active=4, тобто чотири активні воркери, завжди швидше?” | Ні. Книга доводить коректність і виміряну межу; прискорення залежить від вузького місця конкретного навантаження |

## Як виглядає сильна відповідь на захисті

Слабка відповідь захищає кожне рішення як ідеальне. Сильна відповідь звучить інакше:

> “Ось що я довів. Ось де це в коді. Ось перевірка, яка це підтверджує. Ось межа твердження. Ось що я зробив би наступним, якби це стало продукційною вимогою.”

Саме такий режим технічного захисту ця книга має провокувати.

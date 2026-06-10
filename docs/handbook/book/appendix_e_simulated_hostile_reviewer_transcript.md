# Додаток Д: Simulated Hostile Reviewer Transcript

Цей документ є **симуляцією hostile technical review**, а не реальною зовнішньою рекомендацією і не цитатою конкретної людини. Його роль — підготувати книгу до найгіршого корисного сценарію: сильний principal/staff-level reviewer не вірить красивим формулюванням, атакує кожен великий claim і змушує автора показати код, тест, measurement та межу відповідальності.

Якщо реальний reviewer пізніше поставить інші питання, цей додаток треба оновити. Якщо реальний reviewer підтвердить або відхилить відповіді, це має бути окремий документ із явно позначеним джерелом.

## Session Setup

**Reviewer profile (simulated):** Principal backend/distributed systems engineer; сильний у .NET runtime, data pipelines, broker semantics, performance measurement, failure modes і production operations.

**Reviewer posture:** недовіра за замовчуванням. Будь-який claim без scope вважається перебільшенням. Будь-яка performance цифра без corpus/hardware boundary вважається маркетингом. Будь-який local durability claim без crash model вважається слабким.

**Author rule:** відповідати коротко, посилатися на доказ, називати межу claim-а, не захищати те, що не доведено.

**Reviewed material:** [Executive Verdict](preface_executive_verdict.md), [Додаток Б](appendix_b_claim_evidence_matrix.md), [Додаток В](appendix_c_production_hardening.md), [Додаток Г](appendix_d_reviewer_attack_pack.md), [Додаток Е](appendix_f_lab_stand_bootstrap.md), [Додаток Є](appendix_g_lab_stand_linux.md), розділи [3](chapter_03_radar_batch.md), [12](chapter_12_pooled_copy.md), [16](chapter_16_mutable_core.md), [17](chapter_17_stale_recompute.md), [18](chapter_18_durable_envelope.md), [19](chapter_19_file_store.md), [22](chapter_22_delta_merge.md), [24](chapter_24_operator_ui.md), [25](chapter_25_demo_scripts.md), [26](chapter_26_observability_logging.md).

## Exchange 1: “500M+ values/s sounds like a résumé number”

**Reviewer:**

Ви пишете про 500M+ payload values/s. Це типова цифра, яку часто виносять на титульний слайд без контексту. Де доказ, що це не synthetic toy benchmark, підігнаний під красивий headline?

**Author:**

Claim прив'язаний до Milestone 004 і не подається як production throughput. У [Додатку Б](appendix_b_claim_evidence_matrix.md) він має окремий scope: hardware/corpus-bound benchmark. Evidence: [004 closeout](../../milestones/004-processing-core-input-contract-closeout.md) фіксує `553_123_110.90` payload values/s для single-file normalized stream і `509_716_417.97` для cache-wide. Кодова опора — [RadarEventBatchBuilder.cs](../../../src/Domain/Streaming/Batches/Services/RadarEventBatchBuilder/RadarEventBatchBuilder.cs), контрактна опора — [RadarStreamContractTests.cs](../../../tests/RadarPulse.Tests/Streaming/Streams/RadarStreamContractTests.cs).

**Reviewer follow-up:**

Чому я маю вірити, що ця цифра має інженерне значення, якщо вона не production?

**Author:**

Тому що вона доводить не “система витримає будь-який продакшен”, а іншу властивість: input contract достатньо щільний, щоб не вбити runtime до того, як ми дійдемо до concurrency, retention і durable layers. Це baseline фізики даних. Production throughput потребував би live ingestion, broker/database, deployment і latency distribution gates; цього claim-а книга не робить.

**Reviewer verdict:**

Accepted with scope. Цифра сильна, якщо завжди повторювати corpus/hardware boundary.

**If production claim is needed:**

Потрібні live-like ingestion workload, deployment profile, latency percentiles, resource saturation, repeat runs and noise analysis.

## Exchange 2: “Why not zero-copy cast the NEXRAD bytes?”

**Reviewer:**

Якщо ви так дбаєте про hot path, чому не зробити `MemoryMarshal.Cast<byte, RadarStreamEvent>` і не прибрати builder?

**Author:**

Це було б швидко тільки для ідеального однорідного формату. У [Розділі 3](chapter_03_radar_batch.md) пояснено, чому NEXRAD не можна пустити прямо в домен: endian conversion, variable payload, зовнішня binary schema, payload references. Builder переводить зовнішній формат у наш `RadarEventBatch`, де offset/length і payload checksum проходять validation. Кодова точка — [RadarEventBatchBuilder.cs](../../../src/Domain/Streaming/Batches/Services/RadarEventBatchBuilder/RadarEventBatchBuilder.cs), тестова — [RadarEventBatchBuilderTests](../../../tests/RadarPulse.Tests/Streaming/Batches/RadarEventBatchBuilderTests).

**Reviewer follow-up:**

Тобто ви свідомо платите копіюванням?

**Author:**

Так, але контрольованим. Ми купуємо доменний контракт, deterministic payload references і незалежність від зовнішньої binary layout. Далі retained/pooled-copy layer оптимізує lifetime, але не скасовує boundary normalization.

**Reviewer verdict:**

Accepted. Це сильніше, ніж “zero-copy everywhere”, бо пояснює, де zero-copy був би неправильним.

## Exchange 3: “98.97% allocation reduction may be hiding total allocation”

**Reviewer:**

Фраза про 98.97% reduction легко звучить як “ми майже прибрали алокації”. Це правда для всього процесу?

**Author:**

Ні. Це claim тільки про retained payload contour. [Додаток Б](appendix_b_claim_evidence_matrix.md) прямо каже: not total zero allocation. Evidence — [010 performance gate](../../milestones/010-owned-provider-overlap-cost-reduction-performance-gate.md): `snapshot-copy` retained allocation `9_947_507_832` bytes, `pooled-copy` `102_811_264` bytes. Реалізація — [RadarProcessingRetainedPayloadFactory.PooledCopy.cs](../../../src/Infrastructure/Processing/Retention/Services/RadarProcessingRetainedPayloadFactory/RadarProcessingRetainedPayloadFactory.PooledCopy.cs).

**Reviewer follow-up:**

Чому тоді це важливо, якщо total allocation лишається?

**Author:**

Тому що bottleneck був не “будь-які bytes у процесі”, а довгоживучий retained payload після queued-owned decoupling. Snapshot-copy переносив занадто багато пам'яті через async boundary. Pooled-copy змінив ownership protocol: rent, enqueue, release. Це зняло конкретну кризу, не претендуючи на нульову купу.

**Reviewer verdict:**

Accepted with wording discipline. Завжди казати “retained payload allocation”, не “all allocations”.

## Exchange 4: “Prewarm can be benchmark cheating”

**Reviewer:**

Prewarm часто використовується, щоб сховати перший запуск. Ви просто прибрали cold-start cost із цифр?

**Author:**

Ні. [Розділ 13](chapter_13_cold_start.md) відділяє cold start від steady path. Evidence — [017 MeasureFile gate](../../milestones/017-file-level-default-readiness-and-cold-retained-ownership-cost-measurefile-gate.md): natural `138_151_728`, prewarmed `68_420_960`, borrowed `70_635_296`, pool misses `0`. Код — [RadarProcessingRetainedPayloadFactory.Prewarm.cs](../../../src/Infrastructure/Processing/Retention/Services/RadarProcessingRetainedPayloadFactory/RadarProcessingRetainedPayloadFactory.Prewarm.cs).

**Reviewer follow-up:**

Що заважає вам prewarm-ити половину світу й називати steady path дешевим?

**Author:**

Саме тому prewarm posture має бути explicit startup phase, а не прихована частина benchmark. У книзі це названо як ціна запуску, яку треба міряти окремо. Production version потребував би memory budget для prewarm і cold/warm dashboards.

**Reviewer verdict:**

Accepted. Не cheating, якщо startup cost залишається видимою.

## Exchange 5: “Your concurrency story avoids speedup claims. Is that weakness?”

**Reviewer:**

Ви багато говорите про `active=4`, але не кажете, що воно лінійно швидше. Це тому, що parallel runtime не дав speedup?

**Author:**

Ні, це тому, що правильний claim інший. [Розділ 14](chapter_14_concurrency_chaos.md) і [Розділ 16](chapter_16_mutable_core.md) доводять correctness under active batches і bounded tax. Evidence: [021 matrix](../../milestones/021-ordered-concurrent-runtime-archive-processing-ordered-full-cache-performance-matrix.md) має `active=4 elapsed ratio 0.994x` і steady allocation `1.006x`. Це flat/safe envelope, не universal acceleration. Speedup залежить від workload bottleneck; у stale topology bottleneck evidence [022 gate](../../milestones/022-ordered-rebalance-topology-commit-processing-bottleneck-performance-matrix.md) показує `0.891x`, але з allocation cost `1.137x`.

**Reviewer follow-up:**

Тоді навіщо active batches?

**Author:**

Щоб мати право перекривати compute без shared mutation і без порушення commit order. Це foundation для bottleneck-specific speedup. Спершу система має бути правильною при overlap; лише потім можна оптимізувати workload-specific throughput.

**Reviewer verdict:**

Accepted. Книга виграє від того, що не продає linear speedup.

## Exchange 6: “Shared mutable core blocker could be a post-hoc story”

**Reviewer:**

Slice 3 blocker звучить драматично. Як я знаю, що це не narrative після refactor-а?

**Author:**

Milestone trail фіксує blocker як decision point: [021 Slice 3 blocker](../../milestones/021-ordered-concurrent-runtime-archive-processing-slice-3-blocker.md). Проблема була не в “поганому lock”, а в тому, що `RadarProcessingCore` мав shared mutable state і cumulative reads. Рішенням став [RadarProcessingBatchDelta.cs](../../../src/Domain/Processing/Core/Models/RadarProcessingBatchDelta.cs): воркер рахує delta, ordered commit застосовує її до core.

**Reviewer follow-up:**

Чому не просто lock навколо core?

**Author:**

Lock навколо mutation захищає пам'ять, але може звести concurrency до serialized critical section і не розв'язує clean separation між compute і commit. Delta model робить паралельну фазу side-effect-light, а shared mutation лишає в одному commit gate, де порядок і validation контрольовані.

**Reviewer verdict:**

Accepted. Це реальний design correction, не просто “ми додали lock”.

## Exchange 7: “Stale topology recompute sounds expensive and fragile”

**Reviewer:**

Якщо topology змінюється під час compute, ви перераховуєте delta. Це може вибухнути worker dispatch count і allocation. Чи це не прихований denial-of-service?

**Author:**

Ціна не прихована. [Розділ 17](chapter_17_stale_recompute.md) і [022 processing-bottleneck matrix](../../milestones/022-ordered-rebalance-topology-commit-processing-bottleneck-performance-matrix.md) прямо називають: `39_292` worker dispatches for `32_000` logical batches, allocation ratio `1.137x`, elapsed `0.891x`. Correctness вибрано свідомо: stale route не має права на commit.

**Reviewer follow-up:**

Що не дає topology churn робити recompute нескінченним?

**Author:**

Тут вступають anti-churn/hysteresis policies із [Розділу 9](chapter_09_anti_churn.md) і topology version checks із [Розділу 8](chapter_08_topology_migration.md). Production hardening мав би додати explicit churn budget, alerting і worst-case stress gates.

**Reviewer verdict:**

Accepted with future hardening note. Correctness story сильна, але production потребує churn budget.

## Exchange 8: “DurableEnvelope looks like a homemade broker”

**Reviewer:**

Ви створили власний `DurableEnvelope`. Чому це не “not invented here” замість Kafka/RabbitMQ?

**Author:**

Бо це не replacement for Kafka. Це broker-neutral FSM, який описує states and transitions: `Pending`, `Claimed`, `Completed`, `Committed`, `Failed`, `Poison`. Код — [RadarProcessingDurableEnvelopeQueue.cs](../../../src/Infrastructure/Processing/Durable/Services/RadarProcessingDurableEnvelopeQueue/RadarProcessingDurableEnvelopeQueue.cs); тести — [RadarProcessingDurableEnvelopeQueueTests.cs](../../../tests/RadarPulse.Tests/Processing/Durable/RadarProcessingDurableEnvelopeQueueTests.cs). [Розділ 18](chapter_18_durable_envelope.md) прямо не заявляє distributed exactly-once. [Додаток В](appendix_c_production_hardening.md) каже, що broker adapter буде production hardening step після observability/database boundary.

**Reviewer follow-up:**

Чому не почати з Kafka і не отримати durability одразу?

**Author:**

Kafka дала б delivery primitives, але не дала б нам автоматично domain semantics: poison, provider sequence, ordered commit, handler delta replay, topology version. Спершу треба визначити семантику envelope-а, потім adapter can carry it.

**Reviewer verdict:**

Accepted. Не broker, а contract before broker.

## Exchange 9: “File durability is often overclaimed”

**Reviewer:**

Ваш file store пише JSON. Це не serious durability. Де WAL, fsync, transaction log?

**Author:**

Їх немає, і книга більше цього не стверджує. [Розділ 19](chapter_19_file_store.md) описує temp-file replacement через local adapter. Code: [RadarProcessingFileDurableEnvelopeStore.cs](../../../src/Infrastructure/Processing/Durable/Stores/RadarProcessingFileDurableEnvelopeStore.cs). Scope: local restart/recovery baseline, not WAL/fsync/database durability. Production plan у [Додатку В](appendix_c_production_hardening.md) вимагає database adapter або broker adapter gates перед production durability claim.

**Reviewer follow-up:**

Тобто crash during write може бути складним?

**Author:**

Так. Поточний contract захищає від частини simple corruption scenarios через temp-file replacement, але не претендує на full power-loss durability semantics. Саме тому це single-node demo/runtime contour.

**Reviewer verdict:**

Accepted because claim is now honest. Without that scope it would be rejected.

## Exchange 10: “Fail-closed can be an availability disaster”

**Reviewer:**

Fail-closed звучить красиво, але production users ненавидять зупинки. Чи не створили ви систему, яка надто легко падає?

**Author:**

Так, availability тут свідомо поступається correctness. [Розділ 20](chapter_20_fail_closed.md) описує trade-off: wrong metric гірша за visible stop. Тести: [RadarProcessingProductionPipelineFallbackTests.cs](../../../tests/RadarPulse.Tests/Processing/ProductPipeline/RadarProcessingProductionPipelineFallbackTests.cs), [RadarProcessingProductionPipelineRecoveryTests.cs](../../../tests/RadarPulse.Tests/Processing/ProductPipeline/RadarProcessingProductionPipelineRecoveryTests.cs). Production version потребував би SLO/risk budget, operator workflow, replay tools і alerting, але не silent fallback.

**Reviewer follow-up:**

Коли fail-open був би допустимим?

**Author:**

Для некритичних secondary projections або stale UI cache, якщо вони явно позначені degraded і не пишуть у canonical processing state. Для core metrics and ordered commit — ні.

**Reviewer verdict:**

Accepted. Good engineering judgment, if product stakeholders accept correctness-first posture.

## Exchange 11: “Custom handlers can destroy your invariants”

**Reviewer:**

Extension points часто стають діркою в архітектурі. Як custom handler не ламає core?

**Author:**

Handler отримує `RadarSourceProcessingHandlerContext` і `RadarSourceProcessingState`, обмежений slots і posture. Contract — [IRadarSourceProcessingHandler.cs](../../../src/Domain/Processing/Handlers/Contracts/IRadarSourceProcessingHandler.cs). Runtime plan — [RadarProcessingMvpRuntimePlan.cs](../../../src/Infrastructure/Processing/Runtime/Models/RadarProcessingMvpRuntimePlan.cs): snapshot-only handler веде до sequential fallback, unsupported blocks MVP processing, mergeable отримує ordered handler delta/merge path.

**Reviewer follow-up:**

А якщо handler має складний mutable state?

**Author:**

Тоді він не повинен автоматично отримати parallel fast path. Йому потрібен explicit delta/merge contract, tests of associativity/ordering assumptions, and performance gate. Це описано в [Розділі 22](chapter_22_delta_merge.md).

**Reviewer verdict:**

Accepted. Extension model консервативний, а не permissive.

## Exchange 12: “Handler delta/merge still has allocation debt”

**Reviewer:**

У Milestone 025 heavy handler active=4 має allocation ratio `2.612x`. Чому це не блокує інженерний висновок роботи?

**Author:**

Бо це не приховано. [Додаток Б](appendix_b_claim_evidence_matrix.md) прямо вносить `2.612x` як visible debt. Кодова точка — [RadarProcessingHandlerDeltaMergeCoordinator.cs](../../../src/Domain/Processing/Handlers/Services/RadarProcessingHandlerDeltaMergeCoordinator/RadarProcessingHandlerDeltaMergeCoordinator.cs); тести — [RadarProcessingHandlerDeltaMergeCoordinatorTests.cs](../../../tests/RadarPulse.Tests/Processing/Handlers/RadarProcessingHandlerDeltaMergeCoordinatorTests.cs), [RadarProcessingHandlerDeltaPerformanceGateTests](../../../tests/RadarPulse.Tests/Processing/Handlers/RadarProcessingHandlerDeltaPerformanceGateTests). У [Розділі 22](chapter_22_delta_merge.md) tree-merge позначено як future optimization, не виконаний claim.

**Reviewer follow-up:**

Що ви зробили б далі?

**Author:**

Почав би з allocation profile of `RadarProcessingHandlerDeltaValue` arrays, accumulator snapshots, dictionary churn, and heavy handler field cardinality. Наступний gate мав би порівнювати active=4 allocation after optimization against current `2.612x`, without weakening correctness.

**Reviewer verdict:**

Accepted as mature evidence. Debt named, not hidden.

## Exchange 13: “BFF and UI may look better than they are”

**Reviewer:**

UI часто продає систему краще, ніж вона є. Чи Operator UI не створює враження live radar cockpit?

**Author:**

[Розділ 24](chapter_24_operator_ui.md) прямо каже: current UI is read-model cockpit, not live-radar canvas. It uses Angular/HTTP product API, run history, diagnostics, readiness, handler outputs. Code: [Operator UI app](../../../src/Presentation/OperatorUi/src/app), [product-api.client.ts](../../../src/Presentation/OperatorUi/src/app/product/product-api.client.ts). Tests: [app.spec.ts](../../../src/Presentation/OperatorUi/src/app/app.spec.ts), [operator-ui.smoke.spec.ts](../../../src/Presentation/OperatorUi/smoke/operator-ui.smoke.spec.ts).

**Reviewer follow-up:**

А BFF optimization, compression, WebSocket?

**Author:**

Not claimed. [Розділ 23](chapter_23_bff_shield.md) and [Додаток В](appendix_c_production_hardening.md) put traffic benchmark, visual DTO, WebSocket/SSE and browser render gate into future hardening, not current work.

**Reviewer verdict:**

Accepted. UI boundary is honest.

## Exchange 14: “Demo scripts are not a substitute for real CI”

**Reviewer:**

`radarpulse-product-demo.ps1 verify` виглядає як локальний convenience script. Чому це evidence?

**Author:**

Тому що claim не “enterprise CI complete”, а “reviewer can reproduce local product/demo readiness route without author folklore”. Scripts: [radarpulse-product-demo.ps1](../../../scripts/radarpulse-product-demo.ps1), [radarpulse-product-demo.sh](../../../scripts/radarpulse-product-demo.sh). Evidence: [Розділ 25](chapter_25_demo_scripts.md), [product-demo-readiness.md](../../product-demo-readiness.md). It runs build/test/smoke/readiness route for the local demo.

**Reviewer follow-up:**

Що лишається для CI?

**Author:**

Decide which gates become CI-blocking versus release/manual because of hardware noise. Production CI also needs artifact signing, deployment stages, secret handling and environment-specific smoke tests.

**Reviewer verdict:**

Accepted. Good defense readiness, not deployment automation.

## Exchange 15: “Where are the production logs?”

**Reviewer:**

У книзі багато diagnostics/readiness, але я не бачу `ILogger`, OpenTelemetry, trace ids, centralized logs. Як я маю дебажити це о третій ночі?

**Author:**

Поточна книга не claim-ить production logging stack. [Розділ 26](chapter_26_observability_logging.md) прямо каже: немає готового `ILogger`/OpenTelemetry шару. Claim інший: RadarPulse уже має typed diagnostic/readiness contract, з якого такий шар має вирости. Code: [RadarProcessingRunDiagnosticsReadModel.cs](../../../src/Application/Processing/ReadModels/RadarProcessingRunDiagnosticsReadModel.cs), [RadarProcessingProviderQueueTelemetrySummary.cs](../../../src/Domain/Processing/Queueing/Telemetry/RadarProcessingProviderQueueTelemetrySummary.cs), [RadarProcessingProductionPipelineOperatorSummary.Blocking.cs](../../../src/Infrastructure/Processing/ProductPipeline/Models/RadarProcessingProductionPipelineOperatorSummary/RadarProcessingProductionPipelineOperatorSummary.Blocking.cs). Tests: [RadarProcessingRunReadModelTests.cs](../../../tests/RadarPulse.Tests/Processing/ReadModels/RadarProcessingRunReadModelTests.cs), [RadarProcessingProductionPipelineSummaryTests.cs](../../../tests/RadarPulse.Tests/Processing/ProductPipeline/RadarProcessingProductionPipelineSummaryTests.cs).

**Reviewer follow-up:**

Чому не додати logs first?

**Author:**

Logs-first легко перетворити на string soup. Спочатку потрібні stable facts: `runId`, provider sequence, topology version, envelope state, retained pressure, first blocking reason. Поточний код уже формує ці факти як typed summaries/read models. Production hardening step має експортувати їх у structured logs/metrics/traces, не тягнучи sink-и в Domain hot path.

**Reviewer verdict:**

Accepted with explicit gap. Good answer because it names the missing production layer instead of pretending diagnostics are centralized logging.

## Exchange 16: “Architecture tests can create false confidence”

**Reviewer:**

Architecture tests можуть перевіряти тільки `using` і project refs, але не якість дизайну. Чому ви ставите на них вагу?

**Author:**

Я не ставлю на них всю вагу. [Розділ 5](chapter_05_architecture_guards.md) і [RadarPulseArchitectureTests.cs](../../../tests/RadarPulse.Tests/Architecture/RadarPulseArchitectureTests.cs) доводять executable boundary: dependency direction, import constraints, guardrails. Це не замінює design review. [Додаток Б](appendix_b_claim_evidence_matrix.md) прямо каже: protects declared boundaries; does not replace design review.

**Reviewer follow-up:**

Що було б кращим наступним рівнем?

**Author:**

Architectural decision records tied to runtime gates, dependency graph snapshots, module ownership review, and “why not” documentation for major alternatives. Частина цього вже є в chapter rationale blocks and appendices, але automated tests alone are not enough.

**Reviewer verdict:**

Accepted. Good guardrail, if not oversold.

## Exchange 17: “Production hardening plan may be hand-wavy”

**Reviewer:**

У [Додатку В](appendix_c_production_hardening.md) ви описуєте production hardening. Чому це не wishlist?

**Author:**

Тому що він впорядкований навколо invariants and proof gates, not tools. For each layer — observability, database adapter, broker adapter, public API, live ingestion, multi-node — it names why, when, and first proof gate. It also says what not to do first: Kubernetes before SLOs, Kafka without envelope semantics, WebSocket before visual DTO/browser gate.

**Reviewer follow-up:**

Що першим робити, якщо завтра команда попросить production?

**Author:**

Observability contract first. Without run id, sequence id, topology version, retained pressure, retry/poison state and first blocking reason in logs/metrics/traces, every later broker/database/live-ingestion bug becomes harder to debug. Then choose database or broker adapter depending on the first real product pain.

**Reviewer verdict:**

Accepted. This is a production thinking plan, not a production claim.

## Exchange 18: “Can I recreate your lab cache without you?”

**Reviewer:**

Ви постійно посилаєтесь на `data/nexrad`, KTLX/KINX, full-cache matrices. Це ваша приватна папка? Якщо я не маю вашого SSD, я не можу перевірити claims.

**Author:**

Це більше не має бути приватним знанням. [Додаток Е](appendix_f_lab_stand_bootstrap.md) описує Windows/PowerShell bootstrap, а [Додаток Є](appendix_g_lab_stand_linux.md) описує Linux/macOS/WSL2 Bash bootstrap: prerequisites, `archive list`, manifest JSON, `archive download`, deterministic cache layout `data/nexrad/level2/{yyyy}/{MM}/{dd}/{radarId}/{fileName}`, smoke-cache і author-equivalent corpus. Обидва маршрути показують, як зібрати `data/perf/reviewer-*` evidence bundle: environment snapshot, Release build log, cache contour, full-cache benchmark logs і processing-only synthetic logs. Code path: [ArchiveCliApplication.Historical.cs](../../../src/Presentation/RadarPulse.Cli/EntryPoint/RadarPulseCliApplication/ArchiveCliApplication/ArchiveCliApplication.Historical.cs), CLI usage: [RadarPulseCliUsage.cs](../../../src/Presentation/RadarPulse.Cli/EntryPoint/RadarPulseCliApplication/RadarPulseCliUsage.cs), milestone source: [001 historical loader](../../milestones/001-historical-loader.md).

**Reviewer follow-up:**

Якщо я завантажу ті самі дати, я отримаю ті самі performance цифри?

**Author:**

Не гарантовано. Функціональна відтворюваність ідентифікується через manifest/cache/validation route. Performance цифри лишаються hardware/corpus-bound: CPU, SSD, OS, filesystem, scheduler, thermal state, parallelism і точний cache contour впливають на результат. Додатки Е/Є дають платформені шляхи перевірки й smoke/performance commands; вони не перетворюють локальний benchmark на cross-machine certification.

**Reviewer verdict:**

Accepted. The important improvement is that the cache is now reproducible from public data, while benchmark scope remains honest.

## Final Simulated Reviewer Verdict

**Accepted as a senior/principal-level engineering defense artifact with explicit scope.**

The strongest signal is not any single number. The strongest signal is the repeated pattern:

```text
claim -> code contract -> focused tests -> measurement -> scope boundary
```

The author shows:

* data-oriented design under real binary-format constraints;
* runtime memory discipline with measured allocation crises and fixes;
* concurrency correctness without pretending every parallel path is a speedup;
* failure-mode thinking that favors visible correctness over silent progress;
* extension design that blocks unsafe custom behavior instead of trusting discipline;
* product/demo packaging that names non-claims instead of hiding them;
* observability discipline that starts with typed diagnostics before claiming production logs;
* platform-specific lab-stand bootstrap that avoids private setup folklore;
* production thinking that preserves invariants before adding infrastructure.

**Remaining reservations:**

* Production broker/database adapters are not implemented.
* Public API security is not implemented.
* Live radar ingestion is not implemented.
* Handler delta/merge still has visible heavy-handler allocation debt.
* Benchmarks remain hardware/corpus-bound and should not be generalized.

**Defense interpretation:**

For an expert review that evaluates ownership of backend runtime, performance-sensitive data pipelines, clean architecture boundaries and technical evidence culture, this artifact is stronger than a broad oral quiz. A formal defense should move past foundational questions and focus on production trade-offs, risk budgets, external adapter design and how the author would run the next hardening milestone.

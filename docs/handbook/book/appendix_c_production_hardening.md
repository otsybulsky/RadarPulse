# Додаток В: Production Hardening Plan

Цей додаток не стверджує, що RadarPulse вже є production-платформою. Його завдання інше: показати, як автор переносив би доведений lab-table runtime у production, не ламаючи ті інваріанти, заради яких книга взагалі була написана.

Сильний production-план починається не з Kubernetes і не з Kafka. Він починається з питання: **яку властивість системи ми вже довели, і що саме може зламатися, коли ми винесемо її за межі одного процесу?**

## Вихідна межа

Поточна книга доводить локальний single-node contour:

```text
archive corpus -> streaming contract -> retained payload -> delta compute
-> ordered commit -> durable envelope/file store -> BFF read models
-> Operator UI -> demo readiness protocol
```

Це сильний контур для performance, memory discipline, concurrency correctness і failure-mode мислення. Але він навмисно не претендує на:

* live radar network ingestion;
* cross-machine delivery;
* public API security;
* broker/database durability certification;
* autoscaling, deployment automation, alert routing;
* exactly-once distributed processing.

Production hardening починається там, де ці non-claims стають реальними вимогами продукту.

## Інваріанти, які не можна втратити

| Інваріант | Чому він важливий | Де вже доведений |
| :--- | :--- | :--- |
| `RadarStreamEvent`/`RadarEventBatch` лишаються контрольованим hot-path контрактом | Інакше зовнішній формат знову пролізе в домен | [Розділ 3](chapter_03_radar_batch.md), [Додаток Б](appendix_b_claim_evidence_matrix.md) |
| `Provider Sequence` визначає commit order | Інакше паралельність почне міняти історію | [Розділ 14](chapter_14_concurrency_chaos.md), [Розділ 15](chapter_15_ordered_coordinator.md) |
| Compute і commit лишаються розділеними | Інакше повертається shared mutable core blocker | [Розділ 16](chapter_16_mutable_core.md) |
| Topology version перевіряється перед commit | Інакше stale route потрапить у core | [Розділ 17](chapter_17_stale_recompute.md) |
| Durable envelope описує state machine, а adapter лише зберігає її | Інакше broker/database почнуть диктувати доменну семантику | [Розділ 18](chapter_18_durable_envelope.md), [Розділ 19](chapter_19_file_store.md) |
| Failure має бути visible і fail-closed | Інакше система виглядатиме живою після втрати правди | [Розділ 20](chapter_20_fail_closed.md) |
| Handler-и проходять explicit posture | Інакше custom analytics повернуть race conditions | [Розділ 21](chapter_21_custom_handlers.md), [Розділ 22](chapter_22_delta_merge.md) |

Ці інваріанти є мостом між книгою і production. Усе, що не зберігає їх, є не hardening, а переписуванням системи з втратою доказів.

## Порядок введення production-шарів

| Крок | Що вводити | Чому саме тоді | Перший proof gate |
| :--- | :--- | :--- | :--- |
| 1 | Observability contract: structured logs, metrics, traces, run id, sequence id, topology version, retained pressure; починати з [Розділу 26](chapter_26_observability_logging.md) | Без цього production-збій не буде відтворюваним | Fault-injection run показує first blocking reason, retained pressure, retry/poison path і correlation id |
| 2 | Database adapter для run history/readiness або durable state | Коли локальний JSON перестає бути достатнім для audit, retention або multi-user access | Adapter contract tests: schema version, transaction boundary, duplicate replay, corrupted record handling |
| 3 | Broker adapter для `DurableEnvelope` | Коли producer/worker lifecycle виходить за межі одного процесу | At-least-once replay tests, duplicate delivery, poison quarantine, ordered commit after restart |
| 4 | Public API boundary: reverse proxy, TLS termination, authN/authZ, rate limits, CORS policy | Коли Operator UI перестає бути локальним cockpit | API security smoke, permission matrix, rejected unsafe control commands |
| 5 | Live ingestion adapter | Коли система має працювати з реальним зовнішнім потоком, а не replay corpus | Backpressure, malformed record, reconnect, corpus drift і latency budget gates |
| 6 | Multi-node processing | Тільки після broker/database/observability, бо інакше немає fencing і recovery story | Partition ownership fencing, topology lease, stale recompute parity, node-kill scenario |

Цей порядок навмисно консервативний. Він не максимізує кількість технологій у діаграмі; він мінімізує шанс втратити вже доведену коректність.

## Adapter Decision Table

| Якщо болить | Перший production-напрям | Чому не одразу інший шлях |
| :--- | :--- | :--- |
| Потрібен audit trail запусків для кількох операторів | SQL database adapter для product history/readiness | Broker не вирішує query/history; NoSQL може бути доречним пізніше, але спершу потрібна transactional audit shape |
| Потрібна доставка між процесами | Broker adapter над `DurableEnvelope` | Не можна просто “підключити Kafka” і забути FSM: retry, poison і commit boundary мають лишитися нашими |
| Потрібен публічний доступ до UI | Reverse proxy + auth + TLS + explicit API policy | Angular polish без security boundary створює красиву, але відкриту панель керування |
| Потрібен live radar stream | Ingestion adapter з backpressure і validation quarantine | Live input без failure quarantine швидко перетворить parser errors на runtime noise |
| Потрібне горизонтальне масштабування | Partition ownership/fencing service | Multi-node без fencing робить topology migration небезпечнішою, ніж single-node bottleneck |

## Gates, які мають з'явитися перед production claim

| Claim, який хочеться зробити | Gate, без якого claim нечесний |
| :--- | :--- |
| “Broker-backed delivery works” | Kill/restart worker during claimed envelope, duplicate delivery replay, poison after retry limit, ordered commit recovery |
| “Database-backed history is durable” | Crash between temp/transaction stages, schema migration, corrupted row/document handling, concurrent reader/writer |
| “Public API is safe enough for demo users” | Authenticated control command matrix, rejected unauthorized stop/drain/cancel, CORS/TLS/reverse-proxy smoke |
| “Live ingestion is supported” | Reconnect, malformed frame, slow consumer, backpressure, corpus drift, latency distribution |
| “Multi-node processing is correct” | Node loss, partition fencing, topology version conflict, stale recompute parity, checksum equality with single-node baseline |
| “BFF traffic is optimized” | DTO size budget, serialization time, browser render cost, realistic operator workflow trace |

## What Not To Do First

The easiest way to make RadarPulse look bigger and weaker at the same time would be:

* put the current process into Kubernetes before defining SLOs;
* replace the local file store with Kafka without preserving envelope semantics;
* add WebSocket visualization before defining visual DTOs and browser performance gates;
* add auth screens without authorization semantics for dangerous controls;
* call a broker/database adapter “production-ready” after only happy-path tests.

The book earns trust because it does not take those shortcuts. A production version should keep that habit.

## Defense Signal

This plan is deliberately specific because senior production work is not “add cloud”. It is the discipline of carrying proven invariants across new failure modes. The author has already shown the lab-table version of that discipline. This appendix shows the next defense conversation: what to harden first, what to defer, and what evidence would be required before a production claim becomes honest.

import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';

import { App } from './app';
import { RadarPulseProductApiClient } from './product/product-api.client';
import { RADARPULSE_PRODUCT_API_BASE_URL_STORAGE_KEY } from './product/product-api.config';
import {
  ProductApiResponse,
  ProductHandlerOutput,
  ProductRunDetail,
  ProductRunHistoryReadiness,
  ProductRunSummary,
} from './product/product-api.models';

describe('App', () => {
  let api: ProductApiStub;

  beforeEach(async () => {
    localStorage.removeItem(RADARPULSE_PRODUCT_API_BASE_URL_STORAGE_KEY);
    history.replaceState(null, '', '/');
    api = new ProductApiStub();

    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        {
          provide: RadarPulseProductApiClient,
          useValue: api,
        },
      ],
    }).compileComponents();
  });

  it('renders readiness and empty persisted run state', async () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('History is available for product reads.');
    expect(compiled.textContent).toContain('No persisted runs yet.');
  });

  it('renders run list and selected latest run', async () => {
    api.runs = [summary('run-1')];
    api.latest = detail('run-1');

    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('run-1');
    expect(compiled.textContent).toContain('Selected run');
  });

  it('runs deterministic demo workflow and refreshes runs', async () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    await fixture.whenStable();

    const form = fixture.nativeElement.querySelector('.command-form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(api.demoRunRequested).toBe(true);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('demo-created');
  });

  it('renders host connection failure state', async () => {
    api.readinessError = new HttpErrorResponse({ status: 0 });

    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('network-error');
  });

  it('stores API base URL override and refreshes host state', async () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    await fixture.whenStable();

    const input = fixture.nativeElement.querySelector('input[name="apiBaseUrl"]') as HTMLInputElement;
    input.value = 'http://localhost:6117/';
    input.dispatchEvent(new Event('input'));
    clickButton(fixture.nativeElement, 'Apply host URL');
    await fixture.whenStable();

    expect(localStorage.getItem(RADARPULSE_PRODUCT_API_BASE_URL_STORAGE_KEY)).toBe('http://localhost:6117');
  });

  it('rejects invalid API base URL without refreshing host state', async () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    await fixture.whenStable();

    const readinessRequests = api.readinessRequestCount;
    const input = fixture.nativeElement.querySelector('input[name="apiBaseUrl"]') as HTMLInputElement;
    input.value = 'localhost:6117';
    input.dispatchEvent(new Event('input'));
    clickButton(fixture.nativeElement, 'Apply host URL');
    fixture.detectChanges();

    expect(api.readinessRequestCount).toBe(readinessRequests);
    expect(localStorage.getItem(RADARPULSE_PRODUCT_API_BASE_URL_STORAGE_KEY)).toBeNull();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'Use an absolute http:// or https:// URL.',
    );
  });

  it('loads selected run and active tab from URL state', async () => {
    history.replaceState(null, '', '/?runId=deep-run&tab=diagnostics');

    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(api.requestedRunId).toBe('deep-run');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('ordered merge');
  });

  it('updates URL state when selecting a run and tab', async () => {
    api.runs = [summary('run-url')];
    api.latest = detail('run-url');

    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    clickButton(fixture.nativeElement, 'run-url');
    clickButton(fixture.nativeElement, 'Capacity');

    const url = new URL(location.href);
    expect(url.searchParams.get('runId')).toBe('run-url');
    expect(url.searchParams.get('tab')).toBe('capacity');
  });

  it('shows not-found posture when URL state selects a missing run', async () => {
    history.replaceState(null, '', '/?runId=missing-run&tab=sources');
    api.missingRunIds.add('missing-run');

    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('missing-run');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Run was not found.');
  });

  it('renders run inspection tabs for diagnostics and capacity evidence', async () => {
    api.runs = [summary('run-detail')];
    api.latest = detail('run-detail');

    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    clickButton(fixture.nativeElement, 'Diagnostics');
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('ordered merge');

    clickButton(fixture.nativeElement, 'Capacity');
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('production');
  });

  it('distinguishes handler output value from absent handler output', async () => {
    api.runs = [summary('run-handler')];
    api.latest = detail('run-handler');

    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    clickButton(fixture.nativeElement, 'Handlers');
    fixture.detectChanges();
    clickButton(fixture.nativeElement, 'Load handler output');
    await fixture.whenStable();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('benchmark.events');

    api.handlerOutput = null;
    clickButton(fixture.nativeElement, 'Load handler output');
    await fixture.whenStable();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Handler output was not found.');
  });

  it('validates archive and handler lookup inputs before HTTP requests', async () => {
    api.runs = [summary('run-handler')];
    api.latest = detail('run-handler');

    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const archiveForm = fixture.nativeElement.querySelector('.archive-form') as HTMLFormElement;
    archiveForm.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(api.archiveRunRequested).toBe(false);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'Archive file path is required.',
    );

    clickButton(fixture.nativeElement, 'Handlers');
    fixture.detectChanges();

    const handlerField = fixture.nativeElement.querySelector(
      'input[name="handlerField"]',
    ) as HTMLInputElement;
    handlerField.value = '';
    handlerField.dispatchEvent(new Event('input'));
    clickButton(fixture.nativeElement, 'Load handler output');
    fixture.detectChanges();

    expect(api.handlerOutputRequested).toBe(false);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'Handler field name is required.',
    );
  });

  it('sends operator controls and renders unsafe fallback rejection', async () => {
    api.runs = [summary('run-control')];
    api.latest = detail('run-control');

    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const durableInput = fixture.nativeElement.querySelector(
      'input[name="durableStorePath"]',
    ) as HTMLInputElement;
    durableInput.value = 'durable.json';
    durableInput.dispatchEvent(new Event('input'));

    clickButton(fixture.nativeElement, 'Stop accepting');
    await fixture.whenStable();
    expect(api.lastControlAction).toBe('StopAccepting');

    clickButton(fixture.nativeElement, 'Drain accepted');
    await fixture.whenStable();
    expect(api.lastControlAction).toBe('DrainAccepted');

    clickButton(fixture.nativeElement, 'Cancel and release');
    await fixture.whenStable();
    expect(api.lastControlAction).toBe('CancelOpenAndRelease');

    clickButton(fixture.nativeElement, 'Reject unsafe fallback');
    await fixture.whenStable();
    fixture.detectChanges();

    expect(api.lastControlAction).toBe('RejectUnsafeFallback');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('borrowed fallback requested');
  });

  it('disables operator controls while the host is unreachable', async () => {
    api.readinessError = new HttpErrorResponse({ status: 0 });
    api.runs = [summary('run-control')];
    api.latest = detail('run-control');

    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const rejectButton = findButton(fixture.nativeElement, 'Reject unsafe fallback');
    expect(rejectButton.disabled).toBe(true);
  });
});

class ProductApiStub {
  runs: readonly ProductRunSummary[] = [];
  latest: ProductRunDetail | null = null;
  readinessError: HttpErrorResponse | null = null;
  missingRunIds = new Set<string>();
  readinessRequestCount = 0;
  demoRunRequested = false;
  archiveRunRequested = false;
  handlerOutputRequested = false;
  requestedRunId = '';
  lastControlAction = '';
  handlerOutput: ProductHandlerOutput | null = {
    handlerIndex: 0,
    handlerName: 'counter-checksum',
    name: 'benchmark.events',
    type: 'Int64',
    int64Value: 42,
    doubleValue: 0,
  };

  getHistoryReadiness() {
    this.readinessRequestCount += 1;
    if (this.readinessError) {
      return throwError(() => this.readinessError);
    }

    return of(ok<ProductRunHistoryReadiness>({
      storageKind: 2,
      isReady: true,
      storageIdentity: 'history.json',
      schemaVersion: 1,
      loadedRunCount: this.runs.length,
      rejectedRunCount: 0,
      firstBlockingReason: '',
      warnings: [],
    }));
  }

  listRuns() {
    return of(ok(this.runs));
  }

  getLatestRun() {
    return this.latest
      ? of(ok(this.latest))
      : throwError(() => new HttpErrorResponse({
        status: 404,
        error: {
          statusCode: 404,
          isSuccess: false,
          body: null,
          message: 'No latest run is available.',
        },
      }));
  }

  getRun(runId: string) {
    this.requestedRunId = runId;
    if (this.missingRunIds.has(runId)) {
      return throwError(() => new HttpErrorResponse({
        status: 404,
        error: {
          statusCode: 404,
          isSuccess: false,
          body: null,
          message: 'Run was not found.',
        },
      }));
    }

    return of(ok(detail(runId)));
  }

  runDemo() {
    this.demoRunRequested = true;
    const created = detail('demo-created');
    this.latest = created;
    this.runs = [created.summary];
    return of({
      ...ok(created),
      statusCode: 201,
    });
  }

  runArchive() {
    this.archiveRunRequested = true;
    return of(ok(detail('archive-created')));
  }

  getHandlerOutput() {
    this.handlerOutputRequested = true;
    return this.handlerOutput
      ? of(ok(this.handlerOutput))
      : throwError(() => new HttpErrorResponse({
        status: 404,
        error: {
          statusCode: 404,
          isSuccess: false,
          body: null,
          message: 'Handler output was not found.',
        },
      }));
  }

  stopAccepting() {
    return this.control('StopAccepting');
  }

  drainAccepted() {
    return this.control('DrainAccepted');
  }

  cancelOpenAndRelease() {
    return this.control('CancelOpenAndRelease');
  }

  rejectUnsafeFallback() {
    return this.control('RejectUnsafeFallback', false, 'borrowed fallback requested');
  }

  private control(action: string, ready = true, warning = '') {
    this.lastControlAction = action;
    return of(ok({
      runId: 'run-control',
      action,
      operatorSummary: {
        ...operatorSummary(),
        isReady: ready,
        firstBlockingReason: ready ? '' : warning,
        warnings: warning ? [warning] : [],
      },
      canceledOpenCount: action === 'CancelOpenAndRelease' ? 1 : 0,
      releasedCanceledCount: action === 'CancelOpenAndRelease' ? 1 : 0,
      drainedProcessingCount: action === 'DrainAccepted' ? 1 : 0,
      message: '',
    }));
  }
}

function ok<T>(body: T): ProductApiResponse<T> {
  return {
    statusCode: 200,
    isSuccess: true,
    body,
    message: '',
  };
}

function summary(runId: string): ProductRunSummary {
  return {
    runId,
    input: {
      kind: 1,
      description: 'synthetic demo',
      source: 'demo',
      batchCount: 2,
      eventCount: 4,
    },
    state: 4,
    isReady: true,
    hasReadModel: true,
    handlerMode: 3,
    firstBlockingReason: '',
    fallbackRecommendation: 1,
    batchCount: 2,
    sourceCount: 2,
    acceptedBatchCount: 2,
    processedBatchCount: 2,
    committedBatchCount: 2,
    warningCount: 0,
  };
}

function detail(runId: string): ProductRunDetail {
  const runSummary = summary(runId);

  return {
    summary: runSummary,
    configuration: {
      profileName: 'production',
      isValid: true,
      firstInvalidOption: null,
      firstInvalidReason: null,
      values: [],
      warnings: [],
    },
    operatorSummary: operatorSummary(),
    capacityEvidence: {
      runId,
      profileName: 'production',
      elapsedMilliseconds: 1,
      measuredAllocatedBytes: 100,
      acceptedBatchCount: 2,
      processedBatchCount: 2,
      committedBatchCount: 2,
      handlerMode: 3,
      durableAdapterKind: 'file',
      terminalRetainedBatchCount: 0,
      terminalRetainedPayloadBytes: 0,
      processingCompletenessPassed: true,
      isReady: true,
      firstBlockingReason: '',
      configurationContour: 'default',
    },
    diagnostics: {
      processingCompletenessPassed: true,
      isReady: true,
      blockingReason: '',
      handlerOutputProvenance: 'ordered merge',
      usesOrderedHandlerDeltaMerge: true,
      usesSequentialHandlerFallback: false,
      handlerOutputBlocked: false,
      releaseFailureCount: 0,
      terminalRetainedEnvelopeCount: 0,
      terminalRetainedPayloadBytes: 0,
      currentRetainedBatchCount: 0,
      currentRetainedPayloadBytes: 0,
      warnings: [],
    },
    handlerContract: {
      statePosture: 'mergeable',
      message: 'handler output available',
      firstBlockingReason: null,
      isBlocked: false,
      handlers: [
        {
          handlerIndex: 0,
          name: 'counter-checksum',
          int64SlotCount: 1,
          doubleSlotCount: 0,
          executionClassification: 'mergeable',
          fields: [
            {
              handlerIndex: 0,
              handlerName: 'counter-checksum',
              name: 'benchmark.events',
              type: 'Int64',
              slotIndex: 0,
            },
          ],
        },
      ],
    },
    batches: [
      {
        providerSequence: 1,
        wasAccepted: true,
        streamEventCount: 2,
        payloadBytes: 4,
        payloadValueCount: 4,
        rawValueChecksum: 10,
        processingStatus: 'Committed',
        isSuccessful: true,
        message: '',
        topologyVersion: 1,
      },
    ],
    sources: [
      {
        identity: {
          sourceId: 0,
          radarOrdinal: 0,
          elevationSlot: 0,
          azimuthBucket: 0,
          rangeBand: 0,
        },
        isActive: true,
        processedEventCount: 2,
        processedPayloadValueCount: 4,
        rawValueChecksum: 10,
        lastMessageTimestampUtcTicks: 100,
        processingChecksum: 12,
        handlerValues: [],
      },
    ],
    message: '',
    runId,
    isReady: true,
    hasReadModel: true,
  };
}

function clickButton(root: Element, label: string): void {
  findButton(root, label).dispatchEvent(new Event('click'));
}

function findButton(root: Element, label: string): HTMLButtonElement {
  const buttons = Array.from(root.querySelectorAll('button'));
  const button = buttons.find(candidate => candidate.textContent?.trim() === label);

  if (!button) {
    throw new Error(`Button not found: ${label}`);
  }

  return button;
}

function operatorSummary() {
  return {
    runState: 4,
    isReady: true,
    processingComplete: true,
    handlerMode: 3,
    hasHandlerConflict: false,
    handlerBlockingReason: '',
    firstBlockingReason: '',
    fallbackRecommendation: 1,
    firstBlockingBatchId: null,
    firstBlockingSequence: null,
    firstBlockingState: null,
    currentRetainedBatchCount: 0,
    currentRetainedPayloadBytes: 0,
    releaseHealthy: true,
    warnings: [],
  };
}

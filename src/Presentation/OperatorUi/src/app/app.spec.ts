import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';

import { App } from './app';
import { RadarPulseProductApiClient } from './product/product-api.client';
import {
  ProductApiResponse,
  ProductRunDetail,
  ProductRunHistoryReadiness,
  ProductRunSummary,
} from './product/product-api.models';

describe('App', () => {
  let api: ProductApiStub;

  beforeEach(async () => {
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
});

class ProductApiStub {
  runs: readonly ProductRunSummary[] = [];
  latest: ProductRunDetail | null = null;
  readinessError: HttpErrorResponse | null = null;
  demoRunRequested = false;

  getHistoryReadiness() {
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
    return of(ok(detail('archive-created')));
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
    operatorSummary: {
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
    },
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
    diagnostics: null,
    handlerContract: null,
    batches: [],
    sources: [],
    message: '',
    runId,
    isReady: true,
    hasReadModel: true,
  };
}

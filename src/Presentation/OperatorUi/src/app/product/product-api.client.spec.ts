import { provideHttpClient } from '@angular/common/http';
import { HttpErrorResponse } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import {
  getRadarPulseProductApiBaseUrlForOrigin,
  RADARPULSE_PRODUCT_API_BASE_URL_STORAGE_KEY,
  provideRadarPulseProductApi,
} from './product-api.config';
import { RadarPulseProductApiClient } from './product-api.client';
import { ProductControlAction } from './product-api.models';
import {
  mapProductApiResponse,
  mapProductHttpError,
} from './product-api-state';

describe('RadarPulseProductApiClient', () => {
  let client: RadarPulseProductApiClient;
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.removeItem(RADARPULSE_PRODUCT_API_BASE_URL_STORAGE_KEY);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRadarPulseProductApi('http://localhost:5117/'),
      ],
    });

    client = TestBed.inject(RadarPulseProductApiClient);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('maps product query routes to the configured base URL', () => {
    client.getHistoryReadiness().subscribe();
    http.expectOne('http://localhost:5117/product/pipeline/host/readiness').flush(ok({}));

    client.listRuns().subscribe();
    http.expectOne('http://localhost:5117/product/pipeline/runs').flush(ok([]));

    client.getLatestRun().subscribe();
    http.expectOne('http://localhost:5117/product/pipeline/runs/latest').flush(ok({}));

    client.getRun('run/1').subscribe();
    http.expectOne('http://localhost:5117/product/pipeline/runs/run%2F1').flush(ok({}));

    client.listBatches('run/1').subscribe();
    http.expectOne('http://localhost:5117/product/pipeline/runs/run%2F1/batches').flush(ok([]));

    client.getBatch('run/1', 7).subscribe();
    http.expectOne('http://localhost:5117/product/pipeline/runs/run%2F1/batches/7').flush(ok({}));

    client.listSources('run/1').subscribe();
    http.expectOne('http://localhost:5117/product/pipeline/runs/run%2F1/sources').flush(ok([]));

    client.getSource('run/1', 3).subscribe();
    http.expectOne('http://localhost:5117/product/pipeline/runs/run%2F1/sources/3').flush(ok({}));

    client.getHandlerOutput('run/1', 3, 'benchmark.events').subscribe();
    http
      .expectOne(
        'http://localhost:5117/product/pipeline/runs/run%2F1/handlers/3/benchmark.events',
      )
      .flush(ok({}));

    client.getDiagnostics('run/1').subscribe();
    http.expectOne('http://localhost:5117/product/pipeline/runs/run%2F1/diagnostics').flush(ok({}));

    client.getCapacityEvidence('run/1').subscribe();
    http.expectOne('http://localhost:5117/product/pipeline/runs/run%2F1/capacity').flush(ok({}));
  });

  it('maps product run and control routes', () => {
    client.runDemo({ runId: 'demo-1' }).subscribe();
    const demo = http.expectOne('http://localhost:5117/product/pipeline/runs/demo');
    expect(demo.request.method).toBe('POST');
    expect(demo.request.body.runId).toBe('demo-1');
    demo.flush(ok({}));

    client.runArchive({ runId: 'archive-1', filePath: 'data/file.ar2v' }).subscribe();
    const archive = http.expectOne('http://localhost:5117/product/pipeline/runs/archive');
    expect(archive.request.method).toBe('POST');
    expect(archive.request.body.filePath).toBe('data/file.ar2v');
    archive.flush(ok({}));

    const controlRequest = {
      runId: 'run-1',
      durableStorePath: 'durable.json',
    };

    client.stopAccepting(controlRequest).subscribe();
    expectControl('/controls/stop-accepting', ProductControlAction.stopAccepting);

    client.drainAccepted(controlRequest).subscribe();
    expectControl('/controls/drain-accepted', ProductControlAction.drainAccepted);

    client.cancelOpenAndRelease(controlRequest).subscribe();
    expectControl('/controls/cancel-open-release', ProductControlAction.cancelOpenAndRelease);

    client.rejectUnsafeFallback(controlRequest).subscribe();
    expectControl('/controls/reject-unsafe-fallback', ProductControlAction.rejectUnsafeFallback);
  });

  it('uses the runtime base URL override when present', () => {
    localStorage.setItem(RADARPULSE_PRODUCT_API_BASE_URL_STORAGE_KEY, 'http://localhost:6117/');

    client.getHistoryReadiness().subscribe();
    http.expectOne('http://localhost:6117/product/pipeline/host/readiness').flush(ok({}));
  });
});

describe('product API base URL defaults', () => {
  it('keeps the local HTTP host default for the Angular dev server origin', () => {
    expect(getRadarPulseProductApiBaseUrlForOrigin('http://127.0.0.1:4200')).toBe(
      'http://localhost:5000',
    );
  });

  it('uses the current origin for integrated same-origin delivery', () => {
    expect(getRadarPulseProductApiBaseUrlForOrigin('http://127.0.0.1:5129')).toBe(
      'http://127.0.0.1:5129',
    );
  });
});

describe('product API response state mapping', () => {
  it('preserves success, not-found, blocked, rejected, and network states', () => {
    expect(mapProductApiResponse(ok({ runId: 'run-1' })).kind).toBe('success');

    expect(
      mapProductApiResponse({
        statusCode: 404,
        isSuccess: false,
        body: null,
        message: 'Run was not found.',
      }).kind,
    ).toBe('not-found');

    const blocked = mapProductApiResponse(
      ok({
        storageIdentity: 'history.json',
        loadedRunCount: 0,
        rejectedRunCount: 1,
        isReady: false,
        firstBlockingReason: 'history is corrupt',
      }),
    );
    expect(blocked.kind).toBe('blocked');
    expect(blocked.firstBlockingReason).toBe('history is corrupt');

    const rejected = mapProductApiResponse(
      ok({
        action: 'RejectUnsafeFallback',
        operatorSummary: {
          firstBlockingReason: 'borrowed fallback requested',
          warnings: ['borrowed fallback requested'],
        },
      }),
    );
    expect(rejected.kind).toBe('rejected');
    expect(rejected.warnings).toEqual(['borrowed fallback requested']);

    const network = mapProductHttpError(new HttpErrorResponse({ status: 0 }));
    expect(network.kind).toBe('network-error');
  });
});

function ok<T>(body: T) {
  return {
    statusCode: 200,
    isSuccess: true,
    body,
    message: '',
  };
}

function expectControl(route: string, action: number): void {
  const request = TestBed.inject(HttpTestingController).expectOne(
    `http://localhost:5117/product/pipeline${route}`,
  );

  expect(request.request.method).toBe('POST');
  expect(request.request.body.action).toBe(action);
  request.flush(ok({}));
}

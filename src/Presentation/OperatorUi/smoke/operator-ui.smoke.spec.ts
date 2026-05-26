import { expect, Page, Route, test } from '@playwright/test';

const apiBaseUrlStorageKey = 'radarpulse.productApiBaseUrl';
const smokeRunId = 'smoke-run';

test('renders readiness, run list, and deep-linked diagnostics tab', async ({ page }) => {
  await installProductApiMocks(page);

  await page.goto(`/?runId=${smokeRunId}&tab=diagnostics`);

  await expect(page.getByRole('heading', { name: 'Product Pipeline' })).toBeVisible();
  await expect(page.getByText('History is available for product reads.')).toBeVisible();
  await expect(page.getByRole('heading', { name: smokeRunId })).toBeVisible();
  await expect(page.getByText('ordered merge')).toBeVisible();
});

test('creates demo run and preserves run and tab URL state', async ({ page }) => {
  await installProductApiMocks(page);

  await page.goto('/');
  await page.getByRole('button', { name: 'Run demo' }).click();

  await expect(page.getByRole('heading', { name: 'smoke-created' })).toBeVisible();
  await expect(page).toHaveURL(/runId=smoke-created/);

  await page.getByRole('button', { name: 'Capacity' }).click();
  await expect(page).toHaveURL(/tab=capacity/);

  await page.reload();
  await expect(page.getByText('production')).toBeVisible();
});

test('loads handler output and renders rejected control posture', async ({ page }) => {
  await installProductApiMocks(page);

  await page.goto(`/?runId=${smokeRunId}&tab=handlers`);
  await page.getByRole('button', { name: 'Load handler output' }).click();

  await expect(page.getByText('benchmark.events')).toBeVisible();

  await page.getByLabel('Durable store path').fill('durable-smoke.json');
  await page.getByRole('button', { name: 'Reject unsafe fallback' }).click();

  await expect(page.getByRole('paragraph').filter({
    hasText: 'borrowed fallback requested',
  })).toBeVisible();
});

test('shows unreachable host posture without enabling controls', async ({ page }) => {
  await installProductApiMocks(page, { unreachable: true });

  await page.goto('/');

  await expect(page.locator('header').getByText('network-error')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Reject unsafe fallback' })).toBeDisabled();
});

async function installProductApiMocks(
  page: Page,
  options: { readonly unreachable?: boolean } = {},
): Promise<void> {
  let currentRun = productRun(smokeRunId);

  await page.addInitScript(
    ([storageKey, baseUrl]) => {
      localStorage.setItem(storageKey, baseUrl);
    },
    [apiBaseUrlStorageKey, 'http://127.0.0.1:4200'],
  );

  await page.route('**/product/pipeline/**', async route => {
    if (options.unreachable) {
      await route.abort('failed');
      return;
    }

    const request = route.request();
    if (request.method() === 'OPTIONS') {
      await fulfillJson(route, null, 204);
      return;
    }

    const path = new URL(request.url()).pathname;

    if (request.method() === 'GET' && path === '/product/pipeline/host/readiness') {
      await fulfillJson(route, ok({
        storageKind: 2,
        isReady: true,
        storageIdentity: 'smoke-history.json',
        schemaVersion: 1,
        loadedRunCount: 1,
        rejectedRunCount: 0,
        firstBlockingReason: '',
        warnings: [],
      }));
      return;
    }

    if (request.method() === 'GET' && path === '/product/pipeline/runs') {
      await fulfillJson(route, ok([currentRun.summary]));
      return;
    }

    if (request.method() === 'GET' && path === '/product/pipeline/runs/latest') {
      await fulfillJson(route, ok(currentRun));
      return;
    }

    if (request.method() === 'POST' && path === '/product/pipeline/runs/demo') {
      currentRun = productRun('smoke-created');
      await fulfillJson(route, { ...ok(currentRun), statusCode: 201 }, 201);
      return;
    }

    if (request.method() === 'GET' && path.endsWith('/handlers/0/benchmark.events')) {
      await fulfillJson(route, ok({
        handlerIndex: 0,
        handlerName: 'counter-checksum',
        name: 'benchmark.events',
        type: 'Int64',
        int64Value: 42,
        doubleValue: 0,
      }));
      return;
    }

    if (request.method() === 'POST' && path.endsWith('/controls/reject-unsafe-fallback')) {
      await fulfillJson(route, ok({
        runId: currentRun.runId,
        action: 'RejectUnsafeFallback',
        operatorSummary: {
          ...operatorSummary(),
          isReady: false,
          firstBlockingReason: 'borrowed fallback requested',
          warnings: ['borrowed fallback requested'],
        },
        canceledOpenCount: 0,
        releasedCanceledCount: 0,
        drainedProcessingCount: 0,
        message: '',
      }));
      return;
    }

    const runDetailMatch = path.match(/^\/product\/pipeline\/runs\/([^/]+)$/);
    if (request.method() === 'GET' && runDetailMatch) {
      await fulfillJson(route, ok(productRun(decodeURIComponent(runDetailMatch[1]))));
      return;
    }

    await fulfillJson(
      route,
      {
        statusCode: 404,
        isSuccess: false,
        body: null,
        message: 'Smoke route was not found.',
      },
      404,
    );
  });
}

async function fulfillJson(route: Route, body: unknown, status = 200): Promise<void> {
  await route.fulfill({
    status,
    contentType: 'application/json',
    headers: {
      'Access-Control-Allow-Headers': '*',
      'Access-Control-Allow-Methods': 'GET,POST,OPTIONS',
      'Access-Control-Allow-Origin': '*',
    },
    body: body === null ? '' : JSON.stringify(body),
  });
}

function ok<T>(body: T) {
  return {
    statusCode: 200,
    isSuccess: true,
    body,
    message: '',
  };
}

function productRun(runId: string) {
  return {
    summary: {
      runId,
      input: {
        kind: 1,
        description: 'synthetic smoke',
        source: 'smoke',
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
    },
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

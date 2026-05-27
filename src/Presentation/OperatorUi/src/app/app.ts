import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { RadarPulseProductApiClient } from './product/product-api.client';
import {
  getStoredRadarPulseProductApiBaseUrl,
  storeRadarPulseProductApiBaseUrl,
} from './product/product-api.config';
import {
  ProductControlSummary,
  ProductHandlerSet,
  ProductHandlerOutput,
  ProductRunDetail,
  ProductRunHistoryReadiness,
  ProductRunSummary,
  ProductEnumValue,
} from './product/product-api.models';
import {
  ProductRequestState,
  mapProductApiResponse,
  mapProductHttpError,
} from './product/product-api-state';

type DetailTab = 'summary' | 'batches' | 'sources' | 'handlers' | 'diagnostics' | 'capacity';
type EnumKind = 'state' | 'handlerMode' | 'fallback';

const detailTabs = new Set<DetailTab>([
  'summary',
  'batches',
  'sources',
  'handlers',
  'diagnostics',
  'capacity',
]);

@Component({
  selector: 'app-root',
  imports: [FormsModule],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App implements OnInit {
  protected demoRunId = this.createRunId('demo');
  protected apiBaseUrl = getStoredRadarPulseProductApiBaseUrl();
  protected demoSourceCount = 2;
  protected demoBatchCount = 2;
  protected demoEventsPerBatch = 2;
  protected archiveFilePath = '';
  protected durableStorePath = '';
  protected handlerSourceId = 0;
  protected handlerFieldName = 'benchmark.events';

  protected readonly readiness = signal<ProductRequestState<ProductRunHistoryReadiness> | null>(null);
  protected readonly runs = signal<ProductRequestState<readonly ProductRunSummary[]> | null>(null);
  protected readonly latestRun = signal<ProductRequestState<ProductRunDetail> | null>(null);
  protected readonly selectedRun = signal<ProductRequestState<ProductRunDetail> | null>(null);
  protected readonly runCommand = signal<ProductRequestState<ProductRunDetail> | null>(null);
  protected readonly handlerOutput = signal<ProductRequestState<ProductHandlerOutput> | null>(null);
  protected readonly controlOutcome = signal<ProductRequestState<ProductControlSummary> | null>(null);
  protected readonly selectedRunId = signal<string>('');
  protected readonly activeTab = signal<DetailTab>('summary');
  protected readonly apiBaseUrlValidationMessage = signal('');
  protected readonly runValidationMessage = signal('');
  protected readonly handlerLookupValidationMessage = signal('');

  protected readonly readinessLoading = signal(false);
  protected readonly runsLoading = signal(false);
  protected readonly latestLoading = signal(false);
  protected readonly selectedRunLoading = signal(false);
  protected readonly runLoading = signal(false);
  protected readonly handlerOutputLoading = signal(false);
  protected readonly controlLoading = signal(false);

  protected readonly isBusy = computed(() =>
    this.readinessLoading() ||
    this.runsLoading() ||
    this.latestLoading() ||
    this.selectedRunLoading() ||
    this.runLoading() ||
    this.handlerOutputLoading() ||
    this.controlLoading(),
  );

  constructor(private readonly api: RadarPulseProductApiClient) {}

  ngOnInit(): void {
    this.applyInitialUrlState();
    this.refreshAll();
  }

  protected refreshAll(): void {
    this.loadReadiness();
    this.loadRuns();
    this.loadLatestRun();
  }

  protected applyApiBaseUrl(): void {
    const validationMessage = validateProductApiBaseUrl(this.apiBaseUrl);

    if (validationMessage) {
      this.apiBaseUrlValidationMessage.set(validationMessage);
      return;
    }

    this.apiBaseUrlValidationMessage.set('');
    this.apiBaseUrl = storeRadarPulseProductApiBaseUrl(this.apiBaseUrl);
    this.refreshAll();
  }

  protected runDemo(): void {
    const validationMessage = this.validateDemoRun();

    if (validationMessage) {
      this.runValidationMessage.set(validationMessage);
      return;
    }

    this.runValidationMessage.set('');
    this.runLoading.set(true);
    this.api.runDemo({
      runId: this.demoRunId.trim() || this.createRunId('demo'),
      sourceCount: Number(this.demoSourceCount),
      batchCount: Number(this.demoBatchCount),
      eventsPerBatch: Number(this.demoEventsPerBatch),
      handlerSet: ProductHandlerSet.counterChecksum,
    }).subscribe({
      next: response => this.handleRunCreated(mapProductApiResponse(response)),
      error: error => this.handleRunCreated(mapProductHttpError(error) as ProductRequestState<ProductRunDetail>),
    });
  }

  protected runArchive(): void {
    const filePath = this.archiveFilePath.trim();

    if (!filePath) {
      this.runValidationMessage.set('Archive file path is required.');
      return;
    }

    this.runValidationMessage.set('');
    this.runLoading.set(true);
    this.api.runArchive({
      runId: this.createRunId('archive'),
      filePath,
      handlerSet: ProductHandlerSet.counterChecksum,
    }).subscribe({
      next: response => this.handleRunCreated(mapProductApiResponse(response)),
      error: error => this.handleRunCreated(mapProductHttpError(error) as ProductRequestState<ProductRunDetail>),
    });
  }

  protected selectRun(runId: string, updateUrl = true): void {
    const selectedRunId = runId.trim();

    if (!selectedRunId) {
      return;
    }

    this.selectedRunId.set(selectedRunId);
    if (updateUrl) {
      this.updateUrlState();
    }

    this.selectedRunLoading.set(true);
    this.handlerOutput.set(null);
    this.api.getRun(selectedRunId).subscribe({
      next: response => {
        this.selectedRun.set(mapProductApiResponse(response));
        this.selectedRunLoading.set(false);
      },
      error: error => {
        this.selectedRun.set(mapProductHttpError(error) as ProductRequestState<ProductRunDetail>);
        this.selectedRunLoading.set(false);
      },
    });
  }

  protected readinessLabel(): string {
    const state = this.readiness();

    if (!state) {
      return 'Not checked';
    }

    return state.kind === 'success' ? 'Connected' : state.kind;
  }

  protected readinessStatus(): string {
    const state = this.readiness();

    if (!state) {
      return 'Unknown';
    }

    return state.kind === 'success' ? 'Ready' : state.kind === 'blocked' ? 'Blocked' : 'Unavailable';
  }

  protected latestRunLabel(): string {
    const latest = this.latestRun();

    if (!latest) {
      return 'Not loaded';
    }

    return latest.body?.summary.isReady ? 'Ready' : latest.kind;
  }

  protected runCountLabel(): string {
    const count = this.runs()?.body?.length;

    return count === undefined ? 'not loaded' : `${count} run${count === 1 ? '' : 's'}`;
  }

  protected runCommandState(): string {
    if (this.runLoading()) {
      return 'Running';
    }

    return this.runCommand()?.kind || 'Idle';
  }

  protected selectTab(tab: DetailTab): void {
    this.activeTab.set(tab);
    this.updateUrlState();
  }

  protected loadHandlerOutput(): void {
    const runId = this.selectedRunId();
    const sourceId = Number(this.handlerSourceId);
    const fieldName = this.handlerFieldName.trim();
    const validationMessage = validateHandlerLookup(runId, sourceId, fieldName);

    if (validationMessage) {
      this.handlerLookupValidationMessage.set(validationMessage);
      return;
    }

    this.handlerLookupValidationMessage.set('');
    this.handlerOutputLoading.set(true);
    this.api.getHandlerOutput(runId, sourceId, fieldName).subscribe({
      next: response => {
        this.handlerOutput.set(mapProductApiResponse(response));
        this.handlerOutputLoading.set(false);
      },
      error: error => {
        this.handlerOutput.set(mapProductHttpError(error) as ProductRequestState<ProductHandlerOutput>);
        this.handlerOutputLoading.set(false);
      },
    });
  }

  protected stopAccepting(): void {
    this.applyControl('stop');
  }

  protected drainAccepted(): void {
    this.applyControl('drain');
  }

  protected cancelOpenAndRelease(): void {
    this.applyControl('cancel');
  }

  protected rejectUnsafeFallback(): void {
    this.applyControl('reject');
  }

  protected controlTargetRunId(): string {
    return this.selectedRunId() || this.latestRun()?.body?.runId || '';
  }

  protected controlsDisabled(): boolean {
    const readiness = this.readiness();

    return (
      this.controlLoading() ||
      this.readinessLoading() ||
      !readiness ||
      readiness.kind !== 'success' ||
      this.controlTargetRunId().trim().length === 0 ||
      this.durableStorePath.trim().length === 0
    );
  }

  protected archiveRunDisabled(): boolean {
    return this.runLoading() || this.archiveFilePath.trim().length === 0;
  }

  protected handlerLookupDisabled(): boolean {
    return (
      this.handlerOutputLoading() ||
      this.handlerFieldName.trim().length === 0 ||
      !Number.isInteger(Number(this.handlerSourceId)) ||
      Number(this.handlerSourceId) < 0
    );
  }

  protected enumLabel(value: ProductEnumValue, kind: EnumKind): string {
    if (typeof value === 'string') {
      return value;
    }

    const labels = {
      state: new Map<number, string>([
        [1, 'Not started'],
        [2, 'Running'],
        [3, 'Draining'],
        [4, 'Completed'],
        [5, 'Stopped'],
        [6, 'Blocked'],
        [7, 'Failed'],
        [8, 'Canceled'],
      ]),
      handlerMode: new Map<number, string>([
        [1, 'Auto'],
        [2, 'Handler free'],
        [3, 'Mergeable delta'],
        [4, 'Snapshot sequential'],
      ]),
      fallback: new Map<number, string>([
        [1, 'None'],
        [2, 'Fix configuration'],
        [3, 'Inspect durable adapter'],
        [4, 'Recover claimed envelope'],
        [5, 'Retry or poison envelope'],
        [6, 'Quarantine poison envelope'],
        [7, 'Cleanup canceled envelope'],
        [8, 'Release retained resources'],
        [9, 'Complete or recover work'],
        [10, 'Resolve handler posture'],
        [11, 'Reject unsafe fallback'],
      ]),
    } satisfies Record<EnumKind, Map<number, string>>;

    return labels[kind].get(value) || String(value);
  }

  protected combinedWarnings(detail: ProductRunDetail): readonly string[] {
    return [...detail.operatorSummary.warnings, ...detail.configuration.warnings];
  }

  private loadReadiness(): void {
    this.readinessLoading.set(true);
    this.api.getHistoryReadiness().subscribe({
      next: response => {
        this.readiness.set(mapProductApiResponse(response));
        this.readinessLoading.set(false);
      },
      error: error => {
        this.readiness.set(mapProductHttpError(error) as ProductRequestState<ProductRunHistoryReadiness>);
        this.readinessLoading.set(false);
      },
    });
  }

  private loadRuns(): void {
    this.runsLoading.set(true);
    this.api.listRuns().subscribe({
      next: response => {
        this.runs.set(mapProductApiResponse(response));
        this.runsLoading.set(false);
      },
      error: error => {
        this.runs.set(mapProductHttpError(error) as ProductRequestState<readonly ProductRunSummary[]>);
        this.runsLoading.set(false);
      },
    });
  }

  private loadLatestRun(): void {
    this.latestLoading.set(true);
    this.api.getLatestRun().subscribe({
      next: response => {
        const state = mapProductApiResponse(response);
        this.latestRun.set(state);
        this.latestLoading.set(false);

        if (state.body && !this.selectedRunId()) {
          this.selectedRunId.set(state.body.runId);
          this.selectedRun.set(state);
        }
      },
      error: error => {
        this.latestRun.set(mapProductHttpError(error) as ProductRequestState<ProductRunDetail>);
        this.latestLoading.set(false);
      },
    });
  }

  private handleRunCreated(state: ProductRequestState<ProductRunDetail>): void {
    this.runCommand.set(state);
    this.runLoading.set(false);

    if (state.body) {
      this.demoRunId = this.createRunId('demo');
      this.selectedRunId.set(state.body.runId);
      this.selectedRun.set(state);
      this.updateUrlState();
      this.refreshAll();
    }
  }

  private applyControl(action: 'stop' | 'drain' | 'cancel' | 'reject'): void {
    const runId = this.controlTargetRunId().trim();
    const durableStorePath = this.durableStorePath.trim();

    if (!runId || !durableStorePath) {
      return;
    }

    const request = {
      runId,
      durableStorePath,
      sourceCount: this.demoSourceCount,
      handlerSet: ProductHandlerSet.counterChecksum,
    };
    const controlCall = action === 'stop'
      ? this.api.stopAccepting(request)
      : action === 'drain'
        ? this.api.drainAccepted(request)
        : action === 'cancel'
          ? this.api.cancelOpenAndRelease(request)
          : this.api.rejectUnsafeFallback(request);

    this.controlLoading.set(true);
    controlCall.subscribe({
      next: response => {
        this.controlOutcome.set(mapProductApiResponse(response));
        this.controlLoading.set(false);
        this.refreshAll();
      },
      error: error => {
        this.controlOutcome.set(mapProductHttpError(error) as ProductRequestState<ProductControlSummary>);
        this.controlLoading.set(false);
      },
    });
  }

  private createRunId(prefix: string): string {
    const timestamp = new Date()
      .toISOString()
      .replace(/[-:.TZ]/g, '')
      .slice(0, 14);

    return `${prefix}-${timestamp}`;
  }

  private applyInitialUrlState(): void {
    const url = readCurrentUrl();

    if (!url) {
      return;
    }

    const tab = parseDetailTab(url.searchParams.get('tab'));
    this.activeTab.set(tab);

    const runId = url.searchParams.get('runId')?.trim() || '';
    if (runId) {
      this.selectRun(runId, false);
    }
  }

  private updateUrlState(): void {
    const url = readCurrentUrl();

    if (!url || !globalThis.history?.replaceState) {
      return;
    }

    const runId = this.selectedRunId().trim();
    if (runId) {
      url.searchParams.set('runId', runId);
    } else {
      url.searchParams.delete('runId');
    }

    const tab = this.activeTab();
    if (tab === 'summary') {
      url.searchParams.delete('tab');
    } else {
      url.searchParams.set('tab', tab);
    }

    globalThis.history.replaceState(
      globalThis.history.state,
      '',
      `${url.pathname}${url.search}${url.hash}`,
    );
  }

  private validateDemoRun(): string {
    if (!isPositiveInteger(this.demoSourceCount)) {
      return 'Demo source count must be a positive whole number.';
    }

    if (!isPositiveInteger(this.demoBatchCount)) {
      return 'Demo batch count must be a positive whole number.';
    }

    if (!isPositiveInteger(this.demoEventsPerBatch)) {
      return 'Demo event count must be a positive whole number.';
    }

    return '';
  }
}

function parseDetailTab(value: string | null): DetailTab {
  return detailTabs.has(value as DetailTab) ? value as DetailTab : 'summary';
}

function readCurrentUrl(): URL | null {
  try {
    return new URL(globalThis.location.href);
  } catch {
    return null;
  }
}

function validateProductApiBaseUrl(value: string): string {
  const trimmed = value.trim();

  if (!trimmed) {
    return 'Product host URL is required.';
  }

  try {
    const url = new URL(trimmed);

    if (url.protocol !== 'http:' && url.protocol !== 'https:') {
      return 'Use an absolute http:// or https:// URL.';
    }

    return '';
  } catch {
    return 'Use an absolute http:// or https:// URL.';
  }
}

function validateHandlerLookup(
  runId: string,
  sourceId: number,
  fieldName: string,
): string {
  if (!runId.trim()) {
    return 'Select a run before loading handler output.';
  }

  if (!Number.isInteger(sourceId) || sourceId < 0) {
    return 'Handler source id must be zero or a positive whole number.';
  }

  if (!fieldName.trim()) {
    return 'Handler field name is required.';
  }

  return '';
}

function isPositiveInteger(value: number): boolean {
  return Number.isInteger(Number(value)) && Number(value) > 0;
}

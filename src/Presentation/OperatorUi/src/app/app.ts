import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { RadarPulseProductApiClient } from './product/product-api.client';
import {
  ProductHandlerSet,
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

@Component({
  selector: 'app-root',
  imports: [FormsModule],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App implements OnInit {
  protected demoRunId = this.createRunId('demo');
  protected demoSourceCount = 2;
  protected demoBatchCount = 2;
  protected demoEventsPerBatch = 2;
  protected archiveFilePath = '';

  protected readonly readiness = signal<ProductRequestState<ProductRunHistoryReadiness> | null>(null);
  protected readonly runs = signal<ProductRequestState<readonly ProductRunSummary[]> | null>(null);
  protected readonly latestRun = signal<ProductRequestState<ProductRunDetail> | null>(null);
  protected readonly selectedRun = signal<ProductRequestState<ProductRunDetail> | null>(null);
  protected readonly runCommand = signal<ProductRequestState<ProductRunDetail> | null>(null);
  protected readonly selectedRunId = signal<string>('');

  protected readonly readinessLoading = signal(false);
  protected readonly runsLoading = signal(false);
  protected readonly latestLoading = signal(false);
  protected readonly selectedRunLoading = signal(false);
  protected readonly runLoading = signal(false);

  protected readonly isBusy = computed(() =>
    this.readinessLoading() ||
    this.runsLoading() ||
    this.latestLoading() ||
    this.selectedRunLoading() ||
    this.runLoading(),
  );

  constructor(private readonly api: RadarPulseProductApiClient) {}

  ngOnInit(): void {
    this.refreshAll();
  }

  protected refreshAll(): void {
    this.loadReadiness();
    this.loadRuns();
    this.loadLatestRun();
  }

  protected runDemo(): void {
    this.runLoading.set(true);
    this.api.runDemo({
      runId: this.demoRunId.trim() || this.createRunId('demo'),
      sourceCount: this.demoSourceCount,
      batchCount: this.demoBatchCount,
      eventsPerBatch: this.demoEventsPerBatch,
      handlerSet: ProductHandlerSet.counterChecksum,
    }).subscribe({
      next: response => this.handleRunCreated(mapProductApiResponse(response)),
      error: error => this.handleRunCreated(mapProductHttpError(error) as ProductRequestState<ProductRunDetail>),
    });
  }

  protected runArchive(): void {
    const filePath = this.archiveFilePath.trim();

    if (!filePath) {
      return;
    }

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

  protected selectRun(runId: string): void {
    this.selectedRunId.set(runId);
    this.selectedRunLoading.set(true);
    this.api.getRun(runId).subscribe({
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

  protected enumLabel(value: ProductEnumValue): string {
    return String(value);
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
      this.refreshAll();
    }
  }

  private createRunId(prefix: string): string {
    const timestamp = new Date()
      .toISOString()
      .replace(/[-:.TZ]/g, '')
      .slice(0, 14);

    return `${prefix}-${timestamp}`;
  }
}

import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { injectRadarPulseProductApiBaseUrl } from './product-api.config';
import {
  ProductApiResponse,
  ProductArchiveFileRunRequest,
  ProductBatch,
  ProductCapacityEvidence,
  ProductControlAction,
  ProductControlRequest,
  ProductControlSummary,
  ProductDiagnostics,
  ProductHandlerOutput,
  ProductRunDetail,
  ProductRunHistoryReadiness,
  ProductRunSummary,
  ProductSource,
  ProductSyntheticRunRequest,
} from './product-api.models';

@Injectable({
  providedIn: 'root',
})
export class RadarPulseProductApiClient {
  private readonly baseUrl = injectRadarPulseProductApiBaseUrl();

  constructor(private readonly http: HttpClient) {}

  getHistoryReadiness(): Observable<ProductApiResponse<ProductRunHistoryReadiness>> {
    return this.http.get<ProductApiResponse<ProductRunHistoryReadiness>>(
      this.url('/product/pipeline/host/readiness'),
    );
  }

  listRuns(): Observable<ProductApiResponse<readonly ProductRunSummary[]>> {
    return this.http.get<ProductApiResponse<readonly ProductRunSummary[]>>(
      this.url('/product/pipeline/runs'),
    );
  }

  getLatestRun(): Observable<ProductApiResponse<ProductRunDetail>> {
    return this.http.get<ProductApiResponse<ProductRunDetail>>(
      this.url('/product/pipeline/runs/latest'),
    );
  }

  getRun(runId: string): Observable<ProductApiResponse<ProductRunDetail>> {
    return this.http.get<ProductApiResponse<ProductRunDetail>>(
      this.url(`/product/pipeline/runs/${encodeURIComponent(runId)}`),
    );
  }

  listBatches(runId: string): Observable<ProductApiResponse<readonly ProductBatch[]>> {
    return this.http.get<ProductApiResponse<readonly ProductBatch[]>>(
      this.url(`/product/pipeline/runs/${encodeURIComponent(runId)}/batches`),
    );
  }

  getBatch(
    runId: string,
    providerSequence: number,
  ): Observable<ProductApiResponse<ProductBatch>> {
    return this.http.get<ProductApiResponse<ProductBatch>>(
      this.url(
        `/product/pipeline/runs/${encodeURIComponent(runId)}/batches/${providerSequence}`,
      ),
    );
  }

  listSources(runId: string): Observable<ProductApiResponse<readonly ProductSource[]>> {
    return this.http.get<ProductApiResponse<readonly ProductSource[]>>(
      this.url(`/product/pipeline/runs/${encodeURIComponent(runId)}/sources`),
    );
  }

  getSource(runId: string, sourceId: number): Observable<ProductApiResponse<ProductSource>> {
    return this.http.get<ProductApiResponse<ProductSource>>(
      this.url(`/product/pipeline/runs/${encodeURIComponent(runId)}/sources/${sourceId}`),
    );
  }

  getHandlerOutput(
    runId: string,
    sourceId: number,
    fieldName: string,
  ): Observable<ProductApiResponse<ProductHandlerOutput>> {
    return this.http.get<ProductApiResponse<ProductHandlerOutput>>(
      this.url(
        `/product/pipeline/runs/${encodeURIComponent(runId)}/handlers/${sourceId}/${encodeURIComponent(fieldName)}`,
      ),
    );
  }

  getDiagnostics(runId: string): Observable<ProductApiResponse<ProductDiagnostics>> {
    return this.http.get<ProductApiResponse<ProductDiagnostics>>(
      this.url(`/product/pipeline/runs/${encodeURIComponent(runId)}/diagnostics`),
    );
  }

  getCapacityEvidence(runId: string): Observable<ProductApiResponse<ProductCapacityEvidence>> {
    return this.http.get<ProductApiResponse<ProductCapacityEvidence>>(
      this.url(`/product/pipeline/runs/${encodeURIComponent(runId)}/capacity`),
    );
  }

  runDemo(
    request: ProductSyntheticRunRequest,
  ): Observable<ProductApiResponse<ProductRunDetail>> {
    return this.http.post<ProductApiResponse<ProductRunDetail>>(
      this.url('/product/pipeline/runs/demo'),
      request,
    );
  }

  runArchive(
    request: ProductArchiveFileRunRequest,
  ): Observable<ProductApiResponse<ProductRunDetail>> {
    return this.http.post<ProductApiResponse<ProductRunDetail>>(
      this.url('/product/pipeline/runs/archive'),
      request,
    );
  }

  stopAccepting(
    request: Omit<ProductControlRequest, 'action'>,
  ): Observable<ProductApiResponse<ProductControlSummary>> {
    return this.postControl('/product/pipeline/controls/stop-accepting', {
      ...request,
      action: ProductControlAction.stopAccepting,
    });
  }

  drainAccepted(
    request: Omit<ProductControlRequest, 'action'>,
  ): Observable<ProductApiResponse<ProductControlSummary>> {
    return this.postControl('/product/pipeline/controls/drain-accepted', {
      ...request,
      action: ProductControlAction.drainAccepted,
    });
  }

  cancelOpenAndRelease(
    request: Omit<ProductControlRequest, 'action'>,
  ): Observable<ProductApiResponse<ProductControlSummary>> {
    return this.postControl('/product/pipeline/controls/cancel-open-release', {
      ...request,
      action: ProductControlAction.cancelOpenAndRelease,
    });
  }

  rejectUnsafeFallback(
    request: Omit<ProductControlRequest, 'action'>,
  ): Observable<ProductApiResponse<ProductControlSummary>> {
    return this.postControl('/product/pipeline/controls/reject-unsafe-fallback', {
      ...request,
      action: ProductControlAction.rejectUnsafeFallback,
    });
  }

  private postControl(
    route: string,
    request: ProductControlRequest,
  ): Observable<ProductApiResponse<ProductControlSummary>> {
    return this.http.post<ProductApiResponse<ProductControlSummary>>(
      this.url(route),
      request,
    );
  }

  private url(route: string): string {
    return `${this.baseUrl}${route}`;
  }
}

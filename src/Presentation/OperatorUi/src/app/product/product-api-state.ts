import { HttpErrorResponse } from '@angular/common/http';

import {
  ProductApiResponse,
  ProductControlSummary,
  ProductRunHistoryReadiness,
} from './product-api.models';

export type ProductRequestStateKind =
  | 'success'
  | 'not-found'
  | 'bad-request'
  | 'blocked'
  | 'rejected'
  | 'failure'
  | 'network-error';

export interface ProductRequestState<T> {
  readonly kind: ProductRequestStateKind;
  readonly statusCode: number | null;
  readonly isSuccess: boolean;
  readonly body: T | null;
  readonly message: string;
  readonly firstBlockingReason: string;
  readonly warnings: readonly string[];
}

export function mapProductApiResponse<T>(
  response: ProductApiResponse<T>,
): ProductRequestState<T> {
  if (!response.isSuccess) {
    return {
      kind: response.statusCode === 404
        ? 'not-found'
        : response.statusCode === 400
          ? 'bad-request'
          : 'failure',
      statusCode: response.statusCode,
      isSuccess: false,
      body: response.body,
      message: response.message,
      firstBlockingReason: response.message,
      warnings: [],
    };
  }

  const firstBlockingReason = findFirstBlockingReason(response.body);
  const warnings = findWarnings(response.body);
  const rejected = isRejectedControl(response.body);
  const blocked = !rejected && firstBlockingReason.length > 0;

  return {
    kind: rejected ? 'rejected' : blocked ? 'blocked' : 'success',
    statusCode: response.statusCode,
    isSuccess: true,
    body: response.body,
    message: response.message,
    firstBlockingReason,
    warnings,
  };
}

export function mapProductHttpError(error: unknown): ProductRequestState<never> {
  if (error instanceof HttpErrorResponse) {
    const response = isProductApiResponse(error.error) ? error.error : null;
    const message = response?.message || error.message || 'HTTP request failed.';

    return {
      kind: error.status === 0
        ? 'network-error'
        : error.status === 404
          ? 'not-found'
          : error.status === 400
            ? 'bad-request'
            : 'failure',
      statusCode: error.status || null,
      isSuccess: false,
      body: null,
      message,
      firstBlockingReason: message,
      warnings: [],
    };
  }

  return {
    kind: 'network-error',
    statusCode: null,
    isSuccess: false,
    body: null,
    message: 'Unable to reach the RadarPulse product HTTP host.',
    firstBlockingReason: 'Unable to reach the RadarPulse product HTTP host.',
    warnings: [],
  };
}

function findFirstBlockingReason<T>(body: T | null): string {
  if (!body || typeof body !== 'object') {
    return '';
  }

  if (isRunHistoryReadiness(body) && !body.isReady) {
    return body.firstBlockingReason;
  }

  if ('firstBlockingReason' in body && typeof body.firstBlockingReason === 'string') {
    return body.firstBlockingReason;
  }

  if ('operatorSummary' in body) {
    const operatorSummary = body.operatorSummary;

    if (
      operatorSummary &&
      typeof operatorSummary === 'object' &&
      'firstBlockingReason' in operatorSummary &&
      typeof operatorSummary.firstBlockingReason === 'string'
    ) {
      return operatorSummary.firstBlockingReason;
    }
  }

  return '';
}

function findWarnings<T>(body: T | null): readonly string[] {
  if (!body || typeof body !== 'object') {
    return [];
  }

  if ('warnings' in body && Array.isArray(body.warnings)) {
    return body.warnings.filter((warning): warning is string => typeof warning === 'string');
  }

  if ('operatorSummary' in body) {
    const operatorSummary = body.operatorSummary;

    if (
      operatorSummary &&
      typeof operatorSummary === 'object' &&
      'warnings' in operatorSummary &&
      Array.isArray(operatorSummary.warnings)
    ) {
      return operatorSummary.warnings.filter(
        (warning): warning is string => typeof warning === 'string',
      );
    }
  }

  return [];
}

function isRejectedControl<T>(body: T | null): body is T & ProductControlSummary {
  if (!body || typeof body !== 'object' || !('action' in body)) {
    return false;
  }

  return body.action === 'RejectUnsafeFallback';
}

function isRunHistoryReadiness<T>(body: T): body is T & ProductRunHistoryReadiness {
  return (
    body !== null &&
    body !== undefined &&
    typeof body === 'object' &&
    'storageIdentity' in body &&
    'loadedRunCount' in body &&
    'rejectedRunCount' in body
  );
}

function isProductApiResponse(value: unknown): value is ProductApiResponse<unknown> {
  return (
    value !== null &&
    value !== undefined &&
    typeof value === 'object' &&
    'statusCode' in value &&
    'isSuccess' in value &&
    'message' in value
  );
}

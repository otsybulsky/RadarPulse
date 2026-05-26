import { InjectionToken, Provider, inject } from '@angular/core';

export const DEFAULT_RADARPULSE_PRODUCT_API_BASE_URL = 'http://localhost:5000';

export const RADARPULSE_PRODUCT_API_BASE_URL = new InjectionToken<string>(
  'RADARPULSE_PRODUCT_API_BASE_URL',
  {
    providedIn: 'root',
    factory: () => DEFAULT_RADARPULSE_PRODUCT_API_BASE_URL,
  },
);

export function provideRadarPulseProductApi(baseUrl: string): Provider {
  return {
    provide: RADARPULSE_PRODUCT_API_BASE_URL,
    useValue: normalizeBaseUrl(baseUrl),
  };
}

export function injectRadarPulseProductApiBaseUrl(): string {
  return inject(RADARPULSE_PRODUCT_API_BASE_URL);
}

export function normalizeBaseUrl(baseUrl: string): string {
  const trimmed = baseUrl.trim();

  if (trimmed.length === 0) {
    return '';
  }

  return trimmed.replace(/\/+$/, '');
}

import { InjectionToken, Provider, inject } from '@angular/core';

export const DEFAULT_RADARPULSE_PRODUCT_API_BASE_URL = 'http://localhost:5000';
export const RADARPULSE_PRODUCT_API_BASE_URL_STORAGE_KEY = 'radarpulse.productApiBaseUrl';

export const RADARPULSE_PRODUCT_API_BASE_URL = new InjectionToken<string>(
  'RADARPULSE_PRODUCT_API_BASE_URL',
  {
    providedIn: 'root',
    factory: () => getDefaultRadarPulseProductApiBaseUrl(),
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

export function getDefaultRadarPulseProductApiBaseUrl(): string {
  return getRadarPulseProductApiBaseUrlForOrigin(globalThis.location?.origin);
}

export function getRadarPulseProductApiBaseUrlForOrigin(
  origin: string | undefined,
): string {
  if (!origin || origin === 'null') {
    return DEFAULT_RADARPULSE_PRODUCT_API_BASE_URL;
  }

  try {
    const url = new URL(origin);
    if (url.port === '4200') {
      return DEFAULT_RADARPULSE_PRODUCT_API_BASE_URL;
    }

    return normalizeBaseUrl(origin);
  } catch {
    return DEFAULT_RADARPULSE_PRODUCT_API_BASE_URL;
  }
}

export function getStoredRadarPulseProductApiBaseUrl(
  fallback = getDefaultRadarPulseProductApiBaseUrl(),
): string {
  const stored = globalThis.localStorage?.getItem(RADARPULSE_PRODUCT_API_BASE_URL_STORAGE_KEY);

  return normalizeBaseUrl(stored || fallback);
}

export function storeRadarPulseProductApiBaseUrl(baseUrl: string): string {
  const normalized = normalizeBaseUrl(baseUrl);
  globalThis.localStorage?.setItem(RADARPULSE_PRODUCT_API_BASE_URL_STORAGE_KEY, normalized);
  return normalized;
}

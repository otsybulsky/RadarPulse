export function validateProductApiBaseUrl(value: string): string {
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

export function validateHandlerLookup(
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

export function isPositiveInteger(value: number): boolean {
  return Number.isInteger(Number(value)) && Number(value) > 0;
}

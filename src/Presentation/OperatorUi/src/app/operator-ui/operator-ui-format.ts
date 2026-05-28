import { ProductEnumValue } from '../product/product-api.models';

export type EnumKind = 'state' | 'handlerMode' | 'fallback';

export function formatProductEnumLabel(value: ProductEnumValue, kind: EnumKind): string {
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

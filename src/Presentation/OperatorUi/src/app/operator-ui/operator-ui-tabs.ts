export type DetailTab = 'summary' | 'batches' | 'sources' | 'handlers' | 'diagnostics' | 'capacity';

const detailTabs = new Set<DetailTab>([
  'summary',
  'batches',
  'sources',
  'handlers',
  'diagnostics',
  'capacity',
]);

export function parseDetailTab(value: string | null): DetailTab {
  return detailTabs.has(value as DetailTab) ? value as DetailTab : 'summary';
}

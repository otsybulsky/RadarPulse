import { expect, test } from '@playwright/test';

test('serves the built operator UI and product API from one local origin', async ({ page, request }) => {
  const readiness = await request.get('/product/pipeline/host/readiness');
  expect(readiness.ok()).toBe(true);
  expect(readiness.headers()['content-type']).toContain('application/json');

  await page.goto('/');

  await expect(page.getByRole('heading', { name: 'Product Pipeline' })).toBeVisible();
  await expect(page.getByLabel('HTTP host')).toHaveValue('http://127.0.0.1:5129');
  await expect(page.getByText('History is available for product reads.')).toBeVisible();

  await page.getByLabel('Run id').fill('hosted-smoke');
  await page.getByRole('button', { name: 'Run demo' }).click();

  await expect(page.getByRole('heading', { name: 'hosted-smoke' })).toBeVisible();
  await expect(page).toHaveURL(/runId=hosted-smoke/);

  await page.getByRole('button', { name: 'Capacity' }).click();
  await expect(page).toHaveURL(/tab=capacity/);
  await expect(page.getByText('production')).toBeVisible();

  await page.reload();
  await expect(page.getByRole('heading', { name: 'hosted-smoke' })).toBeVisible();
  await expect(page.getByText('production')).toBeVisible();
});

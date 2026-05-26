import path from 'node:path';
import { defineConfig, devices } from '@playwright/test';

const hostedBaseUrl = 'http://127.0.0.1:5129';
const operatorUiAssetPath = path.resolve('dist', 'OperatorUi', 'browser');

export default defineConfig({
  testDir: './smoke',
  testMatch: '**/*.hosted.smoke.spec.ts',
  fullyParallel: false,
  timeout: 45_000,
  expect: {
    timeout: 10_000,
  },
  use: {
    baseURL: hostedBaseUrl,
    trace: 'on-first-retry',
  },
  webServer: {
    command: 'dotnet run --project ../RadarPulse.Http/RadarPulse.Http.csproj --no-launch-profile --urls http://127.0.0.1:5129',
    url: hostedBaseUrl,
    reuseExistingServer: false,
    timeout: 120_000,
    env: {
      ...process.env,
      RadarPulse__ProductHttp__UseInMemoryHistory: 'true',
      RadarPulse__ProductHttp__OperatorUiStaticAssetPath: operatorUiAssetPath,
    },
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
});

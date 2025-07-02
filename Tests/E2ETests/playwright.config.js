import { defineConfig, devices } from '@playwright/test';
import path from "path";

const E2E_PORT = process.env.E2E_PORT || '8080';
const E2E_BASE_URL = process.env.E2E_BASE_URL || `http://localhost:${E2E_PORT}`;
// We expect the dist path to be provided.
let E2E_DIST_PATH = process.env.E2E_DIST_PATH;

if (!E2E_DIST_PATH) {
    console.warn(
        'WARNING: The E2E_DIST_PATH environment variable is not set. Falling back to the default `../../dist` path.'
    );

    E2E_DIST_PATH = path.resolve(__dirname, '../../dist');
}

export default defineConfig({
    testDir: './tests',
    fullyParallel: true,
    forbidOnly: !!process.env.CI,                   // Fail the build on CI if test.only is left in the source code.
    retries: process.env.CI ? 2 : 0,                // Retry on CI only.
    workers: process.env.CI ? 1 : undefined,        // Opt out of parallel tests on CI.
    reporter: 'list',                               // See https://playwright.dev/docs/test-reporters

    webServer: {
        command: `npx http-server "${E2E_DIST_PATH}" -p ${E2E_PORT} --silent`,
        url: E2E_BASE_URL,                                                      // URL to poll to know when the server is ready
        reuseExistingServer: !process.env.CI,                                   // Automatically reuse an existing server on the port if available
    },

    use: {
        baseURL: E2E_BASE_URL,
        trace: 'on-first-retry',
    },

    projects: [{
            name: 'chromium',
            use: { ...devices['Desktop Chrome'] },
        }, {
            name: 'firefox',
            use: { ...devices['Desktop Firefox'] },
        }, {
            name: 'webkit',
            use: { ...devices['Desktop Safari'] },
        },
    ],
});
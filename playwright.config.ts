import { defineConfig, devices } from '@playwright/test';
import { resolve, dirname } from "path";
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));

const E2E_PORT = '8080';
const E2E_BASE_URL = `http://localhost:${E2E_PORT}`;
const DIST_PATH = resolve(__dirname, './dist');

export default defineConfig({
    outputDir: "./Tests/E2ETests/test-results",
    testDir: './Tests/E2ETests/tests',
    fullyParallel: true,
    forbidOnly: !!process.env.CI,                   // Fail the build on CI if test.only is left in the source code.
    retries: process.env.CI ? 2 : 0,                // Retry on CI only.
    workers: process.env.CI ? 1 : undefined,        // Opt out of parallel tests on CI.
    reporter: 'list',                               // See https://playwright.dev/docs/test-reporters
    timeout: 30000,

    webServer: {
        command: `npx http-server "${DIST_PATH}" -p ${E2E_PORT} --silent`,
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
// @ts-check
import { test, expect } from '@playwright/test';

/** @typedef {import('@webapp/types/generated/AppShell.mjs').default} AppShell */

/**
 * This test suite validates the core functionality of the `window.AppShell` object,
 * which serves as the primary JavaScript interface to the C# WASM physics engine.
 */
test.describe('AppShell API and State', () => {

  // This block runs before each test in this suite.
  // It corresponds to the `[SetUp]` method in the original C# tests.
  test.beforeEach(async ({ page }) => {
    // Navigate to the root URL. The `baseURL` is configured in `playwright.config.js`.
    await page.goto('/');

    // This is the readiness gate. We wait until the application has initialized
    // the AppShell on the window object before any test logic runs.
    await page.waitForFunction(() => typeof window.AppShell === 'function', null, { timeout: 15000 });
  });

  test('Confirms that the AppShell global object is created and is a function.', async ({ page }) => {
    const appShellType = await page.evaluate(() => typeof window.AppShell);
    expect(appShellType).toBe('function');
  });

  test.describe('Body Management', () => {
    test('Verifies that window.AppShell.createBody() returns a positive integer.', async ({ page }) => {
      const bodyId = await page.evaluate(() => window.AppShell.createBody());
      expect(bodyId).toBeGreaterThan(0);
    });

    test('Verifies that window.AppShell.deleteBody() returns false when passed an invalid id.', async ({ page }) => {
      const result = await page.evaluate((id) => window.AppShell.deleteBody(id), -1);
      expect(result).toBe(false);
    });

    test('Verifies the full lifecycle: creating a body and then successfully destroying it.', async ({ page }) => {
      // 1. Create the body and get its ID
      const bodyId = await page.evaluate(() => window.AppShell.createBody());
      expect(bodyId, 'Pre-condition failed: A valid id was not created.').toBeGreaterThan(0);

      // 2. Use the valid ID to delete the body
      const result = await page.evaluate((id) => window.AppShell.deleteBody(id), bodyId);
      expect(result, 'deleteBody(id) with a valid id should return true.').toBe(true);
    });
  });

  test.describe('Engine and State', () => {
    test('Verifies that window.AppShell.Bridge.tickEngine() returns an object with the expected shape.', async ({ page }) => {
      const isShapeCorrect = await page.evaluate((dt) => {
        const result = window.AppShell.Bridge.tickEngine(dt);
        if (typeof result !== 'object' || result === null) return false;

        const hasCreated = result.hasOwnProperty('created') && result.created instanceof Set;
        const hasDeleted = result.hasOwnProperty('deleted') && result.deleted instanceof Set;
        const hasUpdated = result.hasOwnProperty('updated') && result.updated instanceof Set;

        return hasCreated && hasDeleted && hasUpdated;
      }, 1); // Pass dt=1

      expect(isShapeCorrect, 'tickEngine() did not return an object with created, deleted, and updated properties of type Set.').toBe(true);
    });

    test('Verifies that window.AppShell.Bridge.simState.bodies is an empty Map on initialization.', async ({ page }) => {
      const isValid = await page.evaluate(() => {
        const simState = window.AppShell?.Bridge?.simState;
        return simState
            && simState.hasOwnProperty('bodies')
            && simState.bodies instanceof Map
            && simState.bodies.size === 0;
      });

      expect(isValid, 'Validation failed: simState.bodies should be an empty Map.').toBe(true);
    });

    test('Verifies that window.AppShell.Bridge.simState.bodyCount is the number 0 on initialization.', async ({ page }) => {
        const isValid = await page.evaluate(() => {
            const simState = window.AppShell?.Bridge?.simState;
            return simState
                && simState.hasOwnProperty('bodyCount')
                && typeof simState.bodyCount === 'number'
                && simState.bodyCount === 0;
        });

      expect(isValid, 'Validation failed: simState.bodyCount should be the number 0.').toBe(true);
    });
  });

  test.describe('Application Data Manager', () => {
    test('Verifies that window.AppShell.appDataManager.bodyData is an empty Map on initialization.', async ({ page }) => {
      const isValid = await page.evaluate(() => {
        const manager = window.AppShell?.appDataManager;
        return manager
            && manager.hasOwnProperty('bodyData')
            && manager.bodyData instanceof Map
            && manager.bodyData.size === 0;
      });

      expect(isValid, 'Validation failed: appDataManager.bodyData should be an empty Map.').toBe(true);
    });

    test('Verifies that window.AppShell.appDataManager.bodyData correctly reflects newly created bodies.', async ({ page }) => {
      // 1. Create two bodies
      const bodyId1 = await page.evaluate(() => window.AppShell.createBody());
      const bodyId2 = await page.evaluate(() => window.AppShell.createBody());

      // 2. Verify the state of the data manager
      const isValid = await page.evaluate(([id1, id2]) => {
        const manager = window.AppShell?.appDataManager;
        const bodiesMap = manager?.bodyData;
        return bodiesMap instanceof Map
            && bodiesMap.size === 2
            && bodiesMap.has(id1)
            && bodiesMap.has(id2);
      }, [bodyId1, bodyId2]);

      expect(isValid, 'Validation failed: appDataManager.bodyData should be a Map with 2 entries containing the new body IDs.').toBe(true);
    });
  });
});
// @ts-check
import { test, expect } from '@playwright/test';
/** @typedef {import('@webapp/types/generated/AppShell.mjs').default} AppShell */

/**
 * This test suite validates the core functionality of the `window.AppShell` object,
 * which serves as the primary JavaScript interface to the C# WASM physics engine.
 */
test.describe('AppShell API and State', () => {
    test.beforeEach(async ({ page }) => {
        await page.goto('/');
        await page.waitForFunction(() => typeof window.AppShell === 'function', null, { timeout: 15000 });
    });

    test('AppShell global object should exist and be a function.', async ({ page }) => {
        const appShellType = await page.evaluate(() => typeof window.AppShell);
        expect(appShellType).toBe('function');
    });

    test.describe('Body Management', () => {
        test('AppShell.createBody() should return a positive integer.', async ({ page }) => {
            const bodyId = await page.evaluate(async () => await window.AppShell.createBody());
            expect(bodyId).toBeGreaterThan(0);
            expect(Number.isSafeInteger(bodyId)).toBe(true);
        });

        test('createBody() should synchronize state across AppDataManager and Bridge.', async ({ page }) => {
            const initialCount = await page.evaluate(() => window.AppShell.Bridge.simState.bodyCount);
            const bodyId = await page.evaluate(async () => await window.AppShell.createBody());
            const state = await page.evaluate((id) => ({
                appDataHasBody: window.AppShell.appDataManager.bodyData.has(id),
                bridgeHasBody: window.AppShell.Bridge.simState.bodies.has(id),
                finalCount: window.AppShell.Bridge.simState.bodyCount
            }), bodyId);
            
            expect(state.appDataHasBody, 'Body should exist in AppDataManager').toBe(true);
            expect(state.bridgeHasBody, 'Body should exist in Bridge simState').toBe(true);
            expect(state.finalCount, 'Bridge bodyCount should be incremented').toBe(initialCount + 1);
        });

        test('AppShell.deleteBody() should return false when passed an invalid id.', async ({ page }) => {
            const result = await page.evaluate(async (id) => await window.AppShell.deleteBody(id), -1);
            expect(result).toBe(false);
        });

        test('AppShell.deleteBody() with the id from a created body should successfully destroy it.', async ({ page }) => {
            const bodyId = await page.evaluate(async () => await window.AppShell.createBody());
            const result = await page.evaluate(async (id) => await window.AppShell.deleteBody(id), bodyId);
            expect(result).toBe(true);
        });

        test('deleteBody() should synchronize state across AppDataManager and Bridge.', async ({ page }) => {
            // Create a body to be deleted
            const bodyId = await page.evaluate(async () => await window.AppShell.createBody());
            const initialCount = await page.evaluate(() => window.AppShell.Bridge.simState.bodyCount);

            // Delete the body
            const deleted = await page.evaluate(async (id) => await window.AppShell.deleteBody(id), bodyId);
            expect(deleted).toBe(true);

            // Get the final state from both state managers
            const state = await page.evaluate((id) => ({
                appDataHasBody: window.AppShell.appDataManager.bodyData.has(id),
                bridgeHasBody: window.AppShell.Bridge.simState.bodies.has(id),
                finalCount: window.AppShell.Bridge.simState.bodyCount
            }), bodyId);
            
            expect(state.appDataHasBody, 'Body should be removed from AppDataManager').toBe(false);
            expect(state.bridgeHasBody, 'Body should be removed from Bridge simState').toBe(false);
            expect(state.finalCount, 'Bridge bodyCount should be decremented').toBe(initialCount - 1);
        });

        test.describe('AppShell.updateBody()', () => {
            test('should successfully update a body and return true', async ({ page }) => {
                const bodyId = await page.evaluate(async () => await window.AppShell.createBody());
                const updates = { name: 'Updated Body Name', tint: '#ff00ff', mass: 42 };

                const result = await page.evaluate(async ({id, data}) => {
                    const success = await window.AppShell.updateBody(id, data);
                    return {
                        success,
                        updatedAppData: window.AppShell.appDataManager.bodyData.get(id),
                        updatedSimData: window.AppShell.Bridge.simState.bodies.get(id)
                    };
                }, { id: bodyId, data: updates });

                expect(result.success).toBe(true);
                expect(result.updatedAppData.name).toBe(updates.name);
                expect(result.updatedAppData.tint).toBe(updates.tint);
                expect(result.updatedSimData.mass).toBe(updates.mass);
            });

            test('should return false when updating a non-existent body ID', async ({ page }) => {
                const result = await page.evaluate(async () => {
                    return await window.AppShell.updateBody(-1, { name: 'I do not exist' });
                });
                expect(result).toBe(false);
            });

            test('should throw a synchronization error if the body is missing from appData', async ({ page }) => {
                // Create a body so it exists in both state managers
                const bodyId = await page.evaluate(async () => await window.AppShell.createBody());

                // Manually de-sync the state by removing the body from appDataManager only
                await page.evaluate((id) => {
                    window.AppShell.appDataManager.bodyData.delete(id);
                }, bodyId);

                // Attempt to update the body. This should now fail at the appDataSuccess check.
                // We expect the promise returned by page.evaluate to be rejected.
                await expect(page.evaluate(async (id) => {
                    await window.AppShell.updateBody(id, { name: 'This will fail' });
                }, bodyId)).rejects.toThrow(`Synchronisation error: Body id "${bodyId}" in sim data but not in appData.`);
            });
        });
    });

    test.describe('Engine and State', () => {
        test('AppShell.Bridge.tickEngine() should return an object with the expected shape.', async ({ page }) => {
            const isValidShape = await page.evaluate((deltaTime) => {
                const tickDiff = window.AppShell.Bridge.tickEngine(deltaTime);
                return tickDiff.created instanceof Set 
                    && tickDiff.created.size === 0
                    && tickDiff.deleted instanceof Set
                    && tickDiff.deleted.size === 0
                    && tickDiff.updated instanceof Set
                    && tickDiff.updated.size === 0
            }, 1);
            expect(isValidShape).toBe(true);
        });

        test('AppShell.Bridge.simState.bodies should be an empty Map on initialization.', async ({ page }) => {
            const isEmptyMap = await page.evaluate(() => {
                const bodies = window.AppShell?.Bridge?.simState?.bodies;
                return bodies instanceof Map 
                    && bodies.size === 0;
            });
            expect(isEmptyMap).toBe(true);
        });

        test('AppShell.Bridge.simState.bodyCount should be falsy on initialization.', async ({ page }) => {
            const bodyCount = await page.evaluate(() => window.AppShell?.Bridge?.simState?.bodyCount);
            expect(bodyCount).toBeFalsy();
        });
    });

    test.describe('Application Data Manager', () => {
        test('AppShell.appDataManager.bodyData should be an empty Map on initialization.', async ({ page }) => {
            const isEmptyMap = await page.evaluate(() => {
                const bodyData = window.AppShell?.appDataManager?.bodyData;
                return bodyData instanceof Map 
                    && bodyData.size === 0;
            });
            expect(isEmptyMap).toBe(true);
        });

        test('AppShell.appDataManager.bodyData should correctly reflect newly created bodies.', async ({ page }) => {
            const bodyId1 = await page.evaluate(async() => await window.AppShell.createBody());
            const bodyId2 = await page.evaluate(async () => await window.AppShell.createBody());
            const hasCreatedBodies = await page.evaluate(([id1, id2]) => {
                const bodyData = window.AppShell?.appDataManager?.bodyData;
                return bodyData instanceof Map
                    && bodyData.size === 2
                    && bodyData.has(id1)
                    && bodyData.has(id2)
            }, [bodyId1, bodyId2]);
            expect(hasCreatedBodies).toBe(true);
        });
    });

    test.describe('Render Loop Control', () => {
        test('startLoop() and stopLoop() should toggle the canvasView render loop state', async ({ page }) => {
            const initialState = await page.evaluate(() => window.AppShell.canvasView.renderLoopStopped);
            expect(initialState, 'Render loop should be stopped initially').toBe(true);

            // Test starting the loop
            await page.evaluate(() => window.AppShell.startLoop());
            const startedState = await page.evaluate(() => window.AppShell.canvasView.renderLoopStopped);
            expect(startedState, 'Render loop should be running after calling startLoop').toBe(false);

            // Test stopping the loop
            await page.evaluate(() => window.AppShell.stopLoop());
            const stoppedState = await page.evaluate(() => window.AppShell.canvasView.renderLoopStopped);
            expect(stoppedState, 'Render loop should be stopped after calling stopLoop').toBe(true);
        });
    });
});
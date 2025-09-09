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
        test('AppShell.createBody() should return a non-negative integer.', async ({ page }) => {
            const bodyId = await page.evaluate(async () => await window.AppShell.createBody());
            expect(bodyId).toBeGreaterThanOrEqual(0);
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

            test('should return false if body is missing from appData before queuing update', async ({ page }) => {
                const bodyId = await page.evaluate(async () => await window.AppShell.createBody());
            
                // Manually de-sync the state by removing the body from appDataManager only
                await page.evaluate((id) => {
                    window.AppShell.appDataManager.bodyData.delete(id);
                }, bodyId);
            
                // This should fail the first check in updateBody, causing it to return false.
                const result = await page.evaluate(async (id) => {
                    return await window.AppShell.updateBody(id, { name: 'This will return false' });
                }, bodyId);
            
                expect(result).toBe(false);
            });

            test('should throw a synchronization error if the bridge update fails', async ({ page }) => {
                const bodyId = 2;
                // Force the metadata out of sync with the physics data
                await page.evaluate((id) => window.AppShell.appDataManager.onCreateBody(id), bodyId);

                const hasBodyMetaData = await page.evaluate((id) => window.AppShell.appDataManager.bodyData.has(id), bodyId);
                expect(hasBodyMetaData).toBe(true);

                const hasBodySimData = await page.evaluate((id) => window.AppShell.Bridge.simState.bodies.has(id), bodyId);
                expect(hasBodySimData).toBe(false);
            
                const throws = await page.evaluate(async(id) => {
                    try {
                        await window.AppShell.updateBody(id, {mass: 200});
                        return false;
                    } catch(err) {
                        return true;
                    }
                }, bodyId);

                expect(throws).toBe(true);
            });
        });
    });

    test.describe('Engine and State', () => {
        test('AppShell.Bridge.simState.bodies should be an empty Map on initialization.', async ({ page }) => {
            const isMap = await page.evaluate(() => window.AppShell?.Bridge?.simState?.bodies instanceof Map);
            expect(isMap).toBe(true);

            const mapSize = await page.evaluate(() => window.AppShell?.Bridge?.simState?.bodies.size);
            expect(mapSize).toBe(0);
        });

        test('AppShell.Bridge.simState.bodyCount should be close to 0 on initialization.', async ({ page }) => {
            const bodyCount = await page.evaluate(() => window.AppShell?.Bridge?.simState?.bodyCount);
            expect(bodyCount).toBeCloseTo(0);
        });
    });

    test.describe('Application Data Manager', () => {
        test('AppShell.appDataManager.bodyData should be an empty Map on initialization.', async ({ page }) => {
            const isMap = await page.evaluate(() => window.AppShell?.appDataManager?.bodyData instanceof Map);
            expect(isMap).toBe(true);

            const mapSize = await page.evaluate(() => window.AppShell?.appDataManager?.bodyData.size);
            expect(mapSize).toBe(0);
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

    test.describe('Pausing', () => {
        test('AppShell.paused should be true on initialization', async ({ page }) => {
            const isPaused = await page.evaluate(() => window.AppShell.paused);
            expect(isPaused, 'Simulation should be paused initially').toBe(true);
        });
    
        test('togglePause() should switch the paused state', async ({ page }) => {
            await page.evaluate(() => window.AppShell.togglePause());
            let isPaused = await page.evaluate(() => window.AppShell.paused);
            expect(isPaused, 'Should be running after first toggle').toBe(false);
            
            await page.evaluate(() => window.AppShell.togglePause());
            isPaused = await page.evaluate(() => window.AppShell.paused);
            expect(isPaused, 'Should be paused after second toggle').toBe(true);
        });
    
        test('togglePause(force) should set the paused state directly', async ({ page }) => {
            await page.evaluate(() => window.AppShell.togglePause(false));
            let isPaused = await page.evaluate(() => window.AppShell.paused);
            expect(isPaused, 'Should be running after togglePause(false)').toBe(false);
    
            await page.evaluate(() => window.AppShell.togglePause(true));
            isPaused = await page.evaluate(() => window.AppShell.paused);
            expect(isPaused, 'Should be paused after togglePause(true)').toBe(true);
        });
    });
});
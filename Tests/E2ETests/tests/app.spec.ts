import { test, expect } from '@playwright/test';

test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await page.waitForFunction(() => !!window.App, null, { timeout: 15000 });
});

test.describe("App Initialization", () => {
    test("window.App should have debug-only properties", async ({page}) => {
        const appProperties = await page.evaluate(() => ({
            hasBridge: !!window.App.parts.bridge,
            hasPixiHandler: !!window.App.parts.pixi,
            hasUiData: !!window.App.parts.views,
            hasNotifications: !!window.App.parts.notif,
            hasAppData: !!window.App.parts.appData,
            hasResolver: !!window.App.parts.resolver,
            hasUiManager: !!window.App.parts.uiManager
        }));

        for (const [key, value] of Object.entries(appProperties)) {
            expect(value, `window.App should have the '${key}' property initialized.`).toBe(true);
        }
    });

    test("Console should contain initialization confirmation", async ({ page }) => {
        const messages = await page.consoleMessages();
        expect(messages.some(m => m.text() === "Begin initialization...")).toBe(true);
        expect(messages.some(m => m.text() === "Initialization complete!")).toBe(true);
    });
});

test.describe('App API and State', () => {
    test.describe('Body Management', () => {
        test('createBody() should synchronize state across AppData and Bridge.', async ({ page }) => {
            const res = await page.evaluate(async() => {
                const initialCount = window.App.parts.bridge.state.sim.bodyCount;
                const bodyId = await window.App.createBody();
                return {
                    initialCount,
                    appDataHasBody: window.App.parts.appData.state.bodies.has(bodyId),
                    bridgeHasBody: window.App.parts.bridge.state.bodies.has(bodyId),
                    finalCount: window.App.parts.bridge.state.sim.bodyCount
                }
            });
            expect(res.appDataHasBody, 'Body should exist in AppData').toBe(true);
            expect(res.bridgeHasBody, 'Body should exist in Bridge state').toBe(true);
            expect(res.finalCount, 'Bridge bodyCount should be incremented').toBe(res.initialCount + 1);
        });

        test('App.deleteBody() with the id from a created body should successfully destroy it.', async ({ page }) => {
            const hasBody = await page.evaluate(async () => {
                const bodyId = await window.App.createBody();
                await window.App.deleteBody(bodyId);
                return window.App.parts.appData.state.bodies.has(bodyId)
            });
            expect(hasBody).toBe(false);
        });

        test("Bridge.state.bodies.size, AppData.state.bodies.size, and Bridge.state.sim.bodyCount should always be synchronized.", async ({ page }) => {
            const res = await page.evaluate(async() => {
                const initial = {
                    bodyCount: window.App.parts.bridge.state.sim.bodyCount,
                    physicsSize: window.App.parts.bridge.state.bodies.size,
                    appSize: window.App.parts.appData.state.bodies.size
                };
                
                const bodyId = await window.App.createBody();
                const afterCreate = {
                    bodyCount: window.App.parts.bridge.state.sim.bodyCount,
                    physicsSize: window.App.parts.bridge.state.bodies.size,
                    appSize: window.App.parts.appData.state.bodies.size
                };

                await window.App.deleteBody(bodyId);
                const afterDelete = {
                    bodyCount: window.App.parts.bridge.state.sim.bodyCount,
                    physicsSize: window.App.parts.bridge.state.bodies.size,
                    appSize: window.App.parts.appData.state.bodies.size
                };

                return { initial, afterCreate, afterDelete }
            });

            const { initial, afterCreate, afterDelete } = res;

            expect(initial.bodyCount).toBe(initial.appSize);
            expect(initial.bodyCount).toBe(initial.physicsSize);

            expect(afterCreate.bodyCount).toBe(afterCreate.appSize);
            expect(afterCreate.bodyCount).toBe(afterCreate.physicsSize);

            expect(afterDelete.bodyCount).toBe(afterDelete.appSize);
            expect(afterDelete.bodyCount).toBe(afterDelete.physicsSize);
        });

        test('deleteBody() should synchronize state across AppData and Bridge.', async ({ page }) => {
            const res = await page.evaluate(async() => {
                // Create a body to be deleted
                const bodyId = await window.App.createBody();
                const afterCreate = {
                    physics: window.App.parts.bridge.state.bodies.has(bodyId),
                    app: window.App.parts.appData.state.bodies.has(bodyId)
                };

                // Delete the body
                await window.App.deleteBody(bodyId);
                const afterDelete = {
                    physics: window.App.parts.bridge.state.bodies.has(bodyId),
                    app: window.App.parts.appData.state.bodies.has(bodyId)
                };

                // Get the final state from both state managers
                return { afterCreate, afterDelete }
            });

            const { afterCreate, afterDelete } = res;

            expect(afterCreate.physics).toBe(true);
            expect(afterCreate.app).toBe(true);
            expect(afterDelete.physics).toBe(false);
            expect(afterDelete.app).toBe(false);
        });

        test.describe('App.updateBody()', () => {
            test("should successfully update a body's physics data and return true", async ({ page }) => {
                const bodyId = await page.evaluate(async () => await window.App.createBody());
                const updates = { physics: { enabled: true, mass: 42 } };

                const result = await page.evaluate(async ({id, data}) => {
                    const success = await window.App.updateBody(id, data);
                    return {
                        success,
                        physicsData: window.App.parts.bridge.state.bodies.get(id)
                    };
                }, { id: bodyId, data: updates });

                expect(result.success).toBe(true);
                expect(result?.physicsData?.mass).toBe(updates.physics.mass);
                expect(result?.physicsData?.enabled).toBe(updates.physics.enabled); // TODO fix type mismatch of physicsData in Bridge
            });

            test("should successfully update a body's app data and return true", async ({ page }) => {
                const bodyId = await page.evaluate(async () => await window.App.createBody());
                const updates = { app: { name: "Updated Body Name", tint: "#ff00ff" } };

                const result = await page.evaluate(async ({id, data}) => {
                    const success = await window.App.updateBody(id, data);
                    return {
                        success,
                        appData: window.App.parts.appData.state.bodies.get(id)
                    };
                }, { id: bodyId, data: updates });

                expect(result.success).toBe(true);
                expect(result?.appData?.name).toBe(updates.app.name);
                expect(result?.appData?.tint).toBe(updates.app.tint);
            });

            test('should return false when updating a non-existent body ID', async ({ page }) => {
                const result = await page.evaluate(async () => {
                    return await window.App.updateBody(-1, { app: { name: 'I do not exist' } });
                });
                expect(result).toBe(false);
            });
        });
    });

    // TODO: Replace with unit tests
    test.describe('Engine and State', () => {   
        test('App.Bridge.state.bodies should be empty on initialization.', async ({ page }) => {
            const mapSize = await page.evaluate(() => window.App.parts.bridge.state.bodies.size);
            expect(mapSize).toBe(0);
        });

        test('App.Bridge.state.sim.bodyCount should be 0 on initialization.', async ({ page }) => {
            const bodyCount = await page.evaluate(() => window.App.parts.bridge.state.sim.bodyCount);
            expect(bodyCount).toBe(0);
        });
    });

    // TODO: Replace with unit tests
    test.describe('AppData', () => {
        test('App.appData.state.bodies should be empty on initialization.', async ({ page }) => {
            const mapSize = await page.evaluate(() => window.App.parts.appData.state.bodies.size);
            expect(mapSize).toBe(0);
        });

        test('App.appData.state.bodies should correctly reflect newly created bodies.', async ({ page }) => {
            const bodyId1 = await page.evaluate(async() => await window.App.createBody());
            const bodyId2 = await page.evaluate(async () => await window.App.createBody());
            const hasCreatedBodies = await page.evaluate(([id1, id2]) => {
                const bodyData = window.App.parts.appData.state.bodies;
                return bodyData instanceof Map
                    && bodyData.size === 2
                    && bodyData.has(id1)
                    && bodyData.has(id2)
            }, [bodyId1, bodyId2]);
            expect(hasCreatedBodies).toBe(true);
        });
    });
});
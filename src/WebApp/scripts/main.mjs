import AppShell from './AppShell.mjs';

//#region Fatal Error Handling

/**
 * Handles fatal errors by logging them, displaying a UI overlay,
 * and preventing further interaction.
 * @param {string|Error|PromiseRejectionEvent} errorInfo - The error information.
 */
function handleFatalError(errorInfo) {
    console.error(`Fatal, unrecoverable error:`, errorInfo);
    document.getElementById('error-fatal')
        .classList.remove("hidden");
}

window.addEventListener('error', (event) => {
    handleFatalError(event.error || event.message)
});

window.addEventListener('unhandledrejection', (event) => {
    handleFatalError(event.reason);
});

//#endregion

window.addEventListener("load", async (event) => {
    await AppShell.initialize();
});

// TEST

document.getElementById("run-test").addEventListener("click", () => {
    const iterationEle = /** @type {HTMLInputElement} */ (document.getElementById("test-iterations"));
    const iterations =  Number(iterationEle.value);

    const growBodiesEle = /** @type {HTMLInputElement} */ (document.getElementById("test-bodies-per-run"));
    const addBodies = Number(growBodiesEle.value);

    const prev = performance.now();
    for(let i = 0; i < iterations; i++) {
        for(let j = 0; j < addBodies; j++) AppShell.createBody();
        AppShell.Bridge.tickEngine(1);
    }
    const post = performance.now();
    const diff = post - prev;

    const totalBodies = AppShell.Bridge.simState.bodyCount;
    document.getElementById("test-result").innerHTML = `
        <p>Total Bodies: ${totalBodies}</p>
        <p>Diff: ${diff.toPrecision()}</p>
        <p>Prev: ${prev.toPrecision()}</p>
        <p>Post: ${post.toPrecision()}</p>
    `;
});
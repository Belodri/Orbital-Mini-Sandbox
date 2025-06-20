import AppShell from './AppShell.mjs';

//#region Fatal Error Handling

/**
 * Handles fatal errors by logging them, displaying a UI overlay,
 * and preventing further interaction.
 * @param {string|Error|PromiseRejectionEvent} errorInfo - The error information.
 */
function handleFatalError(errorInfo) {
    console.error(`Fatal, unrecoverable error:`, errorInfo);
    const errorElement = document.getElementById('error-fatal')
        .toggleAttribute("hidden", false);
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
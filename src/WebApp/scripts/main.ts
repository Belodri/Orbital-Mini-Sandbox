import App from './App.mjs';

//#region Fatal Error Handling

/**
 * Handles fatal errors by logging them, displaying a UI overlay,
 * and preventing further interaction.
 * @param errorInfo - The error information.
 */
function handleFatalError(errorInfo: string | Error | PromiseRejectionEvent) {
    console.error(`Fatal, unrecoverable error:`, errorInfo);
    document.getElementById('error-fatal')!
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
    await App.initialize();
});

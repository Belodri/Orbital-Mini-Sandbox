import App from './App.ts';

//#region Fatal Error Handling

/**
 * Handles fatal errors by logging them, displaying a UI overlay,
 * and preventing further interaction.
 * @param errorInfo - The error information.
 */
function handleFatalError(errorInfo: string | Error | PromiseRejectionEvent) {
    console.error(`Fatal, unrecoverable error:`, errorInfo);
    const element = document.getElementById('error-fatal');
    if(element) element.classList.remove("hidden");
    else {
        console.error("The fatal error overlay element ('#error-fatal') was not found in the DOM.");
        alert("The fatal error overlay element ('#error-fatal') was not found in the DOM.");
    }
}

window.addEventListener('error', (event) => {
    handleFatalError(event.error || event.message)
});

window.addEventListener('unhandledrejection', (event) => {
    handleFatalError(event.reason);
});

//#endregion

window.addEventListener("load", async (event) => {
    await App.init();
});

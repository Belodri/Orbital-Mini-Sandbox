import { DotNet } from '../_framework/dotnet.js';

let engineBridge;
let invokeMethodAsync;



async function initialize() {
    console.log("Initializing .NET runtime...");

    // Create the DotNet runtime instance.
    const dotnet = await DotNet.create();

    // Get the assembly exports for our 'Bridge' project.
    const exports = await dotnet.getAssemblyExports("Bridge");

    // Get a direct reference to the invokeMethodAsync function for convenience.
    invokeMethodAsync = dotnet.invokeMethodAsync;

    // The 'engineBridge' holds all our [JSExport]'d static methods.
    engineBridge = exports.Bridge.EngineBridge;

    console.log(".NET runtime initialized. Starting tests...");
    await test();
}

async function test() {
    let tickErrText;

    console.log("--- Test Run ---");

    tickErrText = await invokeMethodAsync("Bridge", "GetTickErrorText");
    console.log(`first InvokeCall: ${tickErrText}`);    // expected ""

    engineBridge.Tick();

    tickErrText = await invokeMethodAsync("Bridge", "GetTickErrorText");
    console.log(`second InvokeCall: ${tickErrText}`); // expected "TESTING"

    engineBridge.Tick();

    tickErrText = await invokeMethodAsync("Bridge", "GetTickErrorText");
    console.log(`third InvokeCall: ${tickErrText}`); // expected ""

    console.log("--- Test Complete ---");
}

try {
    await initialize();
} catch(err) {
    console.error(err);
}

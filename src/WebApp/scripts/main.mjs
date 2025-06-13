import Bridge from '../bridge/bridge.mjs';

async function initialize() {
    await Bridge.initialize();
    //await test();
}

async function test() {
    let tickErrText;

    console.log("--- Test Run ---");

    tickErrText = await Bridge.callAsync("GetTickErrorText");
    console.log(`Expect: ""; Result: ${tickErrText}`);

    Bridge.callSync("Tick");

    tickErrText = await Bridge.callAsync("GetTickErrorText");
    console.log(`Expect: "TESTING"; Result: ${tickErrText}`);

    Bridge.callSync("Tick");

    tickErrText = await Bridge.callAsync("GetTickErrorText");
    console.log(`Expect: ""; Result: ${tickErrText}`);

    console.log("--- Test Complete ---");
}

try {
    await initialize();
} catch(err) {
    console.error(err);
}

import Bridge from '../bridge/bridge.mjs';

async function initialize() {
    await Bridge.initialize();
    test();
}

function test() {
    console.log("--- Test Run ---");

    console.log("Creating Bodies");
    Bridge._createTestSim(5);

    console.log("Run Tick")
    Bridge.tickEngine();

    console.log(Bridge.simState);

    console.log("--- Test Complete ---");
}

try {
    await initialize();
} catch(err) {
    console.error(err);
}

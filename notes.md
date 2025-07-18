# Resources

https://webassembly.org/features/
https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop/?view=aspnetcore-9.0#type-mappings
https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop/wasm-browser-app?view=aspnetcore-9.0#javascript-interop-on-


# Day Log

- 11/06/2025 - Basic repo setup; after some experimentation realized Blazor is unnecessary for the Bridge; investigated alternatives
- 12/06/2025 - Reworked Bridge; refined build tools; created basic JS consumer; 1st test of JS <-> C# communication SUCCESSFUL
- 13/06/2025 - Added dual shared memory buffers and dynamic memory reallocation; moved bridge.js into EngineBridge project;
- 14/06/2025 - Added dynamic layout aware JavaScript reader; Added custom Vector2D math library; Scaffolded simulation & body models; Defined the data transfer contracts between `PhysicsEngine` and `EngineBridge`
- 16/06/2025 - Added C# data writer with self-configuring layout; added GC-efficient, self-configuring JavaScript reader with primitive delta reporting; first successful end-to-end data pipeline test
- 17/06/2025 - Refactored memory layout to a C# SSOT; created build tool (TsTypeGen) for automatic TypeScript type generation; completed major PDD update.
- 20/06/2025 - Began WebApp scaffolding; Added fatal error handling UI & styles to WebApp; Added AppShell class with basic initialization and logging
- 21/06/2025 - Added and tested Notification class for WebApp; Updated build tools to add WebApp styles; Updated PDD.
- 23/06/2025 - Fixed formatting issues with TsTypeGen tool; Added Preset system; discovered major performance issue on Firefox (not present on Chrome; likely to do with shared memory management) and started investigating solutions
- 25/06/2025 - Performance issue only caused by opening dev console; Added improved memory write operation regardless; Added update and delete methods for bodies; Added full integration testing via Playwright
- 26/06/2025 - Added Pixi.js; Added render loop; Added Vite for WebApp build and bundling and updated build tool accordingly.
- 27/06/2025 - Refactored Bridge and Physics for testability and clarity; Added partial body updates; Added dedicated test projects for Physics and Bridge; Fixed build issues; Integrated BodyTokens in render loop 
- 01/07/2025 - Fixed detaching buffer views, preset loading errors, and missing partial body updates implementation; Added and tested full camera controls (zoom and pan);
- 02/07/2025 - Replaced C# integration tests with JS E2E tests; Refactored build and type generation tools; Updated PDD; Added command queue to Bridge and refactored several methods of the JS API to return promises. THIS BREAKS THE WEBAPP AND TESTS
- 03/07/2025 - Fixed bug in Bridge; Refactored WebApp to work with recent API changes; Updated and added tests for physics, engineBridge, and E2E; Added deterministic offset for newly created celestial bodies; Began work on the quad tree;
- 09/07/2025 - Refactored WebApp and Bridge to enable a reactive, event-based render loop, reduce state synchronization issues, and decouple the individual components from the central controller. Fixed floating-point error exclusive to webkit browsers. Updated tests and PDD.
- 10/07/2025 - Refactored Simulation & CelestialBody for clarity and body management. Refactored QuadTreeNode into a state-machine-like evaluator. Added Grid class. Added Tests for QuadTreeNode and Grid, refactored PhysicsEngineTests.
- 11/07/2025 - Added Timer class; Split public from internal DTOs and added DTOMapper class to handle conversions; Refactored CelestialBody and Simulation; PHYSICS UNIT TESTS NOT YET UPDATED
- 16/07/2025 - Added Calculator class; Refactored QuadTreeNode and AABB for performance and access; Finalized Tick workflow, updated physics unit tests; Updated PDD to reflect Tick workflow changes;
- 17/07/2025 - Refactored all components of the Physics project to use the interface pattern for clarity, maintainability, and testability; Clarified PhysicsEngine api and data contracts; Updated Bridge accordingly;
- 18/07/2025 - Added various integration algorithms (Symplectic Euler, Runge-Kutta-4, Velocity-Valet) via strategy pattern to Calculator; Refactored CelestialBody to simplify update event and add acceleration; Integrate new simulation and body properties across the stack; Update all tests;
- 19/07/2025 - Clarify and fix undocumented and inconsistent units ( & spend several hours on a unit conversion utility that turned out to be completely unnecessary); Improve documentation across the stack;
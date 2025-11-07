# Resources

https://webassembly.org/features/
https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop/?view=aspnetcore-9.0#type-mappings
https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop/wasm-browser-app?view=aspnetcore-9.0#javascript-interop-on-

### Papers
- [Time stepping N-Body simulations](https://ar5iv.labs.arxiv.org/html/astro-ph/9710043#:~:text=and%20show%20that%20if%20is,that%20reflexivity%20is%20the%20key)
- [Testing and tuning symplectic integrators for Hybrid Monte Carlo algorithm in lattice QCD](https://arxiv.org/abs/hep-lat/0505020)
- [BEHALF - BarnEs-Hut ALgorithm For CS205](https://anaroxanapop.github.io/behalf/#Nbody)
- [Symplectic analytically integrable decomposition algorithms: classification, derivation, and application to molecular dynamics, quantum and celestial mechanics simulations](https://www.sciencedirect.com/science/article/abs/pii/S0010465502007543)
- [Using numerical methods to solve the Gravitational n-Body Problem & represent the result graphically using OpenGL](https://www.maths.tcd.ie/~btyrrel/nbody.pdf)
- [Time integration issues](https://courses.physics.ucsd.edu/2019/Winter/physics141/Assignments/volker_error.pdf)
- [Advanced Character Physics](https://www.researchgate.net/publication/228599597_Advanced_character_physics)
- [Dynamic Graphics Project - Chapter 7 Implementation Issues](https://www.dgp.toronto.edu/~davet/phd/tonnesen-thesis-pdf/tonnesen-7.pdf)

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
- 05/08/2025 -  Improve testability and clarity of Calculator class; Write unit tests to verify physics calculations; Discover flaws in calculations; Struggle with understanding the math behind VelocityVerlet and why the current implementation doesn't work;
- 06/08/2025 - Discover major misunderstandings regarding the interaction between integration algorithms and the tick workflow; Find relevant papers; Study math;
- 07/08/2025 - Study integration math and figure out how to apply it efficiently in the context of the simulation loop; Plan the substantial rewrites of Simulation, Calculator, and Timer necessary to implement the Position variant of the Velocity-Verlet integration.
- 08/08/2025 - Work on solidifying the understanding and mental model of the integration algorithm and the challenges of its implementation (see [IntegrationAlgorithm.md](IntegrationAlgorithm.md));
- 13/08/2025 - Take a brief break from math; Add performance optimized QuadTree class with mutable struct Nodes and object pooling to reduce GC pressure (to replace Grid and QuadTreeNode);
- 14/08/2025 - Decide integration algorithm & workflow; Rewrite Simulation to implement Velocity-Verlet integration and the new QuadTree. Refactor Timer for consistent, configurable time steps; Refactor Calculator into a dumb and blind, stateful utility class; Refactor PhysicsEngine to replace DTO-based data transfer in hot path with live view of select simulation state data via new SimulationView class; Refactor DTO and DataMapper to reflect changes and remove redundant code; IMPORTANT: This breaks Bridge, and most physics tests
- 19/08/2025 - Remove redundant Grid & QuadTreeNode classes; Refactor Bridge to implement the newly reworked PhysicsEngine; Update PDD and IntegrationAlgorithm documentation to reflect the recent PhysicsEngine rework;
- 20/08/2025 - Update PhysicsEngineTests, CalculatorTests, TestHelpers & PresetTests following recent refactor; Remove tests for removed Grid & QuadTreeNode classes; Fix major unit conversion bug in Calculator; Fixed major error in QuadTree.Node; Renamed PhysicsEngine methods for clarity (`Load()` => `Import()` & `GetBaseData()` => `Export()`);
- 21/08/2025 - Implement test suite for new QuadTree class; Debug and fix edge-case logic error in the QuadTree.Node's acceleration calculation and improve documentation for acceleration logic;
- 25/08/2025 - Implement integration test suite for Simulation as a whole; Fix numerous bugs and issues with calculations and state management across the PhysicsEngine;
- 26/08/2025 - Add further simulation tests; Create dedicated BodyManager class to allow Simulation to focus on orchestration; All tests updated and passed; IMPORTANT: PDD not yet updated with recent architectural changes;
- 27/08/2025 - Update PDD and IntegrationAlgorithm documents with recent architectural changes; Add test suite for new BodyManager class; Add comprehensive (mostly) black-box integration test suite for Simulation;
- 03/09/2025 - Add handling of negative mass bodies; Add position bounds for bodies and OutOfBounds getter; Add final planned tests for Simulation;
- 07/09/2025 - Update Bridge API and fix minor issues; Add C# logging utility to bridge;
- 09/09/2025 - Update build scripts with `--debug` and `--release` flags; Fix various minor issues with the Bridge; Fix outdated E2ETest case; Remove acceleration parameters from the `updateBody()` API method; Add `updateSimulation()` method to WebApp interface; Remove initial test UI and plan UI implementation;
- 10/09/2025 - Rename `AppShell` to `App` for clarity and conciseness; Add [Front_End_Alpha.md](Front_End_Alpha.md) to plan UI design and implementation details during early iteration (will be added to PDD once solidified); Add ViewModel and ViewModelMovable base classes for UI components;
- 13/09/2025 - Evaluate and compare different architecture models for data and control flow, ultimately choosing a render-synched game-engine-like model; Document evaluated models in Front_End_Alpha.md;
- 16/09/2025 - Improve Front_End_Alpha.md; Begin TypeScript rewrite of Bridge for type safety, maintainability, and to improve separation of concerns;
- 18/09/2025 - Finish TypeScript rewrite of Bridge; Update build pipeline; Update WebApp dependencies; IMPORTANT: 3 broken E2E tests & PDD not yet updated
- 19/09/2025 - Fix tests; Update PDD following recent rewrite of Bridge; Improve Bridge and PhysicsEngine APIs; Untangle and completely overhaul the mess that the build pipeline had grown to;
- 20/09/2025 - Clean repo, deleting code, types, and scripts now made redundant by the overhauled structure and build pipeline (-3k lines); Update the TsTypeGen tool to also infer appropriate TS types from C# code; Add custom C# source code generation to Bridge, allowing for full type safety and casting of boolean values via self-configuring type-inference during runtime; All tests passing;
- 24/09/2025 - Add various component classes to WebApp as part of the TS rewrite: Generic, performance-optimized CommandQueue, AppDataStore to manage front-end exlusive data and provide update diffs; Add UiManager class for efficient, centralized management of UI component lifecycle and rendering; Note: Front_End_Alpha.md has not been updated yet;
- 27/09/2025 - Update Front_End_Alpha.md; Add DeferredResolver to replace the CommandQueue; Rewrite AppData and ViewModel in TypeScript and refactor their implementation according to Front_End_Alpha
- 01/10/2025 - Refactor and rewrite Notifications in TypeScript
- 02/10/2025 - Add singleton UiData to WebApp to act as static data provider for UI components and adapter and facade between multi-source state (physics & appData) and consuming UI
- 03/10/2025 - Replace CanvasView.js with TypeScript PixiHandler; Remove unnecessary work from UiData; Add UiManager as the consumer of UiData and owner and orchestrator of UI components;
- 06/10/2025 - Refactor Bridge to be fully synchronous as required by App, removing TimeoutLoopHandler and CommandQueue classes; Fix numerous small issues with various App components; Add App.ts; Project is now fully converted to TypeScript; Builds and unit tests succeed, only E2E tests need to be updated;
- 07/10/2025 - Refactor E2E tests; Fix various bugs with Bridge & App; Add minimal logger utility; Add `__DEBUG__` flag via vite to isolate non-release code; Remove all (now obsolete) JS code; 
- 08/10/2025 - Fix minor issues with App and vite.config; update gitignore and untrack source-generated types; Refactor Bridge to add complete runtime type safety and type casting for the JS environment (differentiate between `int` and `double`, support `bool`, easily expandable), improve readability and clarity, remove redundant code; Extract C# layout generation logic into its own component; 
- 14/10/2025 - Switch TS from static-singleton to DI-based instanced architecture and add interfaces to nearly all classes for improved testability; Improve error handling in main.ts; Add Controller and RenderLoop classes as orchestrators; Extract several classes into their own files (ViewModelMovable, NotificationSlots, CameraControls) and reorganize App file structure for clarity;
- 15/10/2025 - Fix various bugs in WebApp; Update E2E tests; Add vitest for TypeScript unit testing; Add test suite for DataViews.ts;
- 16/10/2025 - Add test suite for AppData.ts; Fix various bugs and issues with AppData; Remove memory write & read from preset loading in Bridge which the RenderLoop is responsible for orchestrating; Document preset loading process in Front_End_Alpha;
- 17/10/2025 - Figure out an intuitive and scalable system for UI component implementation; Configure vite to allow static HTML asset imports; Refactor ViewModel and ViewModelMovable base classes for improved modularity and flexibility; Add TimeControls UI component; Rearrange initialization order to be more linear; Update Front_End_Alpha;
- 22/10/2025 - Add a FormHandler utility class to read and write input elements with robust type safety; Note: Took ages to get the typing right and after I was done I think I found a better solution entirely... Still going to leave it in the codebase for now, might still be useful;
- 23/10/2025 - Add TypeFields for robust and adaptable input validation, split into abstract `BaseTypeField`, type-specific `StringField`, `NumberField`, and `BooleanField`;
- 03/11/2025 - Add `TypedObjectField` with type-safe, recursive object schema and data validation and casting;
- 04/11/2025 - Struggle with figuring out how to bridge the gap between HTML template strings, DOM, and the underlying data layer, all while avoiding code duplication as much as possible, sticking to a SSOT, and offering a simple and flexible facade that hides away the complexity of event handling and data validation; Create several prototypes but none I'm even remotely happy with;
- 07/11/2025 - Add HTMLStringTemplate utility to allow HTML strings with simple moustache placeholders to be parsed into DocumentFragments from potentially unsafe data while ensuring XSS injection safety; This prevents issues with duplicate ids and allows wider reusability of templates; Can easily be expanded to allow for more complex templating such as dynamic select options from trusted data; Add a regex generator utility to create RegExp from string arrays;
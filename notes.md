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
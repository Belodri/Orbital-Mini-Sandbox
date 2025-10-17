# Preface and Context
This document is not a replacement for the [Project's PDD](PDD.md), which also outlines aspects touched on here, but rather a place to document the rapidly iterating designs and ideas during alpha development. Unlike the PDD, this document retains all previously considered but ultimately discarded designs.
When the project transitions into its beta phase, the PDD will be updated to reflect the actual implementation and this document will be archived.

---

# UI/UX Requirements, Front-End Architecture & Implementation
This section outlines the requirements for the UI/UX, the architecture and design that was chosen, and details for how they were implemented. It is being updated continuously as requirements, architecture, and designs change during development.


## Constraints & Considerations
- Vanilla JS & CSS
- No fontend libraries like react or svelte
- No ARIA attributes in the early stages (Will be retrofitted once the design and architecture solidifies and the UI/UX enters a more stable beta phase.)

---

## General Architecture Choices
- **CSS:** All styles changes that can be handled via toggling of css classes should be handled that way!
- **Throttle/Debounce:** UI components are responsible for throttling or debouncing user inputs.
- **Z-Index:** There are 3 z-index ranges:
    - **Fixed-Below (0-100):** z-index `0` to `100`
        - These are statically assigned to UI components that are immovable and must appear beneath movable components.
    - **Fixed-Above (0-100):** z-index `Number.MAX_SAFE_INTEGER - 100` to `Number.MAX_SAFE_INTEGER`
        - These are statically assigned to UI components that are immovable and must appear above movable components.
    - **Dynamic:** z-index `101` to `Number.MAX_SAFE_INTEGER - 101`
        - These are dynamically assigned to moveable UI components that must be focusable.

---

## UI Components - Overview
### ControlsUI (left sidebar)
Vertically collapsible container with individual subsections.
**z-index:** Fixed-Below 1

**Subsections**
- **Simulation Options:**  
    - gravitational constant: number
    - theta: number (min=0, max=1)
    - epsilon: number (min=0.0001)
- **Visualization Options:**  
    - orbit paths: checkbox
    - velocity trails: checkbox
    - labels: checkbox
    - body scale: range (min=0.01, max=1)
- **Presets:** => Button to open PresetUI
- **Hotkeys** static reference only


### TimeControls (bottom center)
Simple container, always visible.
- **play/pause**: button
- **Simulation Time**: number (readonly)
- **Time Step**: number


### BodyListUI (right sidebar)
Vertically collapsible container with an interactive list of all celestial bodies.
**z-index:** Fixed-Below 2

Each item on the list displays the body's name and has the following buttons:
- **Focus:** focuses the camera on this body; highlighted if active
- **Configure:** opens the body's `BodyConfigUI` component or brings it to the front if already open
- **Details:** opens the body's `BodyDetailsUI` component or brings it to the front if already open
- **Delete:** handles body deletion with confirmation dialog 
Additionally, the `BodiesListUI` component handles
- **Sort:** sort bodies by: name (default), mass; + maybe other reasonably static properties
- **Search:** simple text box to search bodies by name
- **Create** button: creates a new body and opens its `BodyConfigUI` component


### BodyConfigUI
Moveable & collapsible overlay that provides detailed configuration interface for a single celestial body.
**z-index:** Dynamic

- Handles validated input for body property modifications
- Manages form validation and error display


### BodyDetailsUI
Moveable & collapsible overlay that displays detailed read-only information of a single celestial body.
**z-index:** Dynamic


### PresetUI
Dialog to import and export preset strings.
**z-index:** Fixed-Above 0

Simulation is paused while this dialog is open.
- **Default Preset Selection:** List of labeled buttons for default presets to choose from (initially 1, more can be added later).
- **Import Section:**
    - Text input element for importing preset strings which performs initial cleaning and validation.
    - **Import** button which properly cleans the input string, validates it, and loads it into the engine.
- **Export Section:**
    - **Generate Preset** button to generate a preset string from the current simulation.
    - Readonly text input element for the generated preset string with a "copy to clipboard" button


### PixiHandler
The central PIXI.js component for all canvas rendering.
**z-index:** Fixed-Below 0

- Drives the render loop and updates the visual representation of the system.
- Manages camera logic (zoom, pan, focus tracking)
- All other components must be above the canvas. The size of the canvas is must always be the full screen.


### NotificationsUI
A dedicated container area for displaying temporary, non-modal status messages to the user (e.g., "Preset Loaded").
**z-index:** Fixed-Above 1

- Manages a message queue to gracefully handle bursts of notification requests.
- Automatically manages the lifecycle of notification DOM elements, adding them to the view and removing them after either a configured timeout, a user click on the notification's 'close' button, or a `clear()` API call.
- Acts independently from the Pixi render loop and is instead tied to the browser's `requestAnimationFrame` directly.
- Implements a performance-optimized render loop using debouncing and intelligent scheduling to minimize processing and DOM manipulation.

**API**
- Provides a simple API for other parts of the system to display various types of information.
- Notification types are `error`, `warn`, `success`, and `info`.

---

## Utility Classes
`ViewModel` and `ViewModelMovable` base classes encapsulate shared code that the individual components can simply extend.

### `ViewModel`
Base class to encapsulate shared code.
- Owns and manages the lifecycle of:
    - `#element` A HTMLDivElement, nested in which are all HTMLElements the ViewModel manages.
    - `#body` A HTMLDivElement and direct child of `#element` that contains all content that should be hidden when the ViewModel is collapsed.
    - `#header` A HTMLDivElement and direct child of `#element` that contains all content that shouldn't be hidden when the ViewModel is collapsed.
        - `#header` elements: title, collapse button, close button
- `toggleCollapse()` method to manage its own collapse state, hiding `#body` when collapsed
- `destroy()` method to manage the cleanup of its own state and the removal of managed DOM elements


### `ViewModelMovable` extends `BaseViewModel`
For BodyConfig and BodyDetails components
- Static z-index tracking
    - minimum of 101
    - `getTopZ()` static method that returns the current value of the private tracker and increments it by 1. Safeguard against overflow isn't needed; you'd have to write a script that does nothing but continuously bring windows to the front for this to become an issue
- `#element`
    - event listener for any click to bring it to the front
- `#header`
    - event listener for click-and-drag to update `#element`'s position

---

## Render Loop - Overview
The application uses a render-loop execution model similar to ones used in video games, especially older titles.
- **Pixi Controlled:** The render loop is ultimately controlled by Pixi via callbacks from Pixi's `Ticker` plugin. These callbacks are methods on central `RenderLoop` orchestrator and are registered during initialization. 
- **Max FPS:** Pixi settings lock the maximum frame rate to 60 frames per second.
- **FPS-locked Timesteps:** While unpaused, the simulation advances by 1 timestep every frame, locking the rate of execution to the FPS. This could be decoupled relatively easily later if needed by inserting a controller in between Pixi and the render-phase callbacks. 
- **Render-Phases:** The render loop is split into three distinct phases:
    - **Pre-Render:** Handles physics calculations and data transformations
    - **Render:** Selectively updates UI components
    - **Post-Render:** Resolves deferred promises (see API section). The thread is then unblocked, allowing user inputs to be processed until the next Pre-Render.

### Pre-Render Phase
1. `RenderLoop.#preRender()` is called by the Pixi `Ticker` callback with `UPDATE_PRIORITY.HIGH`. The thread is blocked from now until the end of the next Post-Render Phase.
2. (C# `PhysicsEngine`) Physics timestep calculations.
3. (C# `Bridge`) Writes physics state into shared memory.
4. (JS `Bridge`) Reads physics state from shared memory, parses & creates diff => refreshes `Physics.state` and `Physics.diff`.
5. (`AppData`) Uses `Physics.diff` to keep non-physics data store in sync => refreshes `AppData.state` and `AppData.diff`.
6. (`DataViews`) Uses `Physics.diff.bodies` and `AppData.diff.bodies` to keep the store of `DataViewBody` objects in sync with actual bodies in the simulation. It also prepares `BodyFrameData` and `SimFrameData` transient data objects containing the data for bodies and simulation properties that were modified since the last frame.
`RenderLoop.#preRender()` returns to Pixi `Ticker`

### Render Phase
7. `RenderLoop.#render()` is called by the Pixi `Ticker` callback with `UPDATE_PRIORITY.NORMAL`.
8. (`UiManager`) Uses `BodyFrameData` and `SimFrameData` to selectively create, delete, and update individual UI components as required, passing the `DataViewBody` and/or `DataViewSim` objects as required.
9. `RenderLoop.#render()` returns to Pixi `Ticker`
10. Pixi handles the canvas rendering

### Post-Render Phase
11. `RenderLoop.#postRender()` is called by the `PixiHandler` wrapper around Pixi's `render()` method, after Pixi has fully rendered the canvas.
12. (`DeferredResolver`) Resolves any promises returned by API calls that were executed since the last frame's pre-render phase.
13. `RenderLoop.#postRender()` returns and eventually unblocks the thread.

---

## API
Methods on `App.api` serve as the sole entry point for user actions from UI components. This ensures a clear, one way command and data flow of `User Action => UI Component => App.api => State Changes => Render Phases => Visual Result`.
This API is not exposed externally and is only added to the `globalThis` in DEBUG builds for testing purposes.


### Notes about PhysicsEngine
- **Runtime:** The `PhysicsEngine` runs locally in the WASM runtime of the client's browser. It is entirely synchronous and shares a thread with the JS runtime.
- **Black Box:** The `PhysicsEngine` should be viewed as an agnostic library and treated like a black box.
- **Error Propagation:** Any Exceptions raised by the `PhysicsEngine` are marshalled to JS Errors.
- **Batching:** The `PhysicsEngine` processes each call independently and sequentially. Batched calls are not supported.


### API Model: Immediate Execution, Deferred Resolution
Implemented via `DeferredResolver`.

API calls are executed immediately and any errors thrown are also propagated right away.
If the immediate execution is successful, each method of `App.api` returns a Promise that resolves in the post-render phase in step #12.
Certain actions that completely reset the entire system state such as `App.api.loadPreset()` also reject all prior Promises that were returned on the same frame.

- **Phase-Timing:** API calls made during or after `DeferredResolver.resolve()` has been called in step #12 in the post-render phase of frame `n`, will be executed immediately but will resolve only during step #12 in the post-render phase of frame `n + 1`.

**Pros:**
- Immediate validation and error propagation.
    ```ts
        // Assume a maximum body limit in the PhysicsEngine.
        const bodyId1: number = await App.api.createBody();   // Creates body in frame n but reaches body limit. Resolves in post-render phase of frame n.
        const bodyId2: number = await App.api.createBody();   // Throws immediately after resolving the previous Promise, still in frame n
    ```
- Frame desync between action and state is made implict.
    ```ts
        const bodyId: number = await App.api.createBody();  // Creates the body in the PhysicsEngine right away. The Promise then resolves to the body's id at the end of the next render.
        const bodyState: BodyPhysicsState = Physics.state.bodies.get(bodyId); // Would work fine as the awaited Promise resolves after the body has been written to shared memory. 
    ```
- Clear separation of responsibilities. The 

**Cons:**
- Architectural complexity higher than Direct Action model, but lower than Async Command Queue model.
- Errors can happen in two ways: Immediate, synchronous throw or Promise rejection (for system-wide state invalidation). Calling code is responsible for handling both. To make this easier for calling code, promises are rejected with a `DeferredResolverResetError`.

---

# Preset Loading

`App.loadPreset()`
1. `RenderLoop.togglePaused(true)` => Pause time advancement
1. `Bridge.loadPreset()` => Overwrites physicsState
2. `AppData.loadPreset()` => Overwrites appState
    - `#nextFrameDiff.sim` is overwritten right away
    - `#nextFrameDiff.updatedBodies` is cleared, then populated with **ONLY** the ids of bodies that exist in both `#state.bodies` and presetData. This is because created and deleted bodies are handled by the `syncDiff` call during the next preRender phase.
3. `Pixi.render()` => to immediately render this new frame.

In the preRender phase:
- `Bridge.tick(false)` => Calculates derived data without advancing time (because `RenderLoop.togglePaused(true)` was called before) and updates `Bridge.state` and `Bridge.diff`
    - **IMPORTANT**: Bodies with existing ID are counted as updated, not created!!!
- `AppData.syncDiff(Bridge.diff.bodies.created, Bridge.diff.bodies.deleted)` => 
    - 'created' **MUST NOT OVERWRITE** existing data which has already been imported during `AppData.loadPreset()`

Rest works as normal.

---



















-------------------------------------------------------------------



# Previous Notes, Ideas, Designs, and Considerations



-------------------------------------------------------------------


## Step 2 - Render Model
Codify data & command flow for components as detailed in **Model C: Render-Synced**. The entries for the other considered models remain included for future reference.  

### Model A: Event Bus
- **Action:**
    - Actions are operations initiated by a UI component.
    - Are handled within the UI component, if possible (for example: drag operations)
    - For actions that cannot be handled internally, the component calls the `App` interface as a "fire and forget" (for example: changing a body's mass)
- **Reaction:** 
    - Reactions are defined as operations that the component must react to.
    - Are handled via events emitted by the `App`.
    - **Important:** Reactions must be handled entirely within the component! This is to avoid accidental loops or other event soup. 

**Flowchart**
Action => App => Execution => App => Reaction

**Example:** User clicks on the "Create" button of the `BodiesList` component to create a new body.
1. **Action:** `BodiesList` calls `App.createBody()`. It does not await the result or expect any return.
2. **App:** `App.createBody()` delegates the operation to `Bridge`
3. **Execution:** `Bridge` queues the creation of the body, which is processed at the start of the next `Tick()` or after a short timeout, whichever comes first (so that commands are still processed when the simulation is paused). Once the `Engine` has processed the command and `Bridge` has marshalled the new state back across the WASM <=> JS boundary, `Bridge` calls a callback function on `App` to inform it of the new state. For this, `Bridge` also passes a `DiffData` object that consists of keys of any changed simulation properties (such as simulationTime) as well as three Sets of body ids - one for created, one for deleted, and one for updated bodies. The reason for only passing keys and ids is to preserve a single source of truth for all simulation state which all downstream consumers read from directly (the `PhysicsState` getter). 
4. **App:** `App` then iterates over the `DiffData`, integrates any additions or deletions into its own `App.StateView` object (which combines the pure simulation from the `Bridge.PhysicsState` with additional front-end exclusive metadata, such as names of bodies), and emits events based on the new state, including a "bodyCreated" event (or something along those lines) which passes the id of the newly created body.
5. **Reaction:** Components that listen to the "bodyCreated" event are then responsible for handling the rest themselves. In this example:
    - `BodiesList.onCreateBody(id)` Looks up the state of the new body via its ID in the `App.StateView` and creates a list entry for it in its own DOM representation.
    - `BodyConfig.staticOnCreateBody(id)` Creates a new `BodyConfig` instance, looking up the body's state via its ID in the `App.StateView` to do so.
    - `CanvasView.onCreateBody(id)` Looks up the state of the new body via its ID in the `App.StateView` and prepares the data PIXI needs to render the body on the next render frame.

**Pros:**
- Less complex `App` as it only needs to emit events rather than orchestrate components.
- Better decoupling as `App` doesn't need to know which components to call.
- `App` doesn't need to own individual components directly as those can own and manage their own instances.

**Cons:**
- Less efficient as the creation of an `App.StateView` is necessary to provide components with a unified representation of state. Each component must also handle its own lookups.
- Higher systemic complexity as the order in which events are processed is harder to trace and conceptualize.
- Events not causing other events isn't architecturally enforced.

---

### Model B: Direct Orchestration
In this model, the first three steps (Action => App => Execution) are identical to those of model A. But rather than emitting events for components to react to in step 4, the `App` in this model orchestrates UI components directly.  

**Flowchart**
Action => App => Execution => App => Components

**Example:** User clicks on the "Create" button of the `BodiesList` component to create a new body.
1. **Action (same as A):** `BodiesList` calls `App.createBody()`. It does not await the result or expect any return.
2. **App (same as A):** `App.createBody()` delegates the operation to `Bridge`
3. **Execution (same as A):** `Bridge` queues the creation of the body, which is processed at the start of the next `Tick()` or after a short timeout, whichever comes first (so that commands are still processed when the simulation is paused). Once the `Engine` has processed the command and `Bridge` has marshalled the new state back across the WASM <=> JS boundary, `Bridge` calls a callback function on `App` to inform it of the new state. For this, `Bridge` also passes a `DiffData` object that consists of keys of any changed simulation properties (such as simulationTime) as well as three Sets of body ids - one for created, one for deleted, and one for updated bodies. The reason for only passing keys and ids is to preserve a single source of truth for all simulation state which all downstream consumers read from directly (the `PhysicsState` getter). 
4. **App:** `App` then iterates over the `DiffData` and directly manages and orchestrates the components as necessary, passing data only as needed (combining simulation-level and front-end-exclusive data as needed on the fly).
5. **Components:** Offer methods for `App` to call. In this example:
    - `BodiesList.onCreateBody(bodyData)` Creates a list entry for the provided body's data in its own DOM representation.
    - `BodyConfig.staticOnCreateBody(bodyData)` Creates a new `BodyConfig` instance.
    - `CanvasView.onCreateBody(bodyData)` Prepares the data PIXI needs to render the body on the next render frame.

**Pros:**
- High degree of control over the order of operations.
- High traceability, making it easy to debug.
- Efficient, as the creation of a central `StateView` isn't necessary; simulation and metadata are passed only as required.

**Cons:**
- Complex `App` as it must manage not only incoming calls but also orchestrate all components.

---

### Model C: Render-Synced
The idea behind this model is to clearly distinguish between three distinct phases that happen sequentially.

**Compute Phase:** Begins when the PIXI ticker callback with `UPDATE_PRIORITY.HIGH` is called.

1. `CanvasView` Call the `onRenderFrameReady_HIGH` callback function that was injected by `App` during `CanvasView`'s initialization.
2. `App` Calls `Bridge.tickEngine()`, where `advanceTime` is `false` if the simulation is paused, or `true` otherwise.
3. `Bridge` Process command queue.
4. `Engine` Execute a simulation step (with timestep=0 if the `advanceTime` argument was `false`; this must be done to ensure a coherent state as, for example, a newly added body must have its acceleration derived before that can be displayed).
5. `Bridge` Write the simulation state into shared memory (on the WASM side).
6. `Bridge` Read and process the shared memory into the `PhysicsState` object (on the JS side), constructing/updating a `DiffData` object in the process. This object consists of keys of any changed simulation properties (such as simulationTime) as well as three Sets of body ids - one for created, one for deleted, and one for updated bodies. The reason for only passing keys and ids is to preserve a single source of truth for all simulation state which downstream consumers read from directly (the `PhysicsState` getter).
7. `Bridge` Resolve queued commands.

9. `App` Process its own command queue (for commands or parts of commands that aren't related to the physics of the simulation; for example: changing a body's name)
10. `App` Updates its `StateView` object to reflect the new state of the `Bridge.PhysicsState` and any the front-end exclusive metadata. During this process it also updates its own `StateDiff` object, which adds any front-end exclusive metadata that has changed to the `Bridge`'s `DiffData`. From this point forward, the data of the entire system remains unchanged and in sync until the start of the next compute phase! 
11. `App` The call stack then resolves to `CanvasView` and back to PIXI.

**Render Phase:** Begins when the PIXI ticker callback with `UPDATE_PRIORITY.MEDIUM` is called.
1. `CanvasView` Call the `onRenderFrameReady_MEDIUM` callback function that was injected by `App` during `CanvasView`'s initialization.
2. `App` Orchestrates the UI components to render or update, passing `StateView` and `StateDiff`. This can be done either directly or via events as the order in which the components are updated or rendered doesn't matter. Each UI component selectively updates the DOM elements only for properties that have actually changed.
3. `App` The call stack then resolves to `CanvasView` and back to PIXI, which handles the rendering of the `<canvas>` that is managed by `CanvasView`.

**Command Phase:** Begins when the compute phase ends, and ends when the compute phase begins. Due to the single-threaded nature of the JavaScript runtime, the code executed in the compute phase is blocking, which means the command phase does not need to be enforced separately.
- All commands are handled through the `App.Command` API and their execution is deferred until the next compute phase. 
- For clarity and convenience, all `App.Command` API methods must return a promise that resolves at the end of the next render phase, once the entire system - both state and visual representation - are in sync again (see notes below). 

**Notes:**
 - **Error Handling:** Any uncaught error encountered during the compute or render phase up should cancel the following render phase, pause the simulation, reject any pending promises from `App.Command`, and display a user facing `Notification`.
- **Handler Classes:** To ensure a cleaner separation of concerns, the three phases should be handled by different handler classes that are owned by `App`.
    - `ComputeHandler` Manages compute-phase interaction with the `Bridge` & updates `StateView`.
    - `RenderHandler` Manages the collection of UI components and orchestrates their updates during the render phase.
    - `CommandHandler` Provides the API and manages the `App`'s command queue and the resolution of promises.
- **PIXI & requestAnimationFrame:** Since PIXI's render loop is ultimately triggered by the browser's `requestAnimationFrame`, nothing that is handled in the render phase should be wrapped in `requestAnimationFrame`. UI component code that is unrelated to PIXI's render loop, like `ViewModelMovable`'s `onPointerMove` event handler for dragging, can be safely wrapped in `requestAnimationFrame` as needed.
- **Render Performance:** The render phase can be tuned to increase performance by setting a limit of how often each component can be updated. This could be handled by the `App` itself or by a counter within each component. This way the `SimulationControls` component could be set to update only every 10 calls, while the `CanvasView` renders on every call (PIXI handles a lot of the render optimization there already).
- **PIXI & Ticker `render()` call:** To resolve the promises returned by `App.Command` API methods at the end of the next render phase, it must be known when PIXI actually finishes rendering the scene. Using the standard `PIXI.Ticker` plugin, the `render()` method is not called manually and there is no event emitted by PIXI to signify that rendering is finished either. While PIXI's `SystemRunner` could be used to listen to the internal `postrender` event, the setup is complex and requires setting a custom `Renderer`. For this simple task, it's easier to simply monkeypatch the `render()` function in a wrapper that simply calls the original `render()` and then emits a `renderFinished` event.   
See example:  
```js
    // Create a new application
    const app = new Application();
    
    // Initialize the application
    await app.init();

    // Monkeypatch render function.
    // This must be done AFTER the Promise from app.init() has resolved!
    const initialRenderFunc = app.renderer.render;
    app.renderer.render = (...args) => {
        // Ensure correct `this` binding
        const ret = initialRenderFunc.call(app.renderer, ...args);
        // Execute any post-render code.
        console.log("Render process complete.");
        // Renderer.render() returns void by default 
        // but this doesn't hurt.
        return ret;
    }

    // Other initialization...

    // After initialization: Add ticker callback
    app.ticker.add(() => { /* ... */ });
```

**Pros:**
- Command and data flow is strictly enforced by the architecture itself, with no way to circumvent it.
- Easy to understand and visualize both data and commands as the architecture itself is basically a flowchart. 
- Complexity of `App` can be limited through clear and distinct handler classes.
- Deferred execution of commands offers a clean, consistent, and intuitive API
- Choice between an event-based rendering approach or direct orchestration is reduced to a matter of preference, as all the actual work has already been done in the compute phase. This limits the considerations for the choices to:
    - **Events:** Decoupled components and reduced caller complexity
    - **Orchestration:** Fine-controlled render order; (debugging isn't really any easier as all the components do is read from a readonly state; The deferred execution model prevents accidental event soup)

**Cons:**
- Deferred execution of commands and the requirement for commands to return promises that resolve when the rendering is fully finished introduces extra complexity.

**Implementation Considerations:**
- **Command Queue** 
    - **Problem:** The C# side of the `Bridge` already employs a Task-based command queue where Tasks are marshalled back into JS Promises that resolve when the C# side finished writing into the shared memory. If the `App` needs to implement its own command queue for metadata-related tasks anyways, should the `Bridge`'s command queue be removed? Or should `App` simply use its own command queue for metadata-related tasks and pass along all physics-related tasks to the `Bridge`, letting it handle its own commands? The `App` would have to create separate promises to return to callers, though that isn't an issue as the `App`'s promises always resolve after the `Bridge`'s.
    - **Decision:** Remove the `Bridge` queue and let `App` orchestrate everything.

---

## Async Command & Data Flow

### Scenario A: Bridge has its own command queue

**Command Phase**
1. `App.createBody()` enqueues a command in the CommandQueue.
    Enqueued command:
    ```ts
        async () => {
            const id = await Bridge.createBody();
            this.#appDataStore.createBodyData(id);
            return id;
        }
    ```
2. `CommandQueue.enqueue()` adds the command to its queue to be executed when `CommandQueue.process()` is called and returns a promise that resolves when `CommandQueue.resolveProcessed()` is called.
3. `App.createBody()` returns the Promise returned by `CommandQueue.enqueue()`.

**Compute Phase**
1. `App.onPixiRenderFrame_HIGH()` is called by pixi, starting the compute phase
2. `App.onPixiRenderFrame_HIGH()` calls `CommandQueue.process()`
3. `CommandQueue.process()` calls the queued command as an anonymous function
4. `anonymous function` calls `Bridge.createBody()`
5. `Bridge.createBody()` is called and forwards the call to the C# `EngineBridge.CreateBody()` method via the dotnet WASM handler.
6. (C#) `EngineBridge.CreateBody()` enqueues the command in the C# CommandQueue.
    Enqueued command:
    ```c#
        var id = engine.CreateBody();
        return id;
    ```
7. (C#) `CommandQueue.EnqueueTask()` adds the Task to its queue to be executed when `CommandQueue.ProcessAll()` is called, and returns a Task that will be completed when `CommandQueue.ResolveProcessed()` is called.
8. (C#) `EngineBridge.CreateBody()` returns the Task returned by `CommandQueue.EnqueueTask()`, which is marshalled across the JS <-> WASM boundary into a JS Promise.
9. `Bridge.createBody()` returns the Promise for the Task returned by `EngineBridge.CreateBody()`
10. `anonymous function` receives the Promise and awaits its resolution
11. `CommandQueue.process()` adds the Promise returned by the `anonymous function` and adds it to the 'value' in its processing queue.

**PROBLEM:** The anonymous function isn't awaited, thus the 'value' of the command in the processing queue is a Promise


### Scenario B: Bridge does NOT have its own command queue

**Command Phase**
1. `App.createBody()` enqueues a command in the CommandQueue. Enqueued command:
    ```ts
        () => {
            const id = Bridge.createBody();
            this.#appDataStore.createBodyData(id);
            return id;
        }
    ```
    2. `CommandQueue.enqueue()` adds the command to its queue to be executed when `CommandQueue.process()` is called and returns a promise that resolves when `CommandQueue.resolveProcessed()` is called.
3. `App.createBody()` returns the Promise returned by `CommandQueue.enqueue()`.

**Pre-Render Phase**
`App.onPixiRenderFrame_HIGH()` is called by pixi, starting the compute phase.

1. `App.onPixiRenderFrame_HIGH()` calls `CommandQueue.process()`
    2. `CommandQueue.process()` calls the queued command as an anonymous function
        3. `anonymous function` calls `Bridge.createBody()`
            4. `Bridge.createBody()` is called and forwards the call to the C# `EngineBridge.CreateBody()` method via the dotnet WASM handler.
                5. (C#) `EngineBridge.CreateBody()` forwards the call to the (C#) `PhysicsEngine`, which creates the body in its own memory and returns its ID.
                    6. (C#) `PhysicsEngine` creates a body with default values and returns its ID.
                7. (C#) `EngineBridge.CreateBody()` returns the ID returned by (C#) `PhysicsEngine`
            8. `Bridge.createBody()` returns the ID
        9. `anonymous function` receives the ID and calls `AppDataStore.createBodyData()`, passing the ID
            10. `AppDataStore.createBodyData()` creates a BodyData entry for the ID in its own `AppDataStore.appData` and adds the ID to its own `AppDataStore.diff.bodies.created`
        11. `anonymous function` returns the ID
    12. `CommandQueue.process()` assigns the ID returned by the `anonymous function` to the 'value' property of the Command and sets the 'success' property of the Command as `true`
13. `App.onPixiRenderFrame_HIGH()` calls `Bridge.tickEngine()`, passing a boolean 'isPaused'
    14. `Bridge.tickEngine()` forwards the call to (C#) `EngineBridge.Tick()`, passing 'isPaused'
        15. (C#) `EngineBridge.Tick()` calls either (C#) `PhysicsEngine.Tick()` if 'isPaused' is `false`, or (C#) `PhysicsEngine.SyncOnly()` if 'isPaused' is `true`
            16. A: (C#) `PhysicsEngine.Tick()` forwards the 'simulationTime' by 1 'timeStep', doing the calculations for each body in the process.  
                - **Note:** The newly created body is disabled by default, so it is not included physics calculations yet. This is to allow safe configuration of its values. Once a the body is enabled, it irreversibly alters the simulation.
            16. B: (C#) `PhysicsEngine.SyncState()` recalculates the derived state of the simulation at the current 'simulationTime', without advancing it.  
                - **Note:** This is important to ensure that modifying the simulation while it's paused (time not advancing), accurately reflects the modifications on the front-end.
        17. (C#) `EngineBridge.Tick()` calls (C#) `MemoryBufferHandler.WriteViewToMemory()`, passing the live `PhysicsEngine.View`
            18. (C#) `MemoryBufferHandler.WriteViewToMemory()` writes the data from `PhysicsEngine.View` into the shared memory buffers.
        19. (C#) `EngineBridge.Tick()` returns `void` to `Bridge.tickEngine()`
    20. `Bridge.tickEngine()` calls `StateManager.refresh()`
        21. `StateManager.refresh()` calls the injected (C#) `EngineBridge.GetPointerData()`
            22. (C#) `EngineBridge.GetPointerData()` returns the pointers from (C#) `MemoryBufferHandler`
        23. `StateManager.refresh()` calls `SimStateReader.refresh()`, passing the pointers to the fixed-size memory buffer
            24. `SimStateReader.refresh()` gets the view over the shared memory buffer, reads the data using the self-configuring layout, writes the new data into `SimStateReader.state`, and updates `SimStateReader.diff`
        25. `StateManager.refresh()` calls `BodyStateReader.refresh()`, passing the pointers to the resizable memory buffer
            26. `BodyStateReader.refresh()` gets the view over the shared memory buffer, reads the data for each body using the self-configuring layout, writes the new data into `BodyStateReader.state`, and updates `BodyStateReader.diff`
        27. `StateManager` exposes 
            - `StateManager.state`, consisting of `SimStateReader.state` and `BodyStateReader.state`
            - `StateManager.diff`, consisting of `SimStateReader.diff` and `BodyStateReader.diff`
    28. `Bridge` exposes
        - `StateManager.state` as `Bridge.state`
        - `StateManager.diff` as `Bridge.diff`


**Render Phase**
`App.onPixiRenderFrame_MEDIUM()` is called by pixi, starting the compute phase.

1. `App.onPixiRenderFrame_MEDIUM()` calls `UiManager.render()`
    2. `UiManager.render()` accesses injected `Bridge.state`, `Bridge.diff`, `AppDataStore.appData`, and `AppDataStore.diff`
    3. `UiManager.render()` calls `UiManager.#renderBodies()`
        4. `UiManager.#renderBodies()` iterates over `Bridge.diff.bodies` and `AppDataStore.diff.bodies`, calling `UiManager.#onDeletedBody()` for each deleted body.
            5. `UiManager.#onDeletedBody()` for each body that was deleted, passing the deleted body's ID 
                6. Deletes the `DataViewBody` object from the `UiManger.#bodyViews` Map
                7. Removes UI components tied to the lifetime of the body with that ID
                8. Updates UI components that must know about the body's deletion but aren't tied to its lifetime
        9. `UiManager.#renderBodies()` iterates over `Bridge.state.bodies` and calls:
            10. `UiManager.#onCreatedBody()` for each body that was created, passing the created body's ID
                11. Creates a `DataViewBody` object for the body in the `UiManger.#bodyViews` Map. This `DataViewBody` object simply holds references to the body's entries in `Bridge.state.bodies` and `AppDataStore.appData.bodies`.
                12. Creates UI components tied to the lifetime of the body, passing the `DataViewBody` object
                13. Updates UI components that must know about the body's creation but aren't tied to its lifetime, passing the `DataViewBody` object
            14. `UiManager.#onUpdatedBody()` for each body that was updated (not created), passing the updated body's ID
                15. Gets the `DataViewBody` object of the body from the `UiManger.#bodyViews` Map
                16. Updates UI components that must know about the body being updated, passing the `DataViewBody` object
    17. `UiManager.render()` calls `UiManager.renderSim()`
        18. `UiManager.renderSim()` gets the `DataViewSim` object from `UiManager.#simView`, or creates it if none exists yet. This `DataViewSim` object simply holds references to `Bridge.state.sim` and `AppDataStore.appData.sim`.
        19. `UiManager.renderSim()` uses the keys in `Bridge.diff.sim` and `AppDataStore.diff.sim` to update the UI components that must be updated when said keys change, passing the `DataViewSim` object
    20. `UiManager.render()` calls `PixiHandler.prepareRenderFrame()`, which uses the injected `Bridge.state`, `Bridge.diff`, `AppDataStore.appData`, and `AppDataStore.diff` to prepare the pixi scene data for rendering
21. `App.onPixiRenderFrame_MEDIUM()` returns to pixi, which handles the actual rendering of the canvas


**Post-Render Phase**
`App.onPixiRenderFrame_FINISHED()` is called by a wrapper around pixi's `render()` method, which signals that pixi has fully finished rendering the canvas. At this point the frame data and its visual representation (UI components and Pixi canvas) are back in sync.

**This also marks the beginning of the Command Phase for the following render frame.**

1. `App.onPixiRenderFrame_FINISHED()` calls `CommandQueue.resolveProcessed()`
    2. `CommandQueue.resolveProcessed()` resolves the Promise it returned to `App.createBody()` to the ID it assigned to the 'value' property of the Command in step #12 of the Pre-Render Phase.

Thus the promise that the caller of `App.createBody()` received resolves only after the entire application state (physics state, appData, and UI) are back in sync.


### Notes
- **Queue Swapping:** The `CommandQueue` uses a double-buffer queue system with a `#nextFrameQueue` and a `#processingQueue`. When `CommandQueue.process()` is called, the buffers swap. This ensures that any Commands queued during or after the `process()` method call are queued for the **following** frame, not the one that's currently being processed. This way, race conditions are prevented, even if, for example, a UI component were to call `App.updateBody()` during it's own render-phase update (which the UI component shouldn't do anyways!).




### Alternate Version

**Command Phase**
1. `App.createBody()` enqueues a command in the CommandQueue. Enqueued command:
    ```ts
        () => {
            const id = Bridge.createBody();
            return id;
        }
    ```
    2. `CommandQueue.enqueue()` adds the command to its queue to be executed when `CommandQueue.process()` is called and returns a promise that resolves when `CommandQueue.resolveProcessed()` is called.
3. `App.createBody()` returns the Promise returned by `CommandQueue.enqueue()`.

**Pre-Render Phase**
`App.onPixiRenderFrame_HIGH()` is called by pixi, starting the pre-render phase.

1. `App.onPixiRenderFrame_HIGH()` calls `CommandQueue.process()`
    2. `CommandQueue.process()` calls the queued command as an anonymous function
        3. `anonymous function` calls `Bridge.createBody()`
            4. `Bridge.createBody()` is called and forwards the call to the C# `EngineBridge.CreateBody()` method via the dotnet WASM handler.
                5. (C#) `EngineBridge.CreateBody()` forwards the call to the (C#) `PhysicsEngine`, which creates the body in its own memory and returns its ID.
                    6. (C#) `PhysicsEngine` creates a body with default values and returns its ID.
                7. (C#) `EngineBridge.CreateBody()` returns the ID returned by (C#) `PhysicsEngine`
            8. `Bridge.createBody()` returns the ID
        9. `anonymous function` returns the ID
    10. `CommandQueue.process()` assigns the ID returned by the `anonymous function` to the 'value' property of the Command and sets the 'success' property of the Command as `true`
11. `App.onPixiRenderFrame_HIGH()` calls `Bridge.tickEngine()`, passing a boolean 'isPaused'
    12. `Bridge.tickEngine()` forwards the call to (C#) `EngineBridge.Tick()`, passing 'isPaused'
        13. (C#) `EngineBridge.Tick()` calls either (C#) `PhysicsEngine.Tick()` if 'isPaused' is `false`, or (C#) `PhysicsEngine.SyncOnly()` if 'isPaused' is `true`
            14. A: (C#) `PhysicsEngine.Tick()` forwards the 'simulationTime' by 1 'timeStep', doing the calculations for each body in the process.  
                - **Note:** The newly created body is disabled by default, so it is not included physics calculations yet. This is to allow safe configuration of its values. Once a the body is enabled, it irreversibly alters the simulation.
            14. B: (C#) `PhysicsEngine.SyncState()` recalculates the derived state of the simulation at the current 'simulationTime', without advancing it.  
                - **Note:** This is important to ensure that modifying the simulation while it's paused (time not advancing), accurately reflects the modifications on the front-end.
        15. (C#) `EngineBridge.Tick()` calls (C#) `MemoryBufferHandler.WriteViewToMemory()`, passing the live `PhysicsEngine.View`
            16. (C#) `MemoryBufferHandler.WriteViewToMemory()` writes the data from `PhysicsEngine.View` into the shared memory buffers.
        17. (C#) `EngineBridge.Tick()` returns `void` to `Bridge.tickEngine()`
    18. `Bridge.tickEngine()` calls `StateManager.refresh()`
        19. `StateManager.refresh()` calls the injected (C#) `EngineBridge.GetPointerData()`
            20. (C#) `EngineBridge.GetPointerData()` returns the pointers from (C#) `MemoryBufferHandler`
        21. `StateManager.refresh()` calls `SimStateReader.refresh()`, passing the pointers to the fixed-size memory buffer
            22. `SimStateReader.refresh()` gets the view over the shared memory buffer, reads the data using the self-configuring layout, writes the new data into `SimStateReader.state`, and updates `SimStateReader.diff`
        23. `StateManager.refresh()` calls `BodyStateReader.refresh()`, passing the pointers to the resizable memory buffer
            24. `BodyStateReader.refresh()` gets the view over the shared memory buffer, reads the data for each body using the self-configuring layout, writes the new data into `BodyStateReader.state`, and updates `BodyStateReader.diff`
        25. `StateManager` exposes 
            - `StateManager.state`, consisting of `SimStateReader.state` and `BodyStateReader.state`
            - `StateManager.diff`, consisting of `SimStateReader.diff` and `BodyStateReader.diff`
    26. `Bridge` exposes
        - `StateManager.state` as `Bridge.state`
        - `StateManager.diff` as `Bridge.diff`
27. `App.onPixiRenderFrame_HIGH()` calls `AppDataManager.syncBodies()`, passing `Bridge.diff.bodies.deleted` and `Bridge.diff.bodies.created`
    28. `AppDataManager.syncBodies()` iterates over  `Bridge.diff.bodies.created` and calls `AppDataManager.createBodyData()` for each body that was created.
        29. `AppDataManager.createBodyData()` creates a `BodyData` entry for the ID of the created body in its `AppDataStore.appData.bodies`
    30. `AppDataManager.syncBodies()` iterates over  `Bridge.diff.bodies.deleted` and calls `AppDataManager.deleteBodyData()` for each body that was deleted.
        31. `AppDataManager.deleteBodyData()` deletes the entry in `AppDataStore.appData.bodies` for the ID of the deleted body

**Render Phase**
`App.onPixiRenderFrame_MEDIUM()` is called by pixi, starting the render phase.

1. `App.onPixiRenderFrame_MEDIUM()` calls `UiManager.render()`
    2. `UiManager.render()` accesses injected `Bridge.state`, `Bridge.diff`, `AppDataStore.appData`, and `AppDataStore.diff`
    3. `UiManager.render()` calls `UiManager.#renderBodies()`
        4. `UiManager.#renderBodies()` iterates over `Bridge.diff.bodies.deleted`, calling `UiManager.#onDeletedBody()` for each deleted body.
            5. `UiManager.#onDeletedBody()` for each body that was deleted, passing the deleted body's ID 
                6. Deletes the `DataViewBody` object from the `UiManger.#bodyViews` Map
                7. Removes UI components tied to the lifetime of the body with that ID
                8. Updates UI components that must know about the body's deletion but aren't tied to its lifetime
        9. `UiManager.#renderBodies()` iterates over `Bridge.state.bodies` and calls:
            10. `UiManager.#onCreatedBody()` for each body that was created, passing the created body's ID
                11. Creates a `DataViewBody` object for the body in the `UiManger.#bodyViews` Map. This `DataViewBody` object simply holds references to the body's entries in `Bridge.state.bodies` and `AppDataStore.appData.bodies`.
                12. Creates UI components tied to the lifetime of the body, passing the `DataViewBody` object
                13. Updates UI components that must know about the body's creation but aren't tied to its lifetime, passing the `DataViewBody` object
            14. `UiManager.#onUpdatedBody()` for each body that was updated, passing the updated body's ID. A body was updated if its ID is either in `Bridge.diff.bodies.updated` or in `AppDataStore.diff.bodies.updated`.
                15. Gets the `DataViewBody` object of the body from the `UiManger.#bodyViews` Map
                16. Updates UI components that must know about the body being updated, passing the `DataViewBody` object
    17. `UiManager.render()` calls `UiManager.renderSim()`
        18. `UiManager.renderSim()` gets the `DataViewSim` object from `UiManager.#simView`, or creates it if none exists yet. This `DataViewSim` object simply holds references to `Bridge.state.sim` and `AppDataStore.appData.sim`.
        19. `UiManager.renderSim()` uses the keys in `Bridge.diff.sim` and `AppDataStore.diff.sim` to update the UI components that must be updated when said keys change, passing the `DataViewSim` object
    20. `UiManager.render()` calls `PixiHandler.prepareRenderFrame()`, which uses the injected `Bridge.state`, `Bridge.diff`, `AppDataStore.appData`, and `AppDataStore.diff` to prepare the pixi scene data for rendering
21. `App.onPixiRenderFrame_MEDIUM()` returns to pixi, which handles the actual rendering of the canvas

**Post-Render Phase**
`App.onPixiRenderFrame_FINISHED()` is called by a wrapper around pixi's `render()` method, which signals that pixi has fully finished rendering the canvas. At this point the frame data and its visual representation (UI components and Pixi canvas) are back in sync.

**This also marks the beginning of the Command Phase for the following render frame.**

1. `App.onPixiRenderFrame_FINISHED()` calls `CommandQueue.resolveProcessed()`
    2. `CommandQueue.resolveProcessed()` resolves the Promise it returned to `App.createBody()` to the ID it assigned to the 'value' property of the Command in step #12 of the Pre-Render Phase.

Thus the promise that the caller of `App.createBody()` received resolves only after the entire application state (physics state, appData, and UI) are back in sync.


## Notes
- **Queue Swapping:** The `CommandQueue` uses a double-buffer queue system with a `#nextFrameQueue` and a `#processingQueue`. When `CommandQueue.process()` is called, the buffers swap. This ensures that any Commands queued during or after the `process()` method call are queued for the **following** frame, not the one that's currently being processed. This way, race conditions are prevented, even if, for example, a UI component were to call `App.updateBody()` during it's own render-phase update (which the UI component shouldn't do anyways!).

---

## Execution Flow
How to best fit API calls into the constraints enforced by the render frame loop?
This API is private and only used in UI components (and exposed to the `globalThis` for DEBUG builds).

### Render Frame Loop

**Loop Start**
1. (`App`) `onRenderFrameReady` callback (capped at 60 fps) starts the render frame. Thread is blocked from here.

**Pre-Render Phase**
2. (C# `PhysicsEngine`) Physics timestep calculations.
3. (C# `Bridge`) Write physics state into shared memory.
4. (JS `Bridge`) Read physics state from shared memory, parse & create diff => refreshes `Physics.state` and `Physics.diff`.
5. (`AppData`) Use `Physics.diff` to keep non-physics data store in sync => refreshes `AppData.state` and `AppData.diff`.
6. (`UiData`) Use `Physics.diff.bodies` and `AppData.diff.bodies` to keep the store of `DataViewBody` objects in sync with actual bodies in the simulation.

**Render Phase**
7. (`UiHandler`) Iterate through joint `Physics.diff` and `AppData.diff` to selectively update UI components, passing `DataViewBody` and `DataViewSim` (created once on startup) objects.

**Loop End**
- Thread unblocks, allowing for API calls until the start of the next loop.

---

### Notes about PhysicsEngine
- **Runtime:** The `PhysicsEngine` runs locally in the WASM runtime of the client's browser. It is entirely synchronous and shares a thread with the JS runtime.
- **Black Box:** The `PhysicsEngine` is an agnostic library that is developed independantly and must thus be treated like a black box.
- **Error Propagation:** Any Exceptions raised by the `PhysicsEngine` are marshalled to JS Errors.
- **Batching:** The `PhysicsEngine` processes each call independently and sequentially. Batched calls are not supported.

---

### API - Model A: Direct Action
API calls happen synchronously and are processed immediately but are not visualized until the next frame.

**Pros:**
- Simple architecture.
- Immediate validation and error propagation. If the maximum body limit is reached, the function to add another body would throw immediately.
    ```ts
        const bodyId1: number = App.api.createBody();   // Succeeds but reaches body limit
        const bodyId2: number = App.api.createBody();   // Throws immediately
    ```

**Cons:**
- Frame desync between action and state. If a user action calls `App.api.createBody()`, the body is created in the C# `PhysicsEngine` but the JS side does not see it in `Physics.state` until the next frame. The following code example shows code that looks like it should work but doesn't:
    ```ts
        const bodyId: number = App.api.createBody();  // Creates the body in the PhysicsEngine immediately and returns its id.
        const bodyState: BodyPhysicsState = Physics.state.bodies.get(bodyId);     // Would be undefined as body has not been written to shared memory yet.
    ```

---

### API - Model B: Async Command Queue
API calls are pushed to a command queue, which is processed once at the start (between #1 and #2) of every frame.  
Each method of `App.api` returns a Promise that resolves/rejects after the render phase (after #7).

**Pros:**
- Frame desync between action and state is made clearer.
    ```ts
        const bodyId: number = await App.api.createBody();  // Creates the body in the PhysicsEngine at the start of the next frame. Promise then resolves to the body's id at the end of the render.
        const bodyState: BodyPhysicsState = Physics.state.bodies.get(bodyId); // Would work fine as the awaited Promise resolves after the body has been written to shared memory. 
    ```

**Cons:**
- Higher architectural complexity - necessitates a CommandQueue.
- Validation and error propagation aren't immediate. If the maximum body limit is reached, the function to add another body would return a Promise that rejects only at the end of the next render phase.
    ```ts
        const bodyId1: number = await App.api.createBody();   // Resolves after rendering frame n but reaches body limit
        const bodyId2: number = await App.api.createBody();   // Rejects after rendering frame n + 1
    ```

**Considerations:**
- **Immediate Validation:** Why not do pre-processing of API calls before queuing them to throw errors immediately? While this would work for extremely simple or fundamental aspects (i.e. clamping Barnes-Hut Theta between 0 and 1), even the simple example of the body limit wouldn't work as the `PhysicsEngine` does not expose a `MAX_BODIES` value, much less more complex calls. Since `PhysicsEngine` must be treated like a black box, the only way to actually validate most API calls is for the `PhysicsEngine` to execute them.

---

### API - Model C: Immediate Execution, Deferred Resolution
API calls are executed immediately and any errors thrown are also propagated right away.
If the immediate execution is successful however, each method of `App.api` returns a Promise that resolves after the render phase (after #7).
Certain actions that completely reset the entire system state such as `App.api.loadPreset()` also rejects all prior Promises that were returned on the same frame.

**Pros:**
- Immediate validation and error propagation. If the maximum body limit is reached, the function to add another body would throw immediately.
    ```ts
        const bodyId1: number = await App.api.createBody();   // Resolves after rendering frame n but reaches body limit
        const bodyId2: number = await App.api.createBody();   // Throws immediately after resolving the previous Promise, still in frame n
    ```
- Frame desync between action and state is made clearer.
    ```ts
        const bodyId: number = await App.api.createBody();  // Creates the body in the PhysicsEngine right away. The Promise then resolves to the body's id at the end of the next render.
        const bodyState: BodyPhysicsState = Physics.state.bodies.get(bodyId); // Would work fine as the awaited Promise resolves after the body has been written to shared memory. 
    ```

**Cons:**
- Architectural complexity higher than Direct Action model, but lower than Async Command Queue model.
- Errors can happen in two ways: synchronous throw or Promise rejection (for system-wide state invalidation). Calling code is responsible for handling both.

---

### API - Model D: Fire & Forget
API calls are executed immediately and any errors thrown are also propagated right away.
All API methods return `void`.

**Pros:**
- By far the simplest model.
- Implicitly reenforces the "UI action => API call => UI Update" cycle as UI actions cannot use the return to inform any further actions beyond a simple try/catch.

**Cons:**
- Extremely limiting.
- Makes E2E testing more difficult as Playwright would have to interact with the UI components via the DOM instead of simply accessing the console.

---

## Other Considered Models
- **Macro Command:** This model would make it appear like a series of calls to the `PhysicsEngine` could be batched into a single, atomic call when that is not possible.
- **Optimistic Updates:** Physics data cannot be mocked by the front-end and UI updates only once per frame anyways.

---


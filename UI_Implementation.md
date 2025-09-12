# UI Implementation Plan

## Constraints & Considerations
- Vanilla JS & CSS
- No fontend libraries like react or svelte
- No ARIA attributes

# UI Components - Overview
### SimulationControls (left sidebar)
Vertically collapsible container with individual subsections
- **Simulation Options:**  
    - play/pause: button
    - time step: number
    - gravitational constant: number
    - theta: number (min=0, max=1)
    - epsilon: number (min=0.0001)
- **Visualization Options:**  
    - orbit paths: checkbox
    - velocity trails: checkbox
    - labels: checkbox
    - body scale: range (min=0.01, max=1)
- **Presets:**  
    - import: button
    - export: button
- **Hotkeys** static reference only

**Implementation Details:**  
- z-index = 1

### BodiesList (right sidebar)
Vertically collapsible container with an interactive list of all celestial bodies.
Each item on the list displays the body's name and has the following buttons:
- **Focus:** focuses the camera on this body; highlighted if active
- **Configure:** opens the body's `BodyConfig` component or brings it to the front if already open
- **Details:** opens the body's `BodyDetails` component or brings it to the front if already open
- **Delete:** handles body deletion with confirmation dialog 
Additionally, the `BodiesList` component handles
- **Sort:** sort bodies by: name (default), mass; + maybe other reasonably static properties
- **Search:** simple text box to search bodies by name
- **Create** button: creates a new body and opens its `BodyConfig` component

**Implementation Details:**
- z-index = 2

### BodyConfig
Moveable & collapsible overlay that provides detailed configuration interface for a single celestial body
- Handles validated input for body property modifications
- Manages form validation and error display

**Implementation Details:**
- z-index = 10+

### BodyDetails
Moveable & collapsible overlay that displays detailed read-only information of a single celestial body

**Implementation Details:**
- z-index = 10+

### CanvasView (DONE)
The central PIXI.js component for all rendering.
- Drives the render loop and updates the visual representation of the system
- Manages camera logic (zoom, pan, focus tracking)
- All other components must be above the canvas. The size of the canvas is must always be the full screen.

**Implementation Details:**
- z-index = 0

### Notifications (DONE)
A dedicated container area for displaying temporary, non-modal status messages to the user (e.g., "Preset Loaded").
- Provides a simple `add(message)` API for other components to display information.
- Manages a message queue to gracefully handle bursts of notification requests.
- Automatically manages the lifecycle of notification DOM elements, adding them to the view and removing them after a configured duration.
- Implements a performance-optimized render loop using debouncing and intelligent scheduling to minimize processing and DOM manipulation.

**Implementation Details:**
- z-index = `Number.MAX_SAFE_INTEGER / 2`

---

# General
- **CSS:** All styles changes that can be handled via toggling of css classes should be handled that way!

---

# Step 1 (DONE)
Create `ViewModel` or `ViewModelMovable` base classes for shared code that the individual components (SimulationControls, BodiesList, BodyConfig, and BodyDetails) can simply extend.

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
    - minimum of 10
    - `getTopZ()` static method that returns the current value of the private tracker and increments it by 1. Safeguard against overflow isn't needed; you'd have to write a script that does nothing but continuously bring windows to the front for this to become an issue
- `#element`
    - event listener for any click to bring it to the front
- `#header`
    - event listener for click-and-drag to update `#element`'s position

---

# Step 2
Codify data & command flow for components as detailed in **Model C: Render-Synced**. The entries for the other considered models remain included for future reference.  

## Considered Models

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
3. **Execution:** `Bridge` queues the creation of the body, which is processed at the start of the next `Tick()` or after a short timeout, whichever comes first (so that commands are still processed when the simulation is paused). Once the `Engine` has processed the command and `Bridge` has marshalled the new state back across the WASM <=> JS boundary, `Bridge` calls a callback function on `App` to inform it of the new state. For this, `Bridge` also passes a `DiffData` object that consists of keys of any changed simulation properties (such as simulationTime) as well as three Sets of body ids - one for created, one for deleted, and one for updated bodies. The reason for only passing keys and ids is to preserve a single source of truth for all simulation state which all downstream consumers read from directly (the `SimState` getter). 
4. **App:** `App` then iterates over the `DiffData`, integrates any additions or deletions into its own `App.StateView` object (which combines the pure simulation from the `Bridge.SimState` with additional front-end exclusive metadata, such as names of bodies), and emits events based on the new state, including a "bodyCreated" event (or something along those lines) which passes the id of the newly created body.
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
3. **Execution (same as A):** `Bridge` queues the creation of the body, which is processed at the start of the next `Tick()` or after a short timeout, whichever comes first (so that commands are still processed when the simulation is paused). Once the `Engine` has processed the command and `Bridge` has marshalled the new state back across the WASM <=> JS boundary, `Bridge` calls a callback function on `App` to inform it of the new state. For this, `Bridge` also passes a `DiffData` object that consists of keys of any changed simulation properties (such as simulationTime) as well as three Sets of body ids - one for created, one for deleted, and one for updated bodies. The reason for only passing keys and ids is to preserve a single source of truth for all simulation state which all downstream consumers read from directly (the `SimState` getter). 
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

## Chosen Model

### Model C: Render-Synced
The idea behind this model is to clearly distinguish between three distinct phases that happen sequentially.

**Compute Phase:** Begins when the PIXI ticker callback with `UPDATE_PRIORITY.HIGH` is called.

1. `CanvasView` Call the `onRenderFrameReady_HIGH` callback function that was injected by `App` during `CanvasView`'s initialization.
2. `App` Calls `Bridge.tickEngine(advanceTime)`, where `advanceTime` is `false` if the simulation is paused, or `true` otherwise.
3. `Bridge` Process command queue.
4. `Engine` Execute a simulation step (with timestep=0 if the `advanceTime` argument was `false`; this must be done to ensure a coherent state as, for example, a newly added body must have its acceleration derived before that can be displayed).
5. `Bridge` Write the simulation state into shared memory (on the WASM side).
6. `Bridge` Read and process the shared memory into the `SimState` object (on the JS side), constructing the `DiffData` in the process.
7. `Bridge` Resolve queued commands.
8. `Bridge` Call the `onTickCallback` function that was injected by `App` during `Bridge`'s initialization, passing `DiffData`.
9. `App` Process its own command queue (for commands or parts of commands that aren't related to the physics of the simulation; for example: changing a body's name)
10. `App` Updates its `StateView` object to reflect the new state of the `Bridge.SimState` and any the front-end exclusive metadata. During this process it also updates its own `StateDiff` object, which adds any front-end exclusive metadata that has changed to the `Bridge`'s `DiffData`. From this point forward, the data of the entire system remains unchanged and in sync until the start of the next compute phase! 
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
- **PIXI & Ticker `render()` call:** To resolve the promises returned by `App.Command` API methods at the end of the next render phase, it must be known when PIXI actually finishes rendering the scene. Using the standard `Ticker` plugin, the `render()` method is not called manually and there is no event emitted by PIXI to signify that rendering is finished either. While PIXI's `SystemRunner` could be used to listen to the internal `postrender` event, the setup is complex and requires setting a custom `Renderer`. For this simple task, it's easier to simply monkeypatch the `render()` function in a wrapper that simply calls the original `render()` and then emits a `renderFinished` event.   
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
- **Command Queue:** The C# side of the `Bridge` already employs a Task-based command queue where Tasks are marshalled back into JS Promises that resolve when the C# side finished writing into the shared memory. If the `App` needs to implement its own command queue for metadata-related tasks anyways, should the `Bridge`'s command queue be removed? Or should `App` simply use its own command queue for metadata-related tasks and pass along all physics-related tasks to the `Bridge`, letting it handle its own commands? The `App` would have to create separate promises to return to callers, though that isn't an issue as the `App`'s promises always resolve after the `Bridge`'s.
    - **Decision:** Keep two separate command queues. That way the clean separation of concerns between physics and presentation is maintained. 

---

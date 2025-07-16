# Overview
**Orbital Mini-Sandbox** is a 2D, top-down simulation of simplified orbital mechanics that runs entirely in a web browser using a C#-based physics engine compiled to WebAssembly. The application is designed to function as an interactive "what-if" engine for celestial systems, allowing users to visually explore the basic principles of gravity. Users can create and modify these systems, for example by adding a new planet to a model of our solar system or by doubling the Earth's mass, and immediately observe the resulting impact on the orbits and stability of the entire system.

# Revision History

- **16/07/2025**
    - Simplified and optimized Tick workflow by eliminating the need for double buffering.
- **09/07/2025**
    - Added `QuadTreeNode.cs` and `AABB.cs` quad tree components to `Physics` structure.
    - Redefined `AppShell` as an event-based, reactive, unidirectional orchestrator and preset handler.
    - Refactored `CanvasView` into a fully decoupled, dumb rendering component.
    - Refactored `AppDataManager` into a decoupled data manager with queued updates.
- **03/07/2025**
    - Removed replaced `Physics.Tick` timestamp argument in favour of accepting a deltaTime argument.
- **02/07/2025**
    - Decided to allow WASM to marshal C# exceptions to JS.
    - Redesigned `Bridge` to use an Task/Promise-based command queue for select WebApp -> Physics write operations.
    - Updated the PDD to reflect these and other minor revisions and errors.  
- **25/06/2025**
    - Added automated integration testing via Playwright.
    - Updated the PDD to reflect revision.
- **21/06/2025**
    - Defined `main.mjs` as the lightweight application entry point, responsible for initialization and global fatal error handling.
    - Added a self-contained `Notifications` class for displaying transient UI messages.
    - Updated the PDD to reflect revisions.
- **17/06/2025**
    - Extracted definitions for shared memory layout into a separate file `LayoutRecords.cs`.
    - Redefined `WebApp` architecture, introducing an `AppDataManager` class for application exclusive data.
    - Detailed the Bridge's project structure, JS API surface, and tick/error flow.
    - Added missing `timestamp` argument to `Tick()`.
    - Updated the PDD to reflect revisions.
- **16/06/2025** 
    - Standardized `Bridge` shared memory layout. 
    - Defined data contracts via a C# record for SSOT.
- **14/06/2025** 
    - Replaced `System.Numerics.Vector2` (float-based) with a custom `Vector2D` (double-based) in the `Physics` for improved precision.
- **13/06/2025** 
    - Refined shared memory architecture and dynamic memory allocation.
    - Standardized shared buffer fields to `Float64`.
    - Decided to pass the `BodyStateBuffer` pointer as part of `SimStateBuffer` for better performance and convenience
    - Moved `bridge.mjs` into the `Bridge` project.
    - Simplified memory buffer layout communication to string arrays. 
- **12/06/2025** 
    - Added a JavaScript API facade `bridge.mjs` to abstract all direct C# interop calls from the `WebApp`.
- **11/06/2025** 
    - Removed Blazor WebAssembly framework in favor of a pure .NET WASM library for the `Bridge` to eliminate unnecessary overhead.

# Technical Requirements & Constraints
**Core Technology Stack**
- C# physics engine with WebAssembly bridge
- PIXI.js for simulation rendering
- HTML/CSS/JavaScript foundation
- Pure client-side simulation with no server dependencies
- Single-page application architecture
- Static hosting compatible (GitHub Pages deployment)

**Performance Targets**
- Support for minimum 100 simultaneous celestial bodies
- Smooth real-time simulation with variable time scaling
- Optimized for modern browser performance

**Browser Support**
- Modern browsers only (Chrome, Firefox, Safari, Edge - current versions)

**Dependencies**
- PIXI.js
- Minimal other external dependencies preferred 
- Self-contained codebase without external APIs

---

# Core Features & Functionality

**Visualization Engine**
- Minimalist design with customizable mono-colored background (default: black)
- Celestial bodies rendered as simple circles sized by mass (default: white)
- Toggleable orbit paths (thin grey lines, default: on)
- Toggleable velocity trails (thick grey lines, length based on velocity, default: on)
- Toggleable body labels (default: off)

**Camera System**
- Zoom in/out functionality
- Focus tracking on selected celestial body (default: most massive body)
- System-wide view focusing on center of gravity
- Smooth camera transitions

**Simulation Controls**
- Play/pause toggle
- Variable time scaling with speed controls
- Set simulation time (forward/backward navigation)
- Adjustable gravitational constant (G)
- Undo/redo for all modifications

**Celestial Body Management**
- Collapsible, interactive body list with add/remove functionality
- Per-body configuration (name, color, position, velocity, mass)
- Real-time info display for computed properties
- Body-specific controls (focus, delete with confirmation)

**Preset System**
- Default simplified solar system
- Import/export functionality using deterministic string format
- Point-mass physics model (mass, position, velocity)

**Visual Customization**
- Adjustable body scale multiplier
- Customizable background color
- Toggle controls for all visual elements

---

# User Interface Design

**Layout Architecture**
- Collapsible left sidebar containing:
    - Visual configuration controls
    - Simulation settings
    - Preset import/export
    - Hotkey reference
- Collapsible right sidebar for celestial body management list
- Moveable overlay panels for body configuration and info display
- Prominent "PAUSED" indicator when simulation is stopped

**Information Hierarchy**
- All interface elements are collapsible/closable for unobstructed viewing
- Clean canvas prioritizes simulation visualization
- Sidebars expanded by default for discoverability

**User Experience Flow**
1. Application loads with default solar system preset running
2. Both sidebars initially expanded to showcase available controls
3. Users can immediately observe the simulation or explore interface elements
4. Intuitive discovery-based interaction model

**Device Support**
- Optimized for desktop/laptop displays (landscape orientation)
- Minimum resolution warning for displays below 1280×720
- Touch and mouse input support
- Not optimized for mobile devices due to performance constraints

**Accessibility Considerations**
- Visual-first application design
- Dual input support (mouse and touch)
- Clear visual hierarchy and readable interface elements

---

# Technical Architecture

## WebApp Architecture

The WebApp is a single-page application built with HTML, CSS, and JavaScript, using PIXI.js for rendering. It provides the user interface and orchestrates the simulation by communicating with the JavaScript API of `Bridge`.

### Design Principles
- **Separated State Ownership**: The architecture enforces a strict separation of concerns for state management:
    - The C# `Physics` is the exclusive source of truth for all physics-related data (mass, position, velocity, simulation time, etc).
    - The JavaScript `WebApp` is the exclusive source of truth for all application-level metadata (body names, colors, UI configuration).
- **Orchestration, Not Duplication**: The `WebApp` does not duplicate the core physics state. It orchestrates the simulation by issuing commands to the `Bridge` and accesses physics data for rendering.
- **History Management**: The `WebApp` owns and manages the undo/redo history. It accomplishes this by fetching full-state presets from the engine, managing them in its own history stacks, and loading them back into the engine as needed.

### UI Components
**SimulationControls (left sidebar)**
Vertically collapsible container with horizontally collapsible individual subsections
- **Simulation Options:** play/pause, time scaling, time navigation, gravitational constant adjustment
- **Visualization Options:** visual rendering options (orbit paths, velocity trails, labels, body scale), and background color customization
- **Presets:** import/export UI, input validation & error handling
- **Hotkey** static reference

**BodiesList (right sidebar)**
Vertically collapsible container with an interactive list of all celestial bodies.
Each item on the list displays the body's name and has the following buttons:
- **Focus:** focuses the camera on this body; highlighted if active
- **Configure:** opens the body's `BodyConfig` component or brings it to the front if already open
- **Details:** opens the body's `BodyDetails` component or brings it to the front if already open
- **Delete:** handles body deletion with confirmation dialog 
Additionally, the `BodiesList` component handles
- list sorting and search
- **Create** button: creates a new (default disabled) body and opens its `BodyConfig` component

**BodyConfig**
Moveable & collapsible overlay that provides detailed configuration interface for a single celestial body
- Handles validated input for body property modifications
- Manages form validation and error display

**BodyDetails**
Moveable & collapsible overlay that displays detailed read-only information of a single celestial body

**Canvas**
The central PIXI.js component for all rendering.
- Drives the render loop and updates the visual representation of the system
- Manages camera logic (zoom, pan, focus tracking)
- always below all other components

**Notifications**
A dedicated container area for displaying temporary, non-modal status messages to the user (e.g., "Preset Loaded").
- Provides a simple `add(message)` API for other components to display information.
- Manages a message queue to gracefully handle bursts of notification requests.
- Automatically manages the lifecycle of notification DOM elements, adding them to the view and removing them after a configured duration.
- Implements a performance-optimized render loop using debouncing and intelligent scheduling to minimize processing and DOM manipulation.


### Core Components

`main.mjs`
- Application entry point & handler of fatal errors.
- **Responsibilities:**
    - Serves as the sole script entry point, loaded directly by `index.html`.
    - Initiates the application by calling `AppShell.initialize()` on window load.
    - Acts as a global safety net, catching unhandled exceptions and promise rejections.
    - Displays a static fatal error message to the user if the application encounters an unrecoverable state, preventing a blank or broken page.

`AppShell`
- Static orchestrator for the entire application.
- **Responsibilities:**
    - Instantiates and/or holds references to all core components (Javascript API of `Bridge`, `AppDataManager`, `CanvasView`, `Notifications`, all other UI components).
    - Manages the creation, destruction, and layering of UI overlays (`BodyConfig`, `BodyDetails`).
    - Binds global keyboard shortcuts and delegates corresponding actions.
    - Orchestrates the command and data flow between `Bridge` and all other components.
    - **Snapshot Management:**
        - Calls the `getPreset()` method of the `Bridge` to retrieve the current physics state which it then combines with `AppDataManager`'s preset data to form a complete snapshot string.
        - To restore a state, it parses the snapshot string, loads the physics portion into the engine via `engineBridge.loadPreset()`, and the metadata portion via `appDataManager.loadPresetData()`.

`AppDataManager`
- Owner and handler of application level metadata. Dumb and fully decoupled. 
- **Responsibilities:**
    - **Data Handling:** 
        - Maintains a `Map<number, object>` which maintains application-level metadata for each celestial body in the simulation.
        - Handles updates to metadata as instructed by `AppShell`, queue-based updates to existing bodies.
- **State Ownership:**
    - **metaData**: An object containing application-level metadata
    - **updateQueue**

`CanvasView`
- Encapsulates all rendering logic using PIXI.js. Dumb and fully decoupled. 
- **Responsibilities:**
    - Initializes the `PIXI.Application` and attaches it to the DOM.
    - Signals `AppShell` whenever an animation frame is ready.
    - Renders the simulation by efficiently updating the properties of PIXI.js display objects from injected data as instructed.
    - Manages all camera logic (zoom, pan, focus tracking).
- **State Ownership:**
    - The `PIXI.Application` instance.
    - Current camera state.

---

## Bridge Architecture

The `Bridge` serves as the critical communication layer between the JavaScript `WebApp` and the C# `Physics`, leveraging WebAssembly's interop capabilities. It manages the `Physics` lifecycle, coordinates data flow through shared memory, and provides a stable JavaScript API surface.

### Design Principles
- **Single Source of Truth**: The `Bridge` maintains the authoritative `Physics` instance and delegates all physics operations to it
- **Performance-Optimized Communication**: High-frequency data (physics state) flows through shared WASM memory buffers, while low-frequency operations (commands) use direct interop calls
- **Stateless Facade**: The bridge itself maintains minimal state, serving primarily as a coordinator and translator between the `WebApp` and `Physics` domains
- **Error Boundary**: All C# exceptions are caught and handled as errors on the JavaScript side

### Project Structure
```
Bridge/
├── EngineBridge.cs                 # C# coordinator
├── MemoryBufferHandler.cs          # Shared Memory manager
├── LayoutRecords.cs                # SSOT for shared memory buffer layout
├── CommandQueue.cs                 # Manager of Task-based command queue
├── wwwroot/
│   ├── bridge.js                   # JS coordinator
└── types/
    ├── Bridge.d.ts                 # Type information for the JS Bridge class
    ├── LayoutRecords.d.ts          # Auto-generated type information for the simulation properties
    └── dotnet.d.ts                 # .NET type information for the DotNetHostBuilder
```

### Shared Memory Architecture
The `EngineBridge` implements a double/float64 dual-buffer system for efficient data transfer:
- **SimStateBuffer**: General simulation data and buffer metadata
- **BodyStateBuffer**: Continuous array of body data blocks

The single source of truth for the layout of these buffers are the records in `LayoutRecords.cs`. This ensures a self-configuring layout on both the C# and the JavaScript sides.

**Memory Management Rules**
- `Bridge` has exclusive write access to shared buffers
- `WebApp` has read-only access to shared buffers
- Buffers are updated synchronously after each `Physics.Tick()`
- Buffer sizes are pre-allocated based on maximum body count (configurable, default: 10 bodies)

### API Surface
The `Bridge` exposes a Javascript API (`bridge.mjs`) to the WebApp. The API's type definitions can be found in `types/Bridge.d.ts`
```typescript
static initialize(): Promise<void>

static tickEngine(timestamp: number): BodyDiffData;

static setTimeScale(timeScale: number): void;
static setTimeDirection(isForward: bool): void;

// Preset Management  
static getPreset(): string;
static loadPreset(enginePreset: string): BodyDiffData;

// Body Management
static createBody(Partial<BodyStateData>): Promise<number>;
static updateBody(Partial<BodyStateData>): Promise<boolean>;
static deleteBody(id: number): Promise<boolean>;

BodyDiffData: {
    created: Set<number>,
    updated: Set<number>,
    deleted: Set<number>,
}
```

### Components

`Bridge.mjs`
- **Static** coordinator that serves as the sole external-facing API surface
- **Responsibilities:**
    - Abstracts away and hides the implementation details of the C# <-> JavaScript boundry
    - Provides a clean surface for `WebApp` to interact with
    - Efficiently reads from the shared memory buffers and provides primitive differential update data
- **State Ownership:**
    - `simState`: The Javascript representation of the current simulation state

`EngineBridge.cs`
- **Static** coordinator that serves as the sole JavaScript interop surface
- **Responsibilities:**
    - Manages the lifecycle of the `PhysicsEngine` instance
    - Translates JavaScript method calls into appropriate `Physics` API calls
    - Coordinates the `MemoryBufferHandler` to update shared memory after physics operations
- **State Ownership:**
    - `PhysicsEngine` instance (composition)
    - `MemoryBufferHandler` instance (composition)
    - `CommandQueue` instance (composition)

`MemoryBufferHandler.cs`
- Specialized component responsible for efficient shared memory management
- **Responsibilities:**
    - Allocates and manages the dual shared memory buffer system
    - Synchronizes physics engine state into shared buffers after each tick
    - Maintains buffer metadata (sizes, pointers, layouts)
    - Avoids buffer overflow scenarios through automatic resizing
- **State Ownership:**
    - `SimStateBuffer`: Fixed-size buffer for simulation metadata
    - `BodyStateBuffer`: Dynamic buffer for celestial body data
    - Buffer configuration parameters (max bodies, buffer sizes)

`CommandQueue.cs`
- Specialized utility class that decouples immediate method calls from their execution by queuing them as commands.
- **Responsibilities:**
    - Enqueues PhysicsEngine operations for deferred, batched execution.
    - Abstracts the creation and lifecycle of Task<T> objects (which become JavaScript Promises) for commands that must return a value.
    - Manages promise resolution, ensuring tasks are correctly completed with a result or rejected with an exception upon command execution.
    - Executes all pending commands when processed by the EngineBridge (during a tick).
    - Allows the queue to be cleared to prevent stale operations after a full state reset (e.g., LoadPreset).
- **State Ownership:**
    - A Queue of Action<PhysicsEngine> delegates representing the pending commands.

**Tick Flow:**
Javascript
1. `WebApp` calls `Bridge.tickEngine()`
2. `Bridge` (JS) calls `EngineBridge.Tick()` (C#)

C#
3. `EngineBridge` calls `PhysicsEngine.Tick()`
4. `PhysicsEngine` completes physics calculations
5. `EngineBridge` calls `MemoryBufferHandler.WriteTickData()`
6. `EngineBridge` (C#) returns null to `Bridge` (JS) to indicate success, or an error string on a failure

Javascript
7. `Bridge` 
    - propagates errors marshalled from C# exceptions
    - reads from memory and refreshes the exposed `simState` object
    - returns an simple diff to `WebApp`

### Error Handling Strategy
**Exception Translation**
- All C# exceptions are propagated to Javascript via WASM native marshalling to JS Error objects.
- Physics validation errors return descriptive error messages
- System exceptions return generic error indicators

**Graceful Degradation**
- Invalid operations return error responses but don't crash the simulation
- Critical tick errors pause the simulation with error display rather than silent failure
- Memory allocation failures trigger cleanup and retry mechanisms

---

## PhysicsEngine Architecture
The physics engine is designed as a double-buffered, completely UI-agnostic, self-contained system that provides a clean API boundary for external integration. This separation ensures the computational physics logic remains independent of presentation concerns and can be thoroughly tested in isolation.

### API
The PhysicsEngine is accessed by initializing the exposed `PhysicsEngine` class. This class exposes the following methods.
```c#
// Presets
public PresetData GetPresetData(); // takes a snapshot of the current state of the Simulation

public TickData LoadPreset(PresetData preset); // takes a preset data DTO and creates a new Simulation instance from it which replaces the current simulation instance;

// Simulation Time
public TickDataDto Tick(double deltaTime); // ticks the simulation once

public void SetSimulationTime(double time); // sets the simulation time 

public void SetTimeScale(double timescale);  // sets the timescale for the simulation

public void SetTimeForward(bool isForward); // sets the direction of time for the simulation

// DTOs
public record BodyStateData(int Id, bool Enabled, double Mass, double PosX, double PosY, double VelX, double VelY); // I might need to add other properties to this record as CelestialBody also has derived properties like certain orbital values for example. I don't yet know which ones though.

public record SimStateData(double SimulationTime, double TimeScale, bool IsTimeForward);

public record TickDataDto(SimStateData SimStateData, BodyStateData[] BodiesStateData);

public record PresetBodyData(int Id, bool Enabled, double Mass, double PosX, double PosY, double VelX, double VelY);

public record PresetSimData(double SimulationTime, double TimeScale, bool IsTimeForward);

public record PresetData(PresetSimData PresetSimData, PresetBodyData[] PresetBodyDataArray);

public record BodyUpdateData(int Id, bool? Enabled, double? Mass, double? PosX, double? PosY, double? VelX, double? VelY);

// Celestial Bodies

public int CreateBody(); // creats a CelestialBody instance and adds it to the simulation; is always initiaized with enabled=false, a unique id, and default values; the unique id is returned.

public bool DeleteBody(int id); // destroys an existing CelestialBody by its id;

public bool UpdateBody(BodyStateData newData); // updates an existing CelestialBody

public BodyStateData? GetBodyData(int id); // gets the data of an existing CelestialBody instance by its id; If none exists retunrs null instead;

public BodyStateData[] GetBodyDataAll(); // gets the data of all existing CelestialBody instances


```

### Project Structure
```
Physics/
├── PhysicsEngine.cs              # Public class - single entry point
├── Core/
│   ├── Simulation.cs             # Main simulation coordinator
│   ├── Timer.cs                  # Time management and simulation speed
|   ├── Calculator.cs             # Physics calculations per body per timestep
│   ├── Grid.cs                   # Spatial partitioning (QuadTree)
|   └── QuadTreeNode.cs           # Utility class for Grid
├── Bodies/
│   └── CelestialBody.cs          # Individual body state and behavior
└── Models/
    ├── Vector2D.cs               # Mathematical primitives
    └── AABB.cs                   # Axis-aligned bounding box for Grid
```
### Classes
`PhysicsEngine`
- Serves as the single facade interface for external systems to interact with the physics engine. This enforces architectural boundaries by preventing direct access to internal physics components.
- Each instance represents an independent physics engine state; consumer is expected to manage different instances if they choose to instantiate multiple at once
- Responsibilities
	- Manages the current simulation instance lifecycle
	- Provides preset loading/exporting functionality
	- Exposes selective methods from internal simulation components
	- Manages locking and unlocking to prevent external updates during calculation
- State Ownership
	- Current simulation instance
	- locked state

`Simulation`
- Coordinates all simulation components and manages the overall simulation state. Acts as the central orchestrator that ensures all physics components work together cohesively.
- Responsibilities
	- Orchestrates physics calculations through component coordination
	- Maintains the collection of celestial bodies
	- Manages the lifecycle of all owned simulation components
- State Ownership
	- `timer` - Timer instance
	- `bodies` - Collection of CelestialBody instances
	- `calculator` - Calculator instance
	- `grid` - Grid instance

`Timer`
- Manages the temporal aspects of the simulation including speed, direction, and execution timing. Calculates the simulation delta time for each calculation step.
- Responsibilities
	- Controls simulation execution speed and time direction
	- Maintains the current simulation time
	- Calculates simulation delta time for each calculation step
- State Ownership
	- Current simulation timestamp
	- Time direction (forward/backward)
	- Simulation speed multiplier

`Calculator`
- Performs the core physics calculations for each simulation timestep. Handles all gravitational force computations and calculates updates for celestial bodies. Optimized for performance as it executes during every simulation tick.
- Responsibilities
	- Calculates gravitational forces between celestial bodies
	- Creates update DTOs for celestial bodies with new velocities and positions based on physics calculations
	- Utilizes spatial partitioning for efficient proximity queries

`Grid`
- Provides spatial partitioning functionality through QuadTree implementation to optimize gravitational force calculations. Reduces computational complexity from O(n²) to more efficient spatial queries.
- Responsibilities
	- Implements QuadTree spatial data structure for celestial body positioning
	- Serves as the foundational spatial framework for all calculations
	- Maintains spatial index of celestial body positions
- State Ownership
	- QuadTree spatial data structure
	- Spatial partitioning configuration parameters

`CelestialBody`
- Encapsulates the complete state and behavior of individual celestial bodies.
- Responsibilities
	- Maintains all physical properties of a celestial body
	- Provides getters for computed properties derived from its own state
- State Ownership
	- int Id (readonly)
	- Vector2D Position (x, y)
	- Vector2D Velocity (x, y)
	- double Mass
	- bool Enabled

### PhysicsEngine.Tick() workflow
Core workflow when Tick(deltaTime) is called on an instance of PhysicsEngine
1. `PhysicsEngine` Enables syncLock (also prevents further PhysicsEngine.Tick() calls)
2. `PhysicsEngine` Calls simulation.Tick(deltaTime)
3. `Simulation` Gets double simTimeDelta = Timer.GetSimTimeDelta(deltaTime)
4. `Timer` Calculates simTimeDelta from deltaTime, simulationTime, timeScale, and timeDirection and returns the result
5. `Simulation` Calls Timer.UpdateSimTime(simTimeDelta) to update the simulation time; This is safe as only simTimeDelta is used for the rest of the tick calculations
6. `Simulation` calls grid.rebuild(enabledBodies)
7. `Simulation` loops over enabled bodies,calls Calculator.EvaluateBody(body, simTimeDelta, Grid.Root) for each; stores the update DTOs for each body in a cache; can be parallelized safely
8. `Calculator` calculates the changes to the given body and returns a DTO with update data
9. `Simulation` Updates the bodies with the update data DTOs
10. `PhysicsEngine` Disables syncLock

---

# Development Phases & Milestones

**Pre-MVP Milestones**
1. **Core Data Pipeline** 
	1. Mock physics engine (static mock data)
	2. Basic EngineBridge implementation
	3. Basic WebApp implementation
2. **Basic Controls**
	1. Preset system implementation
	2. Camera control integration
3. Real-time physics simulation

**Phase 0 (MVP) - Core Simulation**
- Basic physics engine with point-mass gravitational calculations
- Simple circular celestial bodies (size based on mass)
- Mono-colored background and bodies
- Basic zoom and focus tracking on most massive body
- Play/pause simulation controls
- Default solar system preset
- Import/export preset functionality

**Phase 1 - Simulation Controls**
- Variable time scaling and speed controls
- Simulation time navigation (forward/backward)
- Adjustable gravitational constant
- Undo/redo system implementation

**Phase 2 - Body Management**
- Interactive celestial body list interface
- Add/remove body functionality
- Per-body configuration panel
- Real-time property info display
- Body-specific controls (focus, delete)

**Phase 3 - Visual Polish**
- Orbit path visualization
- Velocity trail rendering
- Body labels
- System-wide camera view with center of gravity focus
- Smooth camera transitions
- Visual customization options
- Background color selection

**Testing Approach**
- Unit tests for physics calculations and data management
- Automated integration testing via Playwright
- Additional manual testing for UI components and user interactions

**Timeline:** 40 hours/week development schedule, open-ended duration (learning project; too many known and unknown unknowns to estimate)

---

# Success Criteria & Constraints

**Definition of Done** 
Project completion is achieved when all features outlined in the Core Features & Functionality section are fully implemented and functional.

**Scope Management** 
Development will strictly adhere to features and functionality defined in this PDD. Any additional feature ideas will be deferred until after project completion to prevent scope creep.

---

# Potential Future Improvements
- Auto-save feature using the snapshot system
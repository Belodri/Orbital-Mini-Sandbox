# Overview
**Orbital Mini-Sandbox** is a 2D, top-down simulation of simplified orbital mechanics that runs entirely in a web browser using a C#-based physics engine compiled to WebAssembly. The application is designed to function as an interactive "what-if" engine for celestial systems, allowing users to visually explore the basic principles of gravity. Users can create and modify these systems, for example by adding a new planet to a model of our solar system or by doubling the Earth's mass, and immediately observe the resulting impact on the orbits and stability of the entire system.

# Revision History

- **17/06/2025**
    - Extracted definitions for shared memory layout into a separate file `LayoutRecords.cs`
    - Redefined `WebApp` architecture, introducing an `AppDataManager` class for application exclusive data.
    - Detailed the Bridge's project structure, JS API surface, and tick/error flow.
    - Added missing `timestamp` argument to `Tick()`
    - Updated the PDD to reflect revisions
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
    - Removed Blazor WebAssembly framework in favor of a pure .NET WASM library for the `Bridge` to eliminate unnecessary overhead

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

### Classes

`AppShell`
- **Static** orchestrator for the entire application.
- **Responsibilities:**
    - Instantiates and/or holds references to all core components (Javascript API of `Bridge`, `AppDataManager`, `CanvasView`, all UI components).
    - Manages the creation, destruction, and layering of UI overlays (`BodyConfig`, `BodyDetails`).
    - Binds global keyboard shortcuts and delegates corresponding actions to the `Bridge` or to other parts of the `WebApp`.
    

`AppDataManager`
- Owner and handler of application level metadata.
- **Responsibilities:**
    - **Data Handling:** 
        - Maintains a `Map<number, object>` which maintains application-level metadata for each celestial body in the simulation.
    - **Snapshot Management:**
        - Calls the `getPreset()` method of the `Bridge` to retrieve the current physics state which it then combines with its own application metadata to form a complete snapshot string.
        - To restore a state, it parses the snapshot string, loads the physics portion into the engine via `engineBridge.loadPreset()`, and updates its own application metadata.
- **State Ownership:**
    - **metaData**: An object containing application-level metadata

`CanvasView`
- Encapsulates all rendering logic using PIXI.js.
- **Responsibilities:**
    - Initializes the `PIXI.Application` and attaches it to the DOM.
    - Directly manages the main `requestAnimationFrame` loop.
    - Consumes the structured data from the `AppDataManager` and `Bridge` JS API.
    - Renders the simulation by efficiently updating the properties of PIXI.js display objects based on the provided data.
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
- Buffer sizes are pre-allocated based on maximum body count (configurable, default: 1000 bodies)

### API Surface
The `Bridge` exposes a Javascript API (`bridge.mjs`) to the WebApp. The API's type definitions can be found in `types/Bridge.d.ts`
```typescript
static initialize(): Promise<void>

static tickEngine(timestamp: number): BodyDiffData;

static setTimeScale(timeScale: number): void;
static setTimeDirection(isForward: bool): void;

// Preset Management  
static getPreset(): string;
static loadPreset(enginePreset: string): void;

// Body Management
static createBody(Partial<BodyStateData>): number;
static updateBody(Partial<BodyStateData>): number;
static deleteBody(bodyId: number): void;

BodyDiffData: {
    created: Set<number>,
    updated: Set<number>,
    deleted: Set<number>,
}
```

### Components

`bridge.mjs`
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
    - Implements comprehensive error handling and transforms C# exceptions into JavaScript-consumable error messages
- **State Ownership:**
    - `PhysicsEngine` instance (composition)
    - `MemoryBufferHandler` instance (composition)

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
    - throws an error if an error string was received
    - reads from memory and refreshes the exposed `simState` object
    - returns an simple diff to `WebApp`

### Error Handling Strategy
**Exception Translation**
- All C# exceptions are caught at the `EngineBridge` boundary. Their messages are used to create new JavaScript Error objects, which are then thrown on the JavaScript side, ensuring no raw C# objects leak across the interop boundary.
- Physics validation errors return descriptive error messages
- System exceptions return generic error indicators
- No exceptions propagate to the JavaScript layer

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
public string GetPreset(); // takes a snapshot of the current state of the Simulation and transforms it intro a string

public bool LoadPreset(string preset); // takes a preset string and creates a new Simulation instance from it which replaces the current simulation instance; returns true if successful, or false if not


// Simulation Time
public TickDataDto Tick(double timestamp); // ticks the simulation once

public void SetSimulationTime(double time); // sets the simulation time 

public void SetTimeScale(double timescale);  // sets the timescale for the simulation

public void SetTimeForward(bool isForward); // sets the direction of time for the simulation

// DTOs
public record BodyStateData(int Id, bool Enabled, double Mass, double PosX, double PosY, double VelX, double VelY); // I might need to add other properties to this record as CelestialBody also has derived properties like certain orbital values for example. I don't yet know which ones though.

public record SimStateData(double SimulationTime, double TimeScale, bool IsTimeForward);

public record TickDataDto(SimStateData SimStateData, BodyStateData[] BodiesStateData);

// Celestial Bodies

public int CreateBody(); // creats a CelestialBody instance and adds it to the simulation; is always initiaized with enabled=false, a unique id, and default values; the unique id is returned.

public void DestroyBody(int id); // destroys an existing CelestialBody by its id;

public BodyStateData? GetBodyData(int id); // gets the data of an existing CelestialBody instance by its id; If none exists retunrs null instead;

public BodyStateData[] GetBodyDataAll(); // gets the data of all existing CelestialBody instances

public int? UpdateBody(BodyStateData newData); // updates an existing CelestialBody; the unique id is returned. If the given data was invalid, returns null instead; 
```

### Project Structure
```
Physics/
├── PhysicsEngine.cs              # Public class - single entry point
├── Core/
│   ├── Simulation.cs             # Main simulation coordinator
│   ├── Timer.cs                  # Time management and simulation speed
|   ├── Calculator.cs             # Physics calculations per body per timestep
│   └── Grid.cs                   # Spatial partitioning (QuadTree)
├── Bodies/
│   ├── CelestialBody.cs          # Individual body state and behavior
└── Models/
    └── Vector2D.cs               # Mathematical primitives
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
	- `prevBodies` - Collection of outdated CelestialBody instances for double-buffering
	- `calculator` - Calculator instance; not persistent
	- `grid` - Grid instance

`Timer`
- Manages the temporal aspects of the simulation including speed, direction, and execution timing. Calculates the delta time for each calculation step.
- Responsibilities
	- Controls simulation execution speed and time direction
	- Maintains the current simulation time
	- Calculates delta time for each calculation step
- State Ownership
	- Current simulation timestamp
	- Time direction (forward/backward)
	- Simulation speed multiplier

`Calculator`
- Performs the core physics calculations for each simulation timestep. Handles all gravitational force computations and mutates celestial bodies. Optimized for performance as it executes during every simulation tick.
- Responsibilities
	- Calculates gravitational forces between celestial bodies
	- Mutates celestial bodies updated velocities and positions based on physics calculations
	- Utilizes spatial partitioning for efficient proximity queries
- State Ownership
	- readonly grid
	- readonly deltaTime
	- readonly prevBodies

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
	- number Id (readonly)
	- Vector2D Position (x, y)
	- Vector2D Velocity (x, y)
	- double Mass
	- bool Enabled

### PhysicsEngine.Tick() workflow
Core workflow when Tick(timestamp) is called on an instance of PhysicsEngine
1. `PhysicsEngine` Enables syncLock (also prevents further PhysicsEngine.Tick() calls)
2. `PhysicsEngine` Calls simulation.Tick(timestamp)
3. `Simulation` Gets double deltaTime = timer.GetDeltaTime(timestamp)
4. `Timer` Calculates deltaTime from timestamp, simulationTime, timeScale, and timeDirection and returns the result
5. `Simulation` calls grid.rebuild(bodies)
6. `Simulation` creates a tempBodiesRef variable and assigns it a reference to bodies  
7. `Simulation` sets bodies = prevBodies  
8. `Simulation` sets prevBodies = tempBodiesRef
9. `Simulation` creates calculator = new Calculator(deltaTime, grid, prevBodies); arguments are readonly;
10. `Simulation` loops over bodies, filters out disabled ones, calls calculator.evaluate(body) for each; can be parallelized safely
11. `Calculator` calculates the changes to the given body and mutates it
12. `Simulation` calls timer.updateTime(deltaTime) which updates the simulationTime and thus advances the tick
13. `PhysicsEngine` Disables syncLock

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
- Manual testing for UI components and user interactions

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
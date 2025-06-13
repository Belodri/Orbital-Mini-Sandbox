# Overview
**Orbital Mini-Sandbox** is a 2D, top-down simulation of simplified orbital mechanics that runs entirely in a web browser using Blazor WebAssembly. The application is designed to function as an interactive "what-if" engine for celestial systems, allowing users to visually explore the basic principles of gravity. Users can create and modify these systems, for example by adding a new planet to a model of our solar system or by doubling the Earth's mass, and immediately observe the resulting impact on the orbits and stability of the entire system.

# Technical Requirements & Constraints
**Core Technology Stack**
- C# physics engine with Blazor WebAssembly bridge
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

The WebApp is a single-page application built with HTML, CSS, and JavaScript, using PIXI.js for rendering. It provides the user interface and orchestrates the simulation by communicating with the C# `EngineBridge`.

### Design Principles
- **Separated State Ownership**: The architecture enforces a strict separation of concerns for state management:
    - The C# `PhysicsEngine` is the exclusive source of truth for all physics-related data (mass, position, velocity, simulation time, etc).
    - The JavaScript `WebApp` is the exclusive source of truth for all application-level metadata (body names, colors, UI configuration).
- **Orchestration, Not Duplication**: The `WebApp` does not duplicate the core physics state. It orchestrates the simulation by issuing commands to the `EngineBridge` and reads physics data back for rendering and state management.
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
- Subscribes to high-frequency tick events and updates the visual representation of the system
- Manages camera logic (zoom, pan, focus tracking)
- always below all other components

### Classes

`AppShell`
- **Singleton** orchestrator for the entire application.
- **Responsibilities:**
    - Instantiates and holds references to all core components (`EngineInterface`, `CanvasView`, all UI components).
    - Manages the creation, destruction, and layering of UI overlays (`BodyConfig`, `BodyDetails`).
    - Binds global keyboard shortcuts and delegates corresponding actions to the `EngineInterface`.

`EngineInterface`
- **Singleton** facade that abstracts all communication with the `EngineBridge`. It is the only module in the `WebApp` permitted to call into the C# code.
- **Responsibilities:**
    - **Command Issuing:** Translates user actions from the UI into specific, low-frequency commands sent to the `EngineBridge` (e.g., `updateBody`, `setTimeScale`).
    - **History Management:**
        - Implements the `undo()` and `redo()` methods.
        - Before issuing a state-changing command, it first calls `engineBridge.getPreset()` to retrieve the current physics state. It combines this with its own application metadata to form a complete snapshot string.
        - This snapshot is pushed onto an `undoStack`. The `undo()`/`redo()` methods manage these stacks and use them to restore a previous state.
        - To restore a state, it parses the snapshot string, loads the physics portion into the engine via `engineBridge.loadPreset()`, updates its own application metadata, and then emits a global event to notify all UI components of the change.
    - **Rendering Loop & Data Transformation:**
        - Manages the `requestAnimationFrame` loop.
        - In each frame, it calls `engineBridge.tick()`. After the tick, it reads the raw physics data from a private typed array view over the WASM shared memory.
        - It transforms this raw data into a structured, user-friendly `engineData` object, which it exposes to the rest of the app. This process maps the flat buffer data into a nested object structure and can include calculated derived values (e.g., acceleration).
        - Finally, it emits a `tick` event, providing the updated `engineData` to all subscribers.
    - **Data Exposure:** Provides clean, read-only access to the application's complete state via two structured properties:
        - `engineData`: A nested object containing all physics-related data for the current tick. This is rebuilt every frame.
        - `metaData`: A map containing all non-physics UI metadata for each body. This persists across ticks.
- **State Ownership:**
    - `undoStack`: `string[]`
    - `redoStack`: `string[]`
    - `metaData`: `Map<[bodyId: number], {name: string, color: string, ...}>`
    - `engineData`: `{simulationTime: number, bodiesData: Map<[bodyId: number], {...}>}`
    - A **private** typed array view over the WASM shared memory buffer.

**Example Data Structures Exposed by `EngineInterface`:**
```js
engineData = { 
    simulationTime: 12345, 
    bodiesData: new Map([
        [28142348, { 
            enabled: true, 
            mass: 5.972e24, 
            position: { x: 1.496e11, y: 0 }, 
            velocity: { vx: 0, vy: 29780 },
            derived: {
                acceleration: { ax: -5.93e-3, ay: 0 }
            }
        }]
    ]) 
};

metaData = new Map([
	[28142348, {
		name: 'Earth',
		color: '#3a72d6'
    }]
]);
```

`CanvasView`
- Encapsulates all rendering logic using PIXI.js.
- **Responsibilities:**
    - Initializes the `PIXI.Application` and attaches it to the DOM.
    - Subscribes to tick notifications from the `EngineInterface` and consumes the structured `engineData` and `metaData` objects.
    - Renders the simulation by efficiently updating the properties of PIXI.js display objects based on the provided data.
    - Manages all camera logic (zoom, pan, focus tracking).
- **State Ownership:**
    - The `PIXI.Application` instance.
    - A `Map<[bodyId: number], PIXI.DisplayObject>` linking body IDs to their visual representations.
    - Current camera state.

---
## EngineBridge Architecture
The `EngineBridge` serves as the critical communication layer between the JavaScript `WebApp` and the C# `PhysicsEngine`, leveraging Blazor WebAssembly's interop capabilities. It manages the `PhysicsEngine` lifecycle, coordinates data flow through shared memory, and provides a stable JavaScript-callable API surface.

### Design Principles
- **Single Source of Truth**: The `EngineBridge` maintains the authoritative `PhysicsEngine` instance and delegates all physics operations to it
- **Performance-Optimized Communication**: High-frequency data (physics state) flows through shared WASM memory buffers, while low-frequency operations (commands) use direct interop calls
- **Stateless Facade**: The bridge itself maintains minimal state, serving primarily as a coordinator and translator between the WebApp and PhysicsEngine domains
- **Error Boundary**: All C# exceptions are caught and transformed into JavaScript-friendly error responses

### Shared Memory Architecture
The `EngineBridge` implements a dual-buffer system for efficient data transfer:

**SimStateBuffer** - General simulation metadata
```
[byte tickError, double simulationTime, float timeScale, byte timeDirection, int bodyCount]
```

**BodyStateBuffer** - Continuous array of body data blocks
```
Per body: [int id, byte enabled, double mass, double posX, double posY, double velX, double velY, ...]
```

_Note: Additional derived properties (acceleration, orbital elements, etc.) will be appended to this layout as physics requirements are determined during development._

**Memory Management Rules**
- `EngineBridge` has exclusive write access to shared buffers
- `WebApp` has read-only access to shared buffers
- Buffers are updated synchronously after each `PhysicsEngine.Tick()`
- Buffer sizes are pre-allocated based on maximum body count (configurable, default: 1000 bodies)

### API Surface
The `EngineBridge` exposes the following `[JSInvokable]` methods to the WebApp
```csharp
// Simulation Control
[JSInvokable] public void Tick() // Performance critical - errors communicated via tickError flag in SimStateBuffer
[JSInvokable] public string GetTickErrorText() // Returns detailed error message from last failed Tick()
[JSInvokable] public EngineResult<bool> SetTimeScale(float timeScale)
[JSInvokable] public EngineResult<bool> SetTimeDirection(bool forward)
[JSInvokable] public EngineResult<bool> SetSimulationTime(double time)

// Preset Management  
[JSInvokable] public EngineResult<string> GetPreset()
[JSInvokable] public EngineResult<bool> LoadPreset(string presetData)

// Body Management
[JSInvokable] public EngineResult<int> CreateBody(double mass, double posX, double posY, double velX, double velY)
[JSInvokable] public EngineResult<bool> UpdateBody(int id, double mass, double posX, double posY, double velX, double velY)
[JSInvokable] public EngineResult<bool> DestroyBody(int id)

// Memory Buffer Access
[JSInvokable] public int GetSimStateBufferPointer()
[JSInvokable] public int GetBodyStateBufferPointer()
[JSInvokable] public string GetBodyStateBufferLayout() // Returns JSON configuration object describing buffer structure
```

**Standardized Result Structure**
```csharp
public struct EngineResult<T>
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } // Empty string on success
    public T Value { get; set; }
}
```

### Classes
`EngineBridge`
- **Singleton** coordinator that serves as the primary JavaScript interop surface
- **Responsibilities:**
    - Manages the lifecycle of the `PhysicsEngine` instance
    - Translates JavaScript method calls into appropriate `PhysicsEngine` API calls
    - Coordinates the `MemoryBufferHandler` to update shared memory after physics operations
    - Implements comprehensive error handling and transforms C# exceptions into JavaScript-consumable error messages
    - Provides shared memory buffer pointers to the JavaScript layer
- **State Ownership:**
    - `PhysicsEngine` instance (composition)
    - `MemoryBufferHandler` instance (composition)
    - Last tick error message (for on-demand retrieval)

`MemoryBufferHandler`
- Specialized component responsible for efficient shared memory management
- **Responsibilities:**
    - Allocates and manages the dual shared memory buffer system
    - Synchronizes physics engine state into shared buffers after each tick
    - Maintains buffer metadata (sizes, pointers, layouts)
    - Handles buffer overflow scenarios gracefully
- **State Ownership:**
    - `SimStateBuffer`: Fixed-size buffer for simulation metadata
    - `BodyStateBuffer`: Dynamic buffer for celestial body data
    - Buffer configuration parameters (max bodies, buffer sizes)

### Buffer Layout Configuration
The `EngineBridge` provides dynamic buffer layout information to enable flexible development and reduce coupling between C# and JavaScript implementations.

**BodyStateBuffer Layout Object**
```json
{
  "stride": 7,
  "fields": {
    "0": { "name": "id", "type": "int32" },
    "1": { "name": "enabled", "type": "uint8" },
    "2": { "name": "mass", "type": "float64" },
    "3": { "name": "posX", "type": "float64" },
    "4": { "name": "posY", "type": "float64" },
    "5": { "name": "velX", "type": "float64" },
    "6": { "name": "velY", "type": "float64" }
  }
}
```

_Additional fields will be appended with higher indices as derived properties are implemented._

This configuration allows the `WebApp` to dynamically parse buffer data without requiring synchronized updates to both codebases during development.

**Command Flow (Low Frequency):**
1. `WebApp` calls `[JSInvokable]` method on `EngineBridge`
2. `EngineBridge` validates input parameters
3. `EngineBridge` translates call to appropriate `PhysicsEngine` method
4. `PhysicsEngine` executes operation and returns result
5. `EngineBridge` catches any exceptions, formats response, and returns to JavaScript
6. `MemoryBufferHandler` updates shared buffers if physics state changed

**Tick Flow (High Frequency):**
1. `WebApp` calls `EngineBridge.Tick()`
2. `EngineBridge` calls `PhysicsEngine.Tick()`
3. `PhysicsEngine` completes physics calculations
4. `EngineBridge` calls `MemoryBufferHandler.UpdateBuffers()`
5. `MemoryBufferHandler` queries `PhysicsEngine` for current state
6. `MemoryBufferHandler` writes structured data to shared memory buffers
7. `WebApp` reads updated buffer data directly from memory

### Error Handling Strategy
**Performance-Optimized Tick Error Handling**
- `Tick()` method uses shared memory flag for error communication to avoid serialization overhead
- `tickError` flag in SimStateBuffer indicates if the last tick failed
- Detailed error messages are retrieved on-demand via `GetTickErrorText()`
- WebApp checks `tickError` before processing physics data and can pause simulation with user-friendly error display

**Rich Error Handling for Commands**
- All non-performance-critical methods return `EngineResult<T>` struct
- Provides success flag, detailed error message, and return value in a single response
- Enables comprehensive error handling and user feedback

**Exception Translation**
- All C# exceptions are caught at the `EngineBridge` boundary
- Physics validation errors return descriptive error messages
- System exceptions return generic error indicators
- No exceptions propagate to the JavaScript layer

**Graceful Degradation**
- Invalid operations return error responses but don't crash the simulation
- Critical tick errors pause the simulation with error display rather than silent failure
- Memory allocation failures trigger cleanup and retry mechanisms

**Error Communication**
- Tick errors: communicated via `tickError` flag + on-demand error text retrieval
- Command errors: structured `EngineResult<T>` responses with detailed messages
- `WebApp` determines how to handle errors and their user-facing display 

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
public void Tick(); // ticks the simulation once

public void SetSimulationTime(int time); // sets the simulation time 

public int GetSimulationTime(); // gets the simulation time

public void SetTimeScale(float timescale);  // sets the timescale for the simulation

public float GetTimeScale();  // gets the timescale for the simulation

public void SetTimeForward(bool isForward); // sets the direction of time for the simulation

public bool GetTimeForward(); // gets the direction of time for the simulation


// Celestial Bodies
public record CelestialBodyData(int Id, bool Enabled, double Mass, Vector2 Position, Vector2 Velocity); // I might need to add other properties to this record as CelestialBody also has derived properties like certain orbital values for example. I don't yet know which ones though.

public int? CreateBody(double Mass, Vector2 Position, Vector2 Velocity); // creats a CelestialBody instance and adds it to the simulation; is always initiaized with enabled=false and a unique id; the unique id is returned. If the given data was invalid, returns null instead;

public void DestroyBody(int id); // destroys an existing CelestialBody by its id;

public CelestialBodyData? GetBodyData(int id); // gets the data of an existing CelestialBody instance by its id; If none exists retunrs null instead;

public CelestialBodyData[] GetBodyDataAll(); // gets the data of all existing CelestialBody instances

public int? UpdateBody(CelestialBodyData newData); // updates an existing CelestialBody; the unique id is returned. If the given data was invalid, returns null instead; 
```
### Project Structure
```
OrbitalSandbox.Physics/
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
	- Vector2 Position (x, y)
	- Vector2 Velocity (vx, vy)
	- double Mass
	- bool Enabled
### PhysicsEngine.Tick() workflow
Core workflow when Tick() is called on an instance of PhysicsEngine
1. `PhysicsEngine` Enables syncLock (also prevents further PhysicsEngine.Tick() calls)
2. `PhysicsEngine` Calls simulation.Tick()
3. `Simulation` Gets int deltaTime = timer.GetDeltaTime()
4. `Timer` Calculates deltaTime from simulationTime, timeScale, and timeDirection and returns the result
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

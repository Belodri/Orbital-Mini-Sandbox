# UI Implementation Plan

## Constraints & Considerations
- Vanilla JS & CSS
- No fontend libraries like react or svelte
- No ARIA attributes

# UI Components - Overview
### SimulationControls (left sidebar)
Vertically collapsible container with individual subsections
**Simulation Options:** 
- play/pause: button
- time step: number
- gravitational constant: number
- theta: number (min=0, max=1)
- epsilon: number (min=0.0001)
**Visualization Options:** 
- orbit paths: checkbox
- velocity trails: checkbox
- labels: checkbox
- body scale: range (min=0.01, max=1)
**Presets:** 
- import: button
- export: button
**Hotkeys** static reference only
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

### Canvas (DONE)
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


## PLAN FOR NEXT STEPS

### General
- **CSS:** All styles changes that can be handled via toggling of css classes should be handled that way!
- **Control & Data Flow:**
    - Components are owned, created, updated, and deleted by `App` 
    - Components handle all outgoing calls through the static `App`, which serves as the main orchestrator/delegator.
    - **Example:** Click on `BodiesList` "Create" button calls `App.createBody()` which:
        - delegates the creation of a new body in the simulation and awaits the result
        - creates a new `BodyConfig`
        - notifies `BodiesList` that a new body has been added
    - An event bus was briefly considered to decouple individual components but a clear, one-way flow makes debugging much easier at this scale.

### ViewModel
Base class to encapsulated shared code.
- Owns and manages the lifecycle of:
    - `#element` A HTMLDivElement, nested in which are all HTMLElements the ViewModel manages.
    - `#body` A HTMLDivElement and direct child of `#element` that contains all content that should be hidden when the ViewModel is collapsed.
    - `#header` A HTMLDivElement and direct child of `#element` that contains all content that shouldn't be hidden when the ViewModel is collapsed.
        - `#header` elements: title, collapse button, close button
- `toggleCollapse()` method to manage its own collapse state, hiding `#body` when collapsed
- `destroy()` method to manage the cleanup of its own state and the removal of managed DOM elements

### ViewModelMovable extends BaseViewModel
For BodyConfig and BodyDetails components
- Static z-index tracking
    - minimum of 10
    - `getTopZ()` static method that returns the current value of the private tracker and increments it by 1. Safeguard against overflow isn't needed; you'd have to write a script that does nothing but continuously bring windows to the front for this to become an issue
- `#element`
    - event listener for any click to bring it to the front
- `#header`
    - event listener for click-and-drag to update `#element`'s position

## Further Steps
The classes for the individual components (SimulationControls, BodiesList, BodyConfig, and BodyDetails) can then simply extend either `ViewModel` or `ViewModelMovable`.

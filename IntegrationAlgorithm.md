# Goal - Integration Algorithm
The goal of this exercise is to find an algorithm for integrating Newtonian equations of motion in an n-body simulation of point-mass object particles which uses a Quad-Tree (Barnes-Hut) for spatial partitioning - generally but also in the context of my **Orbital Mini-Sandbox** project.
The final algorithm must meet all of the following criteria:

1. Second-order accuracy in time (local truncation error of `O(Δt³`), global error of `O(Δt²)`)
2. Symplectic (to the extend allowed by the Barnes-Hut algorithm)
3. Time-reversable
4. Allows for variable time steps
5. Accurate reading of position, velocity, and acceleration at the same time step
6. Optimized for computational efficiency, assuming variations in time step size are rare
7. Atomic particle updates with clear Read/Write separation

Meanwhile the following aspects are explicity not taken into consideration:
- Thread Safety and multithreading

# Starting Point: Velocity-Verlet
The Velocity-Verlet algorithm will be used as the basis of this work because it can already meet the mathematical criteria (1, 2, 3, 4, and 5) if implemented correctly. 
A single, full step of the common Kick-Drift-Kick (KDK) form of the Velocity-Verlet algorithm works as follows:

1. (Half-)Kick: Calculate `v(t + Δt/2) = v(t) + (a(t)Δt)/2`
2. Drift: Calculate `x(t + Δt) = x(t) + v(t + Δt/2)Δt`
3. Derive `a(t+Δt) = F(x(t + Δt))/m`
4. (Half-)Kick: Calculate `v(t + Δt) = v(t + Δt/2) + a(t + Δt)Δt/2`

## The Problems
While the KDK algorithm looks simple on paper, a direct implementation of it isn't ideal in the context of a simulation that uses a Quad-Tree for spatial partitioning. The complication the Quad-Tree introduces is the fact that it must be rebuild in the middle of the time step - after `x(t+Δt)` is calculated but before `a(t+Δt)` can be derived.
This necessitates two separate loops over the point-mass objects:

```pseudocode
function step_function_kdk(particles, dt, tree):
	foreach P of particles:
		calculate v(t + Δt/2)
			P.v_half = P.v + P.a * dt/2
		calculate x(t+Δt)
			P.x = P.x + P.v_half * dt
		
	rebuild tree using P.x
		tree.Rebuild(particles)
	
	foreach P of particles:
		derive a(t+Δt)
			P.a = tree.CalcAcceleration(P.x)
		calculate v(t+Δt)
			P.v = P.v_half + P.a * dt/2
			
return particles
```

This breaks read/write separation as each particle must be altered twice in each time step - once before the tree is rebuilt, and once afterwards.

# Alternative: Position-Verlet
The Position-Verlet is a variation of the Velocity-Verlet algorithm that promises to solve the previously discussed problem by shifting the order of operations. Instead of usual Kick-Drift-Kick order, this algorithm uses a Force-Kick-Drift (FKD) order:

1. Force: Derive `a(t) = F(x(t))/m`
2. (Full-)Kick: Calculate `v(t + Δt/2) = v(t - Δt/2) + a(t) * Δt`
3. Drift: Calculate `x(t + Δt) = x(t) + v(t + Δt/2) * Δt`

This order neatly shifts the tree rebuild to the start of the time step, allowing for all particle calculations to be done within a single loop:

```pseudocode
function step_function_fkd(particles, dt, tree):
	rebuild tree using P.x
		tree.Rebuild(particles)
	
	foreach P of particles:
		derive a(t)
			P.a = tree.CalcAcceleration(P.x)
		calculate v(t + Δt/2)
			P.v_half = P.v_half + P.a * dt
		calculate x(t + Δt)
			P.x = P.x + P.v_half * dt
	
return particles
```

With only minor modifications, this algorithm can also be made atomic::

```pseudocode
function step_function_fkd(particles, dt, tree):
	rebuild tree using P.x
		tree.Rebuild(particles)

	updates = []

	foreach P of particles:
		a_new = tree.CalcAcceleration(P.x)
		v_half_new = P.v_half + a_new * dt
		x_new = P.x + v_half_new * dt
		updates.add(P, a_new, v_half_new, x_new)

	foreach (P, a_new, v_half_new, x_new) of updates:
		P.a = a_new
		P.v_half = v_half_new
		P.x = x_new
		
return particles
```

While this raises the number of loops back to two, this is generally a worthy trade-off for guaranteed atomicity.

## The Problems
This algorithm introduces several new and major issues however.
#### 1. Incoherent Particle State
The values of a particle's individual properties all represent different points in time. The state variables for a given particle P at time `tₙ` (before the start of the n-th step) are:

`Pₙ.x = x(tₙ)`  The particle's position at time step n
`Pₙ.v_half = v(tₙ - Δtₙ₋₁/2)`   The particle's velocity at the halfway point between time steps n-1 and n
`Pₙ.a = a(tₙ₋₁)`    The particle's acceleration at time step n - 1

#### 2. Velocity at Integer Time Steps
Because the velocity is calculated in a single Kick of `Δt`, rather than two separate Kicks of `Δt/2` each, the velocity at integer time steps `v(t + Δt)` is never calculated. 
Since `v(t+Δt)` depends on `a(t + Δt)`, which in turn depends on `x(t + Δt)`, a calculation of `v(t + Δt)` would require a full, additional rebuild of the quad-tree. The impact on performance this would have makes this a non-option.

Instead the `v(t + Δt)` can be estimated through a linear acceleration extrapolation from `a(tₙ₋₁)` with an accuracy of `O(Δt²)`, though this would require  `a(tₙ₋₁)` to be stored on the particle:

1. `aₑₛₜ(tₙ + Δtₙ) ≈ a(tₙ) + ( a(tₙ) - a(tₙ₋₁) )`
2. `vₑₛₜ(tₙ + Δtₙ) ≈ v(tₙ + Δtₙ/2) + aₑₛₜ(tₙ + Δtₙ) * Δtₙ/2`

A beneficial side effect of this is that it would also estimate the acceleration at time step n, which is otherwise unknown. Note however that since both `aₑₛₜ(tₙ)` and `vₑₛₜ(tₙ)` are only estimates, they cannot be used for future integrations as doing so would introduce a self-accumulating numerical drift.

#### 3. Variable Timestep Kick
Since the assertion that `Pₙ.v_half = v(tₙ - Δtₙ₋₁/2)` is only true if `Δtₙ₋₁ = Δtₙ`, each time the time step varies, a non-self-accumulating numerical drift is introduced. 
That said, this issue can be mitigated entirely through a simple modification of the Kick step:

1. Reset the half-step center, assuming `v(tₙ - Δtₙ₋₁/2)` is stored but `v(tₙ - Δtₙ/2)` is needed.
   `v(tₙ - Δtₙ/2) = v(tₙ - Δtₙ₋₁/2) + a(tₙ) * (Δtₙ₋₁ - Δtₙ) / 2`
2. Apply the full step kick.
   `v(tₙ + Δtₙ/2) = v(tₙ - Δtₙ/2) + a(tₙ) * Δtₙ`

Simplified, this results in the following equation for the variable time step kick:

`v(tₙ + Δtₙ/2) = v(tₙ - Δtₙ₋₁/2) + a(tₙ) * (Δtₙ₋₁ + Δtₙ) / 2`

#### 4. Kick-Start
The Kick step of this FDK algorithm requires the velocity at the previous time step's half-step `v(t - Δt/2)`. As such, the first time any particle in the simulation is ever present in a time step, it must be kick-started to initialize the `v(t - Δt/2)`:

1. Force: Derive `a(tₙ) = F(x(tₙ))/m`
2. Backwards Half-Kick: Calculate `v(tₙ - Δtₙ₋₁/2) = v(tₙ) - a(tₙ) * Δtₙ₋₁/2`
3. Kick: Calculate `v(tₙ + Δtₙ/2) = v(tₙ - Δtₙ₋₁/2) + a(tₙ) * (Δtₙ₋₁ + Δtₙ) / 2`
4. Drift: Calculate `x(tₙ + Δtₙ) = x(tₙ) + v(tₙ + Δtₙ/2) * Δtₙ`

*Note: On the very first time step of a new simulation, `Δtₙ₋₁` is likely not known. In this case `Δtₙ` should be used as a substitude. Because this is essentially a first-order Euler integration step applied backwards in time, the very first step of a new simulation breaks time-reversability.*

While this introduces additional algorithmic complexity, its impact on performance is negligable because of the operation's cheap computational cost and one-time nature per particle.

# The Solution - Thinking Ahead
It's clear that the Position-Verlet algorithm is anything but the perfect solution. While many of its downsides have workarounds, it still fails to address the core goal of needing to know the precise values for position, velocity and acceleration at integer time steps. The reality is, there is no perfect solution that combines accuracy, symplecticity, time-reversability, time-step-variability, and interger-step state-synchronization - within a single step. 

The core idea behind this approach is to build the current state not only from the known past but also from an estimated future. This would be simple (and unnecessary) in a time-step-invariant context and pointless in a context where the time steps vary wildly and often. But since one of the core assumptions of this project states that "variations in time step size are rare", a computationally efficient implementation of this idea is complex but possible. 

# The Mental Model of State in Time
Since the mental modeling of a state's progression through time can difficult, counter-intuitive, and riddled with ambiguity at the best of times, let's first establish an exact definitions for "current", "next", and "previous" state in the context of this algorithm. 

## `n` - The Current State
The current (or present) state is the state of the simulation that can be observed at a given moment. Pausing a simulation essentially means freezing the passage of time to preserve the current state.

In more technical terms, the current state is defined as the state of the simulation at time `tₙ`, which is the result of the previous evaluation of the `step_function` at time `tₙ₋₁`.

The time at the current state is defined as: `tₙ = tₙ₋₁ + Δtₙ₋₁`

It should be noted that the following two concepts can easily be mistaken for being different, even though they're identical and  are both represented by the current state `n`. 
- the simulation state at the end of the `step_function` that was called with `Δtₙ₋₁` 
- the simulation state at the start of the `step_function` that is being called with `Δtₙ`

## `n+1` - The Next State
The next state is the state an observer will be able to observe once they can no longer observe the current state. At this state the simulation that is still unknown to an observer. As long as a simulation is paused, the next state will never be known to an observer.

The next state is defined as the state of the simulation at time `tₙ₊₁`, which will be the result of the evaluation of the `step_function` in the present at time `tₙ`.

The time at the next state is defined as: `tₙ₊₁ = tₙ + Δtₙ`

It is important to understand that the next state only becomes the current state at the end of the `step_function` call at time `tₙ`.

## `n-1` - The Previous State
The previous state is the state of the simulation that was observable but isn't any longer. 

The previous state is defined as the state of the simulation at time `tₙ₋₁`, which is a the result of the evaluation of the `step_function` at its own step at time `Δtₙ₋₁`. 

The time at the previous state is defined as: `tₙ₋₁ = tₙ - Δtₙ₋₁`

## `n-m` & `n+m` Beyond Previous and Next
*included for completion's sake*
While most of work and mental load revolves around the range of states `n-1` to `n+1`, other states before and after naturally exist as well. 


# Temporal Context
To make this concept of a transient and shifting view of time more intuitive and easier to work with, let a temporal context TC be defined as a collection that holds individual and distinct states of integer steps.

*Note that this is a purely mental model and does not represent a concrete data structure.*

`...`
`TC(n-1): { x(tₙ₋₁), v(tₙ₋₁), a(tₙ₋₁), tₙ₋₁, Δtₙ₋₁ }
`TC(n): { x(tₙ), v(tₙ), a(tₙ), tₙ, Δtₙ }
`TC(n+1): { x(tₙ₊₁), v(tₙ₊₁), a(tₙ₊₁), tₙ₊₁, Δtₙ₊₁ }
`...`

Since not all properties can have a value in all states - that depends on the chosen integration algorithm - they can still be thought of as buckets that might or might not contain anything.

The Velocity-Verlet algorithm works with a TC of size 1 - it only contains the state `n`. Since TC(n) contains everything the algorithm needs to evaluate the next state, it doesn't need a larger TC.

The Position-Verlet algorithm on the other hand can vary. In its timestep-invariant form, it also has a TC of size 1. 

In the timestep-variant form however, the algorithm needs a TC of size 2, which contains both `n` and `n-1`. This is because in order to calculate the velocity at the half-step of `n`, it must take the size of the previous half-step `Δt` into account.
`v(tₙ + Δtₙ/2) = v(tₙ - Δtₙ₋₁/2) + a(tₙ) * (Δtₙ₋₁ + Δtₙ) / 2`

And since the TC can only hold states of integer time steps, the full `n-1` state must be included.

Restriction of the TC might appear like an arbitrary limitation but without it, the ambiguity that this mental model is designed to eliminate begins to creep back in. This becomes abundantly clear when we talk about the boundaries between states and the need for a precise moment when all states shift at the same time.


---
# WORK IN PROGRESS SECTIONS
### Context Shifts
The contents of the TC shift at the END of the `step_function`. At that moment, the contents of the TC change as follows:
- TC(n-1) => TC(n-2) and disappears from our context window
- TC(n) => TC(n-1)
- TC(n+1) => TC(n)
- TC(n+2) => TC(n+1)


## The Cost of `Δt` Changes
Since it is assumed that `Δt` rarely changes, this algorithm actually calculates TC(n+1) at time step n while returning TC(n) to callers.
In the rare cases where at time step n the `Δtₙ` input is different from `Δtₙ₋₁`, which the last time steps's pre-calculation of n was based on, the result of that pre-calculation is discarded and re-computed in-time.

For steps where `Δt` changes, the algorithm must:
1. Discard the pre-calculated results for TC(n)
2. Re-calculate TC(n) from TC(n-1) using the new, correct `Δt`
3. Calculate TC(n+1) from the newly calculated TC(n) 

Since this involves two full integration steps, the impact of `Δt` changes on the algorithm's pure  computational efficiency is straightforward to calculate as:
`x(y + z)` where `x` is the number of seconds a simulation with `y` steps takes to run if `Δt` changes `z` times. 


## TODO - Other topics to explore
- viability of both the Velocity-Verlet and the Position-Verlet algorithm in this temporal context
- how both can be implemented (both in theory and in pseudocode)
- the concessions and considerations of each
- how the algorithm is bootstrapped
- how a proper "rewind" system can be implemented efficiently with relative ease (handling time reversability through storing  `tₙ` and `Δtₙ` pairs in a stack (FILO) when `Δtₙ` changes )
- general concessions such as a memory overhead that scales with the number of particles in the simulation at 2-3x the rate of simple, single-step integrations  
etc.

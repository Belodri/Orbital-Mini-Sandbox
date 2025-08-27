# Overview

The **Orbital Mini-Sandbox** is a real-time n-body simulation of point-mass bodies which uses a Quad-Tree (Barnes-Hut) for spatial partitioning and implements the **Velocity-Verlet** algorithm for integrating Newtonian equations of motion. Rather than a tool for scientific work, the project aims to be a sandbox for both entertainment and education, letting users set up, explore, and manipulate "what-if" scenarios of orbital mechanics. 

This document explores the mathematical basis for the simulation - the integration algorithm. What specific requirements and constraints this project poses, how the chosen **Velocity-Verlet** works, how it was implemented, and an overview of possible alternative options.

*The documentation for the project's overall design and architecture documentation can be found in [PDD.md](PDD.md).*

# Requirements & Constraints
It is important to recognize that the **Orbital Mini-Sandbox** is designed to run in real-time and in a single-threaded environment. As such, it has to prioritize certain aspects such as performance and stability, over accuracy, even though that is still a highly important factor. 

- **Performance:** The **Orbital Mini-Sandbox** must maintain minimum stable frame rate of 60 fps at 100 simulated bodies.
- **Stability:** Numerical stability is more important than physical accuracy. To avoid drifting orbits, the simulation's total energy must be conserved across many time steps.
- **Determinism:** To make sharable presets useful, the simulation must be perfectly deterministic (outside of floating-point errors).
- **Full-Step State:** To provide a coherent and accurate reading of the true simulation state, the evaluated properties of each body (such as position, velocity, and acceleration) must be synchronized and calculated (not predicted) at integer time steps.
- **Accuracy:** The simulation should aim for the highest degree of accuracy possible within these constraints.


# Velocity-Verlet
The Velocity-Verlet algorithm is implemented in it's common Kick-Drift-Kick format, which defines a single full-step as follows:

1. **(Half-)Kick** $v(t + \frac{\Delta t}{2}) = v(t) + a(t) \frac{\Delta t}{2} $
2. **Drift** $x(t + \Delta t) = x(t) + v(t + \frac{\Delta t}{2}) \Delta t$
3. **Derive** $a(t + \Delta t) = \frac{F(x(t + \Delta t))}{m}$
4. **(Half-)Kick** $v(t + \Delta t) = v(t + \frac{\Delta t}{2}) + a(t + \Delta t) \frac{\Delta t}{2}$

## Strengths
- **Performance:** Because the position is only calculated once per time-step, the Quad-Tree must be rebuilt only once per time step. Since this is one of - if not the - most computationally expensive operation in the simulation's hot path, limiting its rebuild frequency to once per time-step is vital for performance.
- **Algorithmic Symplecticity:** While the Barnes-Hut approximations prevent true symplecticity of the simulation as a whole (depending on the chosen value for Theta $\theta$), a symplectic integration algorithm is nevertheless vital to prevent runaway energy-drift in a long-running simulation.
- **Algorithmic Time-Reversibility:** Assuming the simulation's other parameters aren't externally modified, this algorithm is time-reversable by setting a negative $\Delta t$. Note that the Barnes-Hut approximations can prevent true time-reversibility of the simulation as a whole.
- **2nd-Order Accuracy:** With a global error of $O(\Delta t^2)$ (and a local truncation error of $O(\Delta t^3)$), this algorithm provides a clear improvement over common first-order accurate integration methods such as Symplectic Euler.
- **Full-Step State:** Since the evaluated properties (such as position, velocity, and acceleration) of all bodies are synchronized at integer time steps, this algorithm provides a coherent and accurate reading of the true simulation state.

## The Quad-Tree Problem
The implementation of this rather straightforward algorithm is complicated by the need to account for the Quad-Tree for spacial partitioning, which requires the tree to be rebuilt in the middle of the time step - after $x(t + \Delta t)$ is calculated but before $a(t + \Delta t)$ can be derived.
This necessitates two separate loops over the bodies, which breaks strict read/write separation as each body must be altered twice in each time step - once before the tree is rebuilt, and once afterwards.

```pseudocode
function step_function(bodies, dt, tree):
	foreach B of bodies:
		// Calculate v(t + Δt/2)
		B.v_half = B.v + B.a * dt/2
		// Calculate x(t+Δt)
		B.x = B.x + B.v_half * dt
		
	// rebuild tree using B.x
	tree.Rebuild(bodies)
	
	foreach B of bodies:
		// Derive a(t+Δt)
		B.a = tree.CalcAcceleration(B.x)
		// Calculate v(t+Δt)
		B.v = B.v_half + B.a * dt/2
			
return bodies
```

Separating read/write steps would require either a different integration algorithm such as the **Leapfrog** method, a swappable read-write buffer of bodies, or a complete redesign of the Quad-Tree to encode the properties of the bodies directly instead of simply storing references.
All come with their own downsides however:
- The issues with the **Leapfrog** algorithm are explored in great detail below in the section of the same name.
- The swappable read-write buffer would nearly double the memory requirements of the simulation and introduce a whole host of potential state synchronization issues such as for update calls.
- The redesign of the Quad-Tree would increase the complexity of an already very complex component that must be highly performant.

While this separation - and thus the integrator itself - would be a major problem in a multi-threaded environment, in the single-threaded context of this project, it can be viewed more as an academic flaw rather than a practical one. As such, these workarounds have been deemed unnecessary for the **Orbital Mini-Sandbox** in favour of a simpler approach with 2 distinct read-write steps.

## Kick-Start & Updates
Evaluation of the half-step velocities $ v(t + \frac{\Delta t}{2}) = v(t) + a(t) \frac{\Delta t}{2} $ requires both the velocity $v(t)$ as well as the acceleration $a(t)$ at the beginning of the timestep - aka the end of the previous time step. When the simulation is initialized, the bodies' positions and velocities are given but because the acceleration depends on the other bodies in the system, it must be evaluated. This leads to a kick-start step, in which the acceleration is derived from the positions at $t=0$, necessitating a full rebuild of the Quad-Tree.

**Kick-Start:** $a(t) = \frac{F(x(t))}{m}$

If the simulation couldn't be modified externally while running, this would be a one-time step. But since the **Orbital Mini-Sandbox** allows user to create new bodies and delete or update existing ones while the simulation is running, this "synchronization" step is necessary before every time-step immediately following such an interaction.

## Full Implementation
To illustrate how this algorithm maps directly to the project's architecture, the final C# implementation - from `src/Physics/Core/Simulation.cs` - is provided below. Further context and details can be found in the project's PDD.

```csharp
/// <summary>
/// Advances the simulation by a single timestep.
/// Calculates the forces on all enabled bodies and updates their properties like position, velocity and acceleration.
/// </summary>
public void StepFunction()
{
    VelocityVerletStep();
    Timer.AdvanceSimTime();
}

private void VelocityVerletStep()
{
    if (Bodies.EnabledCount == 0) return;

    if (_queueSync)
    {
        // If a resynchronization of the system is necessary
        // (on initial step or after a body has been added/deleted/modified externally),
        // re-evaluate the forces at time t.
        RebuildQuadTree();
        for (int i = 0; i < Bodies.EnabledCount; i++)
        {
            var body = Bodies.EnabledBodies[i];
            var a = QuadTree.CalcAcceleration(body, Calculator);
            body.Update(acceleration: a);
        }

        _queueSync = false;
    }

    for (int i = 0; i < Bodies.EnabledCount; i++)
    {
        var body = Bodies.EnabledBodies[i];

        // Step 1: Half-Kick
        // v(t + Δt/2) = v(t) + (a(t)Δt)/2
        var v_half = body.Velocity + body.Acceleration * Timer.DeltaTimeHalf;

        // Step 2: Drift
        // x(t + Δt) = x(t) + v(t + Δt/2)Δt
        var x = body.Position + v_half * Timer.DeltaTime;

        body.Update(position: x, velocityHalfStep: v_half);
    }

    RebuildQuadTree();

    for (int i = 0; i < Bodies.EnabledCount; i++)
    {
        var body = Bodies.EnabledBodies[i];

        // Step 3: Force
        // a(t+Δt) = F(x(t + Δt))/m
        var a = QuadTree.CalcAcceleration(body, Calculator);

        // Step 4: Half-Kick
        // v(t + Δt) = v(t + Δt/2) + a(t + Δt)Δt/2
        var v = body.VelocityHalfStep + a * Timer.DeltaTimeHalf;

        body.Update(acceleration: a, velocity: v);
    }
}

```

# Alternatives: Leapfrog
Also called the Position-Verlet method is a variation of the Velocity-Verlet algorithm that promises to solve the previously discussed problem by shifting the order of operations. Instead of usual Kick-Drift-Kick order, this algorithm uses a Force-Kick-Drift order:

1. **Force** $a(t) = \frac{F(x(t))}{m}$
2. **(Full-)Kick** $v(t + \frac{\Delta t}{2}) = v(t - \frac{\Delta t}{2}) + a(t) \Delta t$
3. **Drift** $x(t + \Delta t) = x(t) + v(t + \frac{\Delta t}{2}) \Delta t$

This order neatly shifts the tree rebuild to the start of the time step, allowing for all body calculations to be done within a single loop:

```pseudocode
function step_function(bodies, dt, tree):
	rebuild tree using B.x
		tree.Rebuild(bodies)
	
	foreach B of bodies:
		derive a(t)
			B.a = tree.CalcAcceleration(B.x)
		calculate v(t + Δt/2)
			B.v_half = B.v_half + P.a * dt
		calculate x(t + Δt)
			B.x = B.x + B.v_half * dt
	
return bodies
```

With only minor modifications, this algorithm be easily separated into read and write steps:

```pseudocode
function step_function(bodies, dt, tree):
	rebuild tree using B.x
		tree.Rebuild(bodies)

	updates = []

	foreach B of bodies:
		a_new = tree.CalcAcceleration(B.x)
		v_half_new = B.v_half + a_new * dt
		x_new = B.x + v_half_new * dt
		updates.add(B, a_new, v_half_new, x_new)

	foreach (B, a_new, v_half_new, x_new) of updates:
		B.a = a_new
		B.v_half = v_half_new
		B.x = x_new
		
return bodies
```

While this algorithm offers guaranteed read-write separation, it also introduces several new and major issues.

## Incoherent Body State
The values of a body's individual properties all represent different points in time. While this is a fundamental property of these kinds of "Leapfrog" integrators, it violates one of the set goals. The state variables for a given body at time $t_n$ (the start of the $n$-th step) are:

- $x(t_n)$ The body's position at time step $n$ 
- $v(t_n - \frac{\Delta t_{n-1}}{2})$ The body's velocity at the halfway point between time steps $n-1$ and $n$
- $a(t_{n-1})$ The body's acceleration at time step $n-1$

Because the velocity is calculated in a single kick of $\Delta t$, rather than two separate kicks of $\frac{\Delta t}{2}$ each, the velocity at integer time steps $v(t_n)$ is never calculated. And while the acceleration is calculated at integer time steps, it is always one step "behind" the position. 

Since $v(t + \Delta t)$ depends on $a(t + \Delta t)$, which in turn depends on $x(t + \Delta t)$, a calculation of $v(t + \Delta t)$ would require a full, additional rebuild of the quad-tree. The impact on performance this would have makes this a non-option.

Instead the $v(t + \Delta t)$ can be estimated through a linear acceleration extrapolation from $a(t_{n-1})$ with an accuracy of $O(\Delta t^2)$, though this would require $a(t_{n-1})$ to be stored on the body:

1. $a_{est}(t_n + \Delta t_n) \approx 2a(t_n) - a(t_{n-1}) $
2. $v_{est}(t_n + \Delta t_n) \approx v(t_n + \frac{\Delta t_{n-1}}{2}) + \frac{{\Delta t_n} a_{est}(t_n + \Delta t_n)}{2} $


Note however that since both $a_{est}(t_n + \Delta t_n)$ and $v_{est}(t_n + \Delta t_n)$ are only estimates, they cannot be used for future integrations as doing so would introduce a self-accumulating numerical drift.

## Variable Timestep Kick
Since the assertion that $v(t_n - \frac{\Delta t_{n-1}}{2})$ is only true if $\Delta t_{n-1} = \Delta t_n$, each time the time step varies, a non-self-accumulating numerical drift is introduced. 

That said, this issue can be mitigated entirely through a simple modification of the Kick step:

1. Reset the half-step center, assuming $v(t_n - \frac{\Delta t_{n-1}}{2})$ is stored but $v(t_n - \frac{\Delta t_n}{2})$ is needed

    $v(t_n - \frac{\Delta t_n}{2}) = v(t_n - \frac{\Delta t_{n-1}}{2}) + a(t_n) \frac{\Delta t_{n-1} - \Delta t_n}{2}$

2. Apply the full step kick

    $v(t_n + \frac{\Delta t_n}{2}) = v(t_n - \frac{\Delta t_n}{2}) + a(t_n) \Delta t_n$


Simplified, this results in the following equation for the variable time step kick.

$v(t_n + \frac{\Delta t_n}{2}) = v(t_n - \frac{\Delta t_{n-1}}{2}) + a(t_n) \frac{\Delta t_{n-1} + \Delta t_n}{2}$



## Kick-Start
The kick step of this algorithm requires the velocity at the previous half-step $v(t_{n-1} + \frac{\Delta t_{n-1}}{2})$.
As such, the first time any body in the simulation is ever present in a time step, it must be kick-started to initialize the $v(t - \frac{\Delta t}{2})$.

1. Force $a(t_n) = \frac{F(x(t_n))}{m}$

2. Backwards-Half-Kick $v(t_{n-1} + \frac{\Delta t_{n-1}}{2}) = v(t_n) - a(t_n) \frac{\Delta t_{n-1}}{2}$  

    Note: $t_{n-1}$ is not required because because $v(t_{n-1} + \frac{\Delta t_{n-1}}{2}) = v(t_n - \frac{\Delta t_{n-1}}{2})$

3. Kick $v(t_n + \frac{\Delta t_n}{2}) = v(t_{n-1} + \frac{\Delta t_{n-1}}{2}) + a(t_n) \frac{\Delta t_{n-1} + \Delta t_n}{2}$

4. Drift $x(t_n + \Delta t_n) = x(t_n) + v(t_n + \frac{\Delta t_n}{2}) \Delta t_n $


*Note: On the very first time step of a new simulation, $\Delta t_{n-1}$ is likely not known. In this case $\Delta t_n$ should be used as a substitute. Because this is essentially a first-order Euler integration step applied backwards in time, the very first step of a new simulation breaks time-reversability.*

While this introduces additional algorithmic complexity, its impact on performance is negligable because of the operation's cheap computational cost and one-time nature per body.


# Non-Alternatives

### Euler Methods
- **Forward Euler:** One of the most basic and well-known numerical integration methods, its numerical instability and first-order ($O(\Delta t)$) accuracy make this a non-option for this project.
- **Semi-Implicit Euler:** While this modification of the classic Euler method, also known the Symplectic Euler method, introduces symplecticity and thus solves the original's issues with numerical instability, it retains the $O(\Delta t)$ accuracy.

### Runge-Kutta-4
The most widely known member of the family of numerical integrators known as Runge-Kutta, this is a fourth order method, boasting a global truncation error of $O(\Delta t^4)$ and a local error of $O(\Delta t^5)$. While this level of accuracy is often desired in scientific computing, this method not only leads to numerical drift due to its explicit nature, but its implementation is also very computationally expensive.

It works by estimating the velocity and acceleration of a body at 4 specific points in time between $t_n$ and $t_n + \Delta t_n$, before determining $x(t_n + \Delta t_n)$ by via the weighted average of these estimations. This means not only a large the number of individual calculations per full time step, but also a total of 4 rebuilds of the Quad-Tree.
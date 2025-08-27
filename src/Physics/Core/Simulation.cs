namespace Physics.Core;

internal interface ISimulation
{
    /// <summary>
    /// Gets the timer responsible for managing the flow and scale of time within the simulation.
    /// </summary>
    /// <seealso cref="ITimer"/>
    ITimer Timer { get; }
    /// <summary>
    /// Gets the QuadTree used for spatial partitioning of bodies.
    /// </summary>
    /// <seealso cref="QuadTree"/>
    QuadTree QuadTree { get; }
    /// <summary>
    /// Gets the calculator that contains the logic for determining physical interactions, such as gravitational forces.
    /// </summary>
    /// <seealso cref="ICalculator"/>
    ICalculator Calculator { get; }
    /// <summary>
    /// Gets the BodyManager that contains and manages the bodies of the simulation.
    /// </summary>
    IBodyManager Bodies { get; }
    /// <summary>
    /// Advances the simulation by a single timestep.
    /// Calculates the forces on all enabled bodies and updates their properties like position, velocity and acceleration.
    /// </summary>
    void StepFunction();
}

internal sealed class Simulation : ISimulation
{
    public Simulation(ITimer timer, QuadTree quadTree, ICalculator calculator, IBodyManager bodyManager)
    {
        Timer = timer;
        QuadTree = quadTree;
        Calculator = calculator;
        Bodies = bodyManager;

        Bodies.EnabledContentModified += () => _queueSync = true;
    }
    /// <summary>
    /// Flag to indicate whether a resynchronization of the simulation state 
    /// is necessary before the next time step.
    /// </summary>
    private bool _queueSync = true;

    public ITimer Timer { get; init; }
    public QuadTree QuadTree { get; init; }
    public ICalculator Calculator { get; init; }
    public IBodyManager Bodies { get; init; }


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

    private void RebuildQuadTree()
    {
        double minX = double.MaxValue;
        double minY = double.MaxValue;
        double maxX = double.MinValue;
        double maxY = double.MinValue;

        for (int i = 0; i < Bodies.EnabledCount; i++)
        {
            var body = Bodies.EnabledBodies[i];
            minX = Math.Min(minX, body.Position.X);
            minY = Math.Min(minY, body.Position.Y);
            maxX = Math.Max(maxX, body.Position.X);
            maxY = Math.Max(maxY, body.Position.Y);
        }
        QuadTree.Reset(minX, minY, maxX, maxY, Bodies.EnabledCount);
        for (int i = 0; i < Bodies.EnabledCount; i++) QuadTree.InsertBody(Bodies.EnabledBodies[i]);
        QuadTree.Evaluate();
    }
}

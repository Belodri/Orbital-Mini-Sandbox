using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Physics;

namespace Bridge;

internal class MemoryBufferHandler : IDisposable
{
    public nint SimBufferPtr { get; private set; }
    public nint BodyBufferPtr { get; private set; }

    public int SimBufferSizeInBytes { get; private set; }
    public int BodyBufferSizeInBytes { get; private set; }

    private int _bodyCapacity;

    private readonly int _simStride = Layouts.SimKeys.Count;
    private readonly int _bodyStride = Layouts.BodyKeys.Count;

    // Staging buffer for GC efficient memory writes
    private double[] _bodiesStagingBuffer;  

    // Layout for Float64 buffer for C# writer.
    public static readonly SimStateLayoutRec SimLayout = Layouts.SimMemLayout;
    public static readonly BodyStateLayoutRec BodyLayout = Layouts.BodyMemLayout;

    public MemoryBufferHandler(int initialBodyCapacity = 10)
    {
        if (initialBodyCapacity <= 0)
            throw new ArgumentException("Initial body capacity must be positive", nameof(initialBodyCapacity));

        _bodyCapacity = initialBodyCapacity;

        // Calculate the required size for the buffers.
        unsafe
        {
            SimBufferSizeInBytes = _simStride * sizeof(double);
            BodyBufferSizeInBytes = _bodyStride * sizeof(double) * _bodyCapacity;
        }

        // Allocate the unmanaged memory.
        SimBufferPtr = Marshal.AllocHGlobal(SimBufferSizeInBytes);
        BodyBufferPtr = Marshal.AllocHGlobal(BodyBufferSizeInBytes);

        // Setup the staging buffer with the initial capacity
        _bodiesStagingBuffer = new double[_bodyCapacity * _bodyStride];
    }

    #region Data Writing

    public void WriteViewToMemory(SimulationView view)
    {
        // Resize if needed
        EnsureBodyCapacity(view.Bodies.Count);
        WriteFixedBuffer(view);
        WriteDynamicBuffer(view.Bodies);        
    }

    private unsafe void WriteFixedBuffer(SimulationView view)
    {
        double* pSimState = (double*)SimBufferPtr;

        pSimState[SimLayout.simulationTime] = view.SimulationTime;
        pSimState[SimLayout.timeStep] = view.TimeStep;
        pSimState[SimLayout.bodyCount] = view.Bodies.Count;
        pSimState[SimLayout.theta] = view.Theta;
        pSimState[SimLayout.gravitationalConstant] = view.G_SI;
        pSimState[SimLayout.epsilon] = view.Epsilon;
    }

    private unsafe void WriteDynamicBuffer(IReadOnlyList<BodyView> bodies)
    {
        int bodyCount = bodies.Count;
        if (bodyCount == 0) return;

        int totalDoubles = bodyCount * _bodyStride;

        // Span to represent only the used portion of the reusable array.
        var allBodiesData = new Span<double>(_bodiesStagingBuffer, 0, totalDoubles);

        for (int i = 0; i < bodyCount; i++)
        {
            BodyView body = bodies[i];
            var bodySlice = allBodiesData.Slice(i * _bodyStride, _bodyStride);

            bodySlice[BodyLayout.id] = body.Id;
            bodySlice[BodyLayout.enabled] = body.Enabled ? 1.0 : 0.0;
            bodySlice[BodyLayout.mass] = body.Mass;
            bodySlice[BodyLayout.posX] = body.Position.X;
            bodySlice[BodyLayout.posY] = body.Position.Y;
            bodySlice[BodyLayout.velX] = body.Velocity.X;
            bodySlice[BodyLayout.velY] = body.Velocity.Y;
            bodySlice[BodyLayout.accX] = body.Acceleration.X;
            bodySlice[BodyLayout.accY] = body.Acceleration.Y;
            bodySlice[BodyLayout.outOfBounds] = body.OutOfBounds ? 1.0 : 0.0;
        }

        fixed (double* pSource = allBodiesData)
        {
            Unsafe.CopyBlock((double*)BodyBufferPtr, pSource, (uint)(totalDoubles * sizeof(double)));
        }
    }

    private void EnsureBodyCapacity(int requiredBodyCount)
    {
        if (requiredBodyCount <= _bodyCapacity) return;

        int newCapacity = _bodyCapacity;
        while (newCapacity < requiredBodyCount) { newCapacity *= 2; }
        _bodyCapacity = newCapacity;

        int newSizeInBytes;
        unsafe { newSizeInBytes = _bodyStride * sizeof(double) * _bodyCapacity; }

        // Reallocate the unmanaged memory block.
        BodyBufferPtr = Marshal.ReAllocHGlobal(BodyBufferPtr, newSizeInBytes);
        BodyBufferSizeInBytes = newSizeInBytes;

        // Resize the staging buffer to match capacity.
        _bodiesStagingBuffer = new double[_bodyCapacity * _bodyStride];
    }

    #endregion


    #region IDisposable implementation

    private bool _disposed = false;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            // Clean up unmanaged resources.
            if (SimBufferPtr != nint.Zero)
            {
                Marshal.FreeHGlobal(SimBufferPtr);
                SimBufferPtr = nint.Zero;
            }

            if (BodyBufferPtr != nint.Zero)
            {
                Marshal.FreeHGlobal(BodyBufferPtr);
                BodyBufferPtr = nint.Zero;
            }

            _disposed = true;
        }
    }

    ~MemoryBufferHandler() => Dispose(false);

    #endregion
}
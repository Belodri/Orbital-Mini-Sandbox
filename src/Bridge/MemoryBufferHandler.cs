using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Physics;

namespace Bridge;

internal class MemoryBufferHandler : IDisposable
{
    // Pointers
    private nint _simBufferPtr;
    private nint _bodyBufferPtr;
    // Public Accessors
    public nint SimBufferPtr => _simBufferPtr;
    public nint BodyBufferPtr => _bodyBufferPtr;

    // Sizes in bytes
    private readonly int _simBufferSizeInBytes;
    private int _bodyBufferSizeInBytes;
    // Public Accessors
    public int SimBufferSizeInBytes => _simBufferSizeInBytes;
    public int BodyBufferSizeInBytes => _bodyBufferSizeInBytes;

    // Capacity in number of bodies
    private int _bodyCapacity;

    // Staging buffer for GC efficient memory writes
    private double[] _bodiesStagingBuffer;

    #region Static Dynamic Layout Creation

    // This region contains the logic to automatically generate all necessary layout configurations
    // from the record definitions above. This ensures the design is DRY 
    // and that there is a single source of truth for the memory layout.
    //
    // This process creates two artifacts for each layout:
    //   1. A `string[]` (e.g., `SimStateLayoutArr`): An array of the property names.
    //      This is exported to JavaScript, allowing the JS side to understand the data structure
    //      without hardcoded values.
    //
    //   2. An instance of the record itself (e.g., `SimStateLayout`): A strongly-typed object
    //      where each property holds its own integer index (e.g., `SimStateLayout._bodyBufferPtr` will be 0).
    //      This is used internally by the C# hot path for maximum performance, avoiding dictionary lookups.
    //
    // This is achieved using minimal, AOT-safe reflection that runs only once during static initialization,
    // ensuring no performance impact at runtime.

    // Layout for Float64 buffer.
    public static readonly string[] SimStateLayoutArr = GetRecordKeys<SimStateLayoutRec>();
    public static readonly string[] BodyStateLayoutArr = GetRecordKeys<BodyStateLayoutRec>();

    public static readonly SimStateLayoutRec SimStateLayout = CreateInstanceFromLayout<SimStateLayoutRec>(SimStateLayoutArr);
    public static readonly BodyStateLayoutRec BodyStateLayout = CreateInstanceFromLayout<BodyStateLayoutRec>(BodyStateLayoutArr);

    /// <summary>
    /// Creates an array of property names (keys) from a record type.
    /// This method is compatible with trimming and Native AOT.
    /// </summary>
    /// <typeparam name="T">The record or class type to inspect.</typeparam>
    /// <returns>A string array containing the names of the record's public, declared properties.</returns>
    private static string[] GetRecordKeys<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>() where T : class
    {
        return [.. typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(p => p.Name)];
    }

    /// <summary>
    /// Creates an instance of a record type from a layout array.
    /// The value for each property is set to the index of its name in the layout array.
    /// </summary>
    /// <typeparam name="T">The record type to create. Must have a single public constructor with only int parameters.</typeparam>
    /// <param name="layout">An array of strings where each string is a property name of the record.</param>
    /// <returns>A new instance of the record T.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the record does not have a single, unique public constructor.</exception>
    private static T CreateInstanceFromLayout<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(string[] layout) where T : class
    {
        var constructor = typeof(T).GetConstructors().Single();
        var constructorParams = constructor.GetParameters();

        var arguments = constructorParams.Select(param =>
        {
            int index = Array.IndexOf(layout, param.Name);
            return (object)index;
        }).ToArray();

        return (T)constructor.Invoke(arguments);
    }

    #endregion

    public MemoryBufferHandler(int initialBodyCapacity = 10)
    {
        if (initialBodyCapacity <= 0)
            throw new ArgumentException("Initial body capacity must be positive", nameof(initialBodyCapacity));

        _bodyCapacity = initialBodyCapacity;

        // Calculate the required size for the buffers.
        unsafe
        {
            _simBufferSizeInBytes = SimStateLayoutArr.Length * sizeof(double);
            _bodyBufferSizeInBytes = BodyStateLayoutArr.Length * sizeof(double) * _bodyCapacity;
        }

        // Allocate the unmanaged memory. Marshal.AllocHGlobal returns an IntPtr, which we store in our nint field.
        _simBufferPtr = Marshal.AllocHGlobal(_simBufferSizeInBytes);
        _bodyBufferPtr = Marshal.AllocHGlobal(_bodyBufferSizeInBytes);

        // Setup initial bodyBuffer pointers in shared memory
        unsafe
        {
            double* pSimState = (double*)_simBufferPtr;
            pSimState[SimStateLayout._bodyBufferPtr] = _bodyBufferPtr;
            pSimState[SimStateLayout._bodyBufferSize] = _bodyBufferSizeInBytes;
        }
        
        // Setup the staging buffer with the initial capacity
        _bodiesStagingBuffer = new double[_bodyCapacity * BodyStateLayoutArr.Length];
    }

    #region Data Writing

    internal void WriteViewToMemory(SimulationView view)
    {
        // Resize if needed and ensure that _bodyStateBufferPtr
        // and _bodyStateBufferSizeInBytes are up to date
        EnsureBodyCapacity(view.Bodies.Count);
        WriteFixedBuffer(view);
        WriteDynamicBuffer(view.Bodies);
    }

    private unsafe void WriteFixedBuffer(SimulationView view)
    {
        double* pSimState = (double*)_simBufferPtr;

        pSimState[SimStateLayout._bodyBufferPtr] = _bodyBufferPtr;
        pSimState[SimStateLayout._bodyBufferSize] = _bodyBufferSizeInBytes;

        pSimState[SimStateLayout.simulationTime] = view.SimulationTime;
        pSimState[SimStateLayout.timeStep] = view.TimeStep;
        pSimState[SimStateLayout.bodyCount] = view.Bodies.Count;
        pSimState[SimStateLayout.theta] = view.Theta;
        pSimState[SimStateLayout.gravitationalConstant] = view.G_SI;
        pSimState[SimStateLayout.epsilon] = view.Epsilon;
    }

    public unsafe void WriteDynamicBuffer(IReadOnlyList<BodyView> bodies)
    {
        int bodyCount = bodies.Count;
        if (bodyCount == 0) return;

        int bodyStride = BodyStateLayoutArr.Length;
        int totalDoubles = bodyCount * bodyStride;

        // Span to represent only the used portion of the reusable array.
        var allBodiesData = new Span<double>(_bodiesStagingBuffer, 0, totalDoubles);

        for (int i = 0; i < bodyCount; i++)
        {
            BodyView body = bodies[i];
            var bodySlice = allBodiesData.Slice(i * bodyStride, bodyStride);

            bodySlice[BodyStateLayout.id] = body.Id;
            bodySlice[BodyStateLayout.enabled] = Convert.ToDouble(body.Enabled);
            bodySlice[BodyStateLayout.mass] = body.Mass;
            bodySlice[BodyStateLayout.posX] = body.Position.X;
            bodySlice[BodyStateLayout.posY] = body.Position.Y;
            bodySlice[BodyStateLayout.velX] = body.Velocity.X;
            bodySlice[BodyStateLayout.velY] = body.Velocity.Y;
            bodySlice[BodyStateLayout.accX] = body.Acceleration.X;
            bodySlice[BodyStateLayout.accY] = body.Acceleration.Y;
            bodySlice[BodyStateLayout.outOfBounds] = Convert.ToDouble(body.OutOfBounds);
        }

        fixed (double* pSource = allBodiesData)
        {
            Unsafe.CopyBlock((double*)_bodyBufferPtr, pSource, (uint)(totalDoubles * sizeof(double)));
        }
    }

    internal void EnsureBodyCapacity(int requiredBodyCount)
    {
        if (requiredBodyCount <= _bodyCapacity) return;

        int newCapacity = _bodyCapacity;
        while (newCapacity < requiredBodyCount) { newCapacity *= 2; }
        _bodyCapacity = newCapacity;

        int bodyStride = BodyStateLayoutArr.Length;
        int newSizeInBytes;
        unsafe { newSizeInBytes = bodyStride * sizeof(double) * _bodyCapacity; }

        // Reallocate the unmanaged memory block.
        _bodyBufferPtr = Marshal.ReAllocHGlobal(_bodyBufferPtr, newSizeInBytes);
        _bodyBufferSizeInBytes = newSizeInBytes;

        // Resize the staging buffer to match capacity.
        _bodiesStagingBuffer = new double[_bodyCapacity * bodyStride];
    }

    #endregion


    #region IDisposable implementation

    private bool _disposed = false;

    // This is the public Dispose method, called by consumers of the class.
    public void Dispose()
    {
        Dispose(true);
        // Tell the garbage collector that it doesn't need to call the finalizer,
        // because we've already cleaned up.
        GC.SuppressFinalize(this);
    }

    // This is the protected virtual method that does the actual work of cleaning up.
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Clean up managed resources here, if any.
            }

            // Clean up unmanaged resources.
            if (_simBufferPtr != nint.Zero)
            {
                Marshal.FreeHGlobal(_simBufferPtr);
                _simBufferPtr = nint.Zero;
            }

            if (_bodyBufferPtr != nint.Zero)
            {
                Marshal.FreeHGlobal(_bodyBufferPtr);
                _bodyBufferPtr = nint.Zero;
            }

            _disposed = true;
        }
    }

    // This is the finalizer (destructor). It's a fallback mechanism.
    // It will be called by the garbage collector if Dispose() was never called.
    ~MemoryBufferHandler()
    {
        Dispose(false);
    }

    #endregion
}
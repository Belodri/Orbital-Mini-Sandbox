using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using Physics;

namespace Bridge;


#region Layout Definitions

// Defines the memory layout structures as strongly-typed records. These records are the
// Single Source of Truth for the entire shared memory layout.
//
// - Property names MUST be written in camelCase! as they define the names of the properties in Javascript
// - All properties MUST be of type 'int'! The system relies on this, as the properties will hold
//       the integer index/offset for each field within the shared memory array.

#pragma warning disable IDE1006 // Naming Styles
internal record SimStateLayoutRec(int bodyBufferPtr, int bodyBufferSize, int simulationTime, int timeScale, int timeIsForward, int bodyCount);
internal record BodyStateLayoutRec(int id, int enabled, int mass, int posX, int posY, int velX, int velY);
#pragma warning restore IDE1006 // Naming Styles

#endregion

internal class MemoryBufferHandler : IDisposable
{
    // Pointers
    private nint _simStateBufferPtr;
    private nint _bodyStateBufferPtr;
    // Public Accessors
    public nint SimStateBufferPtr => _simStateBufferPtr;
    public nint BodyStateBufferPtr => _bodyStateBufferPtr;

    // Sizes in bytes
    private readonly int _simStateBufferSizeInBytes;
    private int _bodyStateBufferSizeInBytes;
    // Public Accessors
    public int SimStateBufferSizeInBytes => _simStateBufferSizeInBytes;
    public int BodyStateBufferSizeInBytes => _bodyStateBufferSizeInBytes;

    // Capacity in number of bodies
    private int _bodyCapacity;

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
    //      where each property holds its own integer index (e.g., `SimStateLayout.bodyBufferPtr` will be 0).
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
            _simStateBufferSizeInBytes = SimStateLayoutArr.Length * sizeof(double);
            _bodyStateBufferSizeInBytes = BodyStateLayoutArr.Length * sizeof(double) * _bodyCapacity;
        }

        // Allocate the unmanaged memory. Marshal.AllocHGlobal returns an IntPtr, which we store in our nint field.
        _simStateBufferPtr = Marshal.AllocHGlobal(_simStateBufferSizeInBytes);
        _bodyStateBufferPtr = Marshal.AllocHGlobal(_bodyStateBufferSizeInBytes);

        // Setup initial bodyBuffer pointers in shared memory
        unsafe
        {
            double* pSimState = (double*)_simStateBufferPtr;
            pSimState[SimStateLayout.bodyBufferPtr] = (double)_bodyStateBufferPtr;
            pSimState[SimStateLayout.bodyBufferSize] = _bodyStateBufferSizeInBytes;
        }
        
    }

    #region Data Writing

    internal void WriteTickData(TickDataDto tickData)
    {
        // Resize if needed and ensure that _bodyStateBufferPtr
        // and _bodyStateBufferSizeInBytes are up to date
        EnsureBodyCapacity(tickData.BodiesStateData.Length);

        WriteSimState(tickData);
        WriteBodyState(tickData);
    }

    private unsafe void WriteSimState(TickDataDto tickData)
    {
        double* pSimState = (double*)_simStateBufferPtr;

        pSimState[SimStateLayout.bodyBufferPtr] = (double)_bodyStateBufferPtr;
        pSimState[SimStateLayout.bodyBufferSize] = _bodyStateBufferSizeInBytes;
        pSimState[SimStateLayout.simulationTime] = tickData.SimStateData.SimulationTime;
        pSimState[SimStateLayout.timeScale] = tickData.SimStateData.TimeScale;
        pSimState[SimStateLayout.timeIsForward] = Convert.ToDouble(tickData.SimStateData.IsTimeForward);
        pSimState[SimStateLayout.bodyCount] = tickData.BodiesStateData.Length;
    }

    private unsafe void WriteBodyState(TickDataDto tickData)
    {
        double* pBodyState = (double*)_bodyStateBufferPtr;
        int bodyStride = BodyStateLayoutArr.Length;

        for (int i = 0; i < tickData.BodiesStateData.Length; i++)
        {
            BodyStateData body = tickData.BodiesStateData[i];
            double* pBody = pBodyState + (i * bodyStride);

            pBody[BodyStateLayout.id] = body.Id;
            pBody[BodyStateLayout.enabled] = Convert.ToDouble(body.Enabled);
            pBody[BodyStateLayout.mass] = body.Mass;
            pBody[BodyStateLayout.posX] = body.PosX;
            pBody[BodyStateLayout.posY] = body.PosY;
            pBody[BodyStateLayout.velX] = body.VelX;
            pBody[BodyStateLayout.velY] = body.VelY;
        }
    }

    internal void EnsureBodyCapacity(int requiredBodyCount)
    {
        if (requiredBodyCount <= _bodyCapacity) return;

        int newCapacity = _bodyCapacity;
        while (newCapacity < requiredBodyCount) { newCapacity *= 2; }
        _bodyCapacity = newCapacity;

        int newSizeInBytes;
        unsafe { newSizeInBytes = BodyStateLayoutArr.Length * sizeof(double) * _bodyCapacity; }

        // Reallocate the unmanaged memory block.
        _bodyStateBufferPtr = Marshal.ReAllocHGlobal(_bodyStateBufferPtr, (nint)newSizeInBytes);
        _bodyStateBufferSizeInBytes = newSizeInBytes;
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
                // We don't have any in this class.
            }

            // Clean up unmanaged resources.
            // This is CRUCIAL. We must free the memory we allocated.
            if (_simStateBufferPtr != nint.Zero)
            {
                Marshal.FreeHGlobal(_simStateBufferPtr);
                _simStateBufferPtr = nint.Zero;
            }

            if (_bodyStateBufferPtr != nint.Zero)
            {
                Marshal.FreeHGlobal(_bodyStateBufferPtr);
                _bodyStateBufferPtr = nint.Zero;
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
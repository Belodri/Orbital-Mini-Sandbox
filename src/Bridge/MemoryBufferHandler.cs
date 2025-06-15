using System.Runtime.InteropServices;

namespace Bridge;

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

    // Layout for simulation metadata (fixed-size Float64 buffer).
    public static readonly string[] SimStateLayout = [
        "bodyBufferPtr",    // Pointer to the BodyStateBuffer
        "bodyBufferSize",   // The size of the buffer
        "tickError", "simulationTime", "timeScale", "timeIsForward", "bodyCount"
    ];

    // Layout for individual body state (dynamic Float64 buffer).
    public static readonly string[] BodyStateLayout = [
        "id", "enabled", "mass", "posX", "posY", "velX", "velY"
    ];

    private static readonly Dictionary<string, int> SimLayoutCache = CreateLayoutCache(SimStateLayout);
    private static readonly Dictionary<string, int> BodyLayoutCache = CreateLayoutCache(BodyStateLayout);

    static Dictionary<string, int> CreateLayoutCache(string[] layout)
    {
        var dict = new Dictionary<string, int>();
        for (int i = 0; i < layout.Length; i++)
        {
            dict[layout[i]] = i;
        }
        return dict;
    }


    public MemoryBufferHandler(int initialBodyCapacity = 100)
    {
        if (initialBodyCapacity <= 0)
            throw new ArgumentException("Initial body capacity must be positive", nameof(initialBodyCapacity));

        _bodyCapacity = initialBodyCapacity;

        // Calculate the required size for the buffers.
        unsafe
        {
            _simStateBufferSizeInBytes = SimStateLayout.Length * sizeof(double);
            _bodyStateBufferSizeInBytes = BodyStateLayout.Length * sizeof(double) * _bodyCapacity;
        }

        // Allocate the unmanaged memory. Marshal.AllocHGlobal returns an IntPtr, which we store in our nint field.
        _simStateBufferPtr = Marshal.AllocHGlobal(_simStateBufferSizeInBytes);
        _bodyStateBufferPtr = Marshal.AllocHGlobal(_bodyStateBufferSizeInBytes);

        UpdateSimStateBodyBufferPtr();
    }

    private unsafe void UpdateSimStateBodyBufferPtr()
    {
        double* pBuffer = (double*)_simStateBufferPtr;
        // Cast the nint pointer to a double. This is safe on wasm32.
        pBuffer[SimLayoutCache["bodyBufferPtr"]] = (double)_bodyStateBufferPtr;
        pBuffer[SimLayoutCache["bodyBufferSize"]] = (double)_bodyStateBufferSizeInBytes;
    }

    public void EnsureBodyCapacity(int requiredBodyCount)
    {
        if (requiredBodyCount <= _bodyCapacity) return;

        int newCapacity = _bodyCapacity;
        while (newCapacity < requiredBodyCount) { newCapacity *= 2; }
        _bodyCapacity = newCapacity;

        int newSizeInBytes;
        unsafe { newSizeInBytes = BodyStateLayout.Length * sizeof(double) * _bodyCapacity; }

        // Reallocate the unmanaged memory block.
        _bodyStateBufferPtr = Marshal.ReAllocHGlobal(_bodyStateBufferPtr, (nint)newSizeInBytes);
        _bodyStateBufferSizeInBytes = newSizeInBytes;

        UpdateSimStateBodyBufferPtr();
    }

    internal unsafe void WriteToBodyStateBuffer(double[][] bodyBufferData)
    {
        EnsureBodyCapacity(bodyBufferData.Length);

        double* bodyState = (double*)_bodyStateBufferPtr;
        int bodyStride = BodyStateLayout.Length;

        for (int i = 0; i < bodyBufferData.Length; i++)
        {
            double[] currentBodyData = bodyBufferData[i];
            double* currentBody = bodyState + (i * bodyStride);

            for (int j = 0; j < currentBodyData.Length; j++)
            {
                currentBody[j] = currentBodyData[j];
            }
        }
    }

    internal unsafe void WriteToSimStateBuffer(double[] simBufferData)
    {
        double* simState = (double*)_simStateBufferPtr;

        for (int i = 0; i < simBufferData.Length; i++)
        {
            simState[i] = simBufferData[i];
        }
    }


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
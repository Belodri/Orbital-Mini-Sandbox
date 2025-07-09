using Physics;

namespace Bridge;

internal class CommandQueue
{
    const int IniticalCapacity = 32;
    readonly Queue<Action<PhysicsEngine>> _queue = new(IniticalCapacity);
    readonly Queue<Action> _pendingResolutions = new(IniticalCapacity);

    /// <summary>
    /// Enqueues a command that returns a value.
    /// </summary>
    /// <typeparam name="T">The return type of the task.</typeparam>
    /// <param name="taskFunc">A function that takes a PhysicsEngine and returns a value of type T.</param>
    /// <returns>
    /// A Task that will be completed when the <see cref="ResolveProcessed"/> is called after the command has been executed during the next tick.
    /// </returns>
    internal Task<T> EnqueueTask<T>(Func<PhysicsEngine, T> taskFunc)
    {
        var tcs = new TaskCompletionSource<T>();
        _queue.Enqueue(engine =>
        {
            try
            {
                T result = taskFunc(engine);
                _pendingResolutions.Enqueue(() => tcs.TrySetResult(result));
            }
            catch (Exception ex)
            {
                _pendingResolutions.Enqueue(() => tcs.TrySetException(ex));
            }
        });

        return tcs.Task;
    }

    /// <summary>
    /// Processes all queued Tasks.
    /// </summary>
    /// <param name="engine">The PhysicsEngine instance passed as an argument to the queued Tasks.</param>
    internal void ProcessAll(PhysicsEngine engine)
    {
        _pendingResolutions.Clear();
        while (_queue.TryDequeue(out var action)) action(engine);
    }

    /// <summary>
    /// Executes all pending resolution actions, completing the Tasks from the last <see cref="ProcessAll"/> batch.
    /// </summary>
    internal void ResolveProcessed()
    {
        while(_pendingResolutions.TryDequeue(out var resAction)) resAction();
    }

    /// <summary>
    /// Clears both queued tasks and pending resolutions.
    /// </summary>
    internal void ClearQueue()
    {
        _queue.Clear();
        _pendingResolutions.Clear();
    }
}

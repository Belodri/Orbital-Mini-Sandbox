using Physics;

namespace Bridge;

internal class CommandQueue
{
    readonly Queue<Action<PhysicsEngine>> _queue = new();

    /// <summary>
    /// Enqueues a command that does not return a value.
    /// </summary>
    internal void Enqueue(Action<PhysicsEngine> commandAction)
    {
        _queue.Enqueue(commandAction);
    }

    /// <summary>
    /// Enqueues a command that returns a value. This method abstracts away the
    /// creation of a TaskCompletionSource and the associated try/catch block.
    /// </summary>
    /// <typeparam name="T">The return type of the task.</typeparam>
    /// <param name="taskFunc">A function that takes a PhysicsEngine and returns a value of type T.</param>
    /// <returns>A Task that will be completed when the command is executed.</returns>
    internal Task<T> EnqueueTask<T>(Func<PhysicsEngine, T> taskFunc)
    {
        var tcs = new TaskCompletionSource<T>();
        Enqueue(engine =>
        {
            try
            {
                T result = taskFunc(engine);
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        return tcs.Task;
    }

    internal void ProcessAll(PhysicsEngine engine)
    {
        List<Action<PhysicsEngine>> toProcess = [];
        while (_queue.Count > 0) toProcess.Add(_queue.Dequeue());

        foreach (var action in toProcess) action(engine);
    }

    internal void ClearQueue() => _queue.Clear();
}

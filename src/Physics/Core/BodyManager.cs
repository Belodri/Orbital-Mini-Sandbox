using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Physics.Bodies;

namespace Physics.Core;

internal interface IBodyManager
{
    /// <summary>
    /// Event raised after a body has been added to from the manager.
    /// </summary>
    event Action<ICelestialBody>? BodyAdded;
    /// <summary>
    /// Event raised after a body has been removed from the manager.
    /// </summary>
    event Action<int>? BodyRemoved;
    /// <summary>
    /// Event raised after a body has been enabled, disabled, or an already-enabled body has been updated.
    /// </summary>
    event Action? EnabledContentModified;
    /// <summary>
    /// The number of all managed bodies, both enabled and disabled.
    /// </summary>
    int BodyCount { get; }
    /// <summary>
    /// The number of all currently enabled bodies.
    /// </summary>
    int EnabledCount { get; }
    /// <summary>
    /// A read-only collection of bodies with `Enabled = true` that can be accessed by index.
    /// </summary>
    IReadOnlyList<ICelestialBody> EnabledBodies { get; }
    /// <summary>
    /// A readonly dictionary of all bodies managed by this class, both enabled and disabled.
    /// </summary>
    IReadOnlyDictionary<int, ICelestialBody> AllBodies { get; }
    /// <summary>
    /// Creates a new celestial body using a factory function, assigns it a unique ID, and adds it to the manager.
    /// <example><code>
    /// ICelestialBody myPlanet = bodyManager.CreateBody(id => new Planet(id));
    /// </code></example>
    /// </summary>
    /// <param name="bodyFactory">
    /// A factory function that takes a unique integer ID as input and returns a new instance of an <see cref="ICelestialBody"/> implementation.
    /// The function is responsible for constructing the body and assigning the provided ID.
    /// </param>
    /// <returns>The newly created and added celestial body instance.</returns>
    ICelestialBody CreateBody(Func<int, ICelestialBody> bodyFactory);
    /// <summary>
    /// Try to add an existing body to the manager.
    /// </summary>
    /// <param name="body">The body to add.</param>
    /// <returns><c>true</c> if the body was added, <c>false</c> otherwise, meaning the manager already has a body with the same ID.</returns>
    bool TryAddBody(ICelestialBody body);
    /// <summary>
    /// Try to update a celestial body in the manager.
    /// </summary>
    /// <param name="id">The ID of the body to update.</param>
    /// <param name="updates">Partial data to update the body.</param>
    /// <returns><c>true</c> if the update was successful, <c>false</c> if the body wasn't found.</returns>
    /// <remarks>
    /// WARNING: ALL external updates to bodies must be done through this method! <br/>
    /// Internal, direct body updates should be handled with care as the manager is not aware of such changes!
    /// </remarks>
    bool TryUpdateBody(int id, BodyDataUpdates updates);
    /// <summary>
    /// Removes a celestial body from the manager using its ID. Does nothing if no body with that ID is found.
    /// </summary>
    /// <param name="id">The ID of the body to remove.</param>
    /// <returns><c>true</c> if a body with the specified ID was found and removed; otherwise <c>false</c>.</returns>
    bool TryDeleteBody(int id);
    /// <summary>
    /// Tests if a body with a given ID is managed by this manager.
    /// </summary>
    /// <param name="id">The ID of the body to check.</param>
    /// <returns><c>true</c> if a body with the specified ID was found; otherwise <c>false</c>.</returns>
    bool HasBody(int id);
    /// <summary>
    /// Attempt get a body with a given ID.
    /// </summary>
    /// <param name="id">The ID of the body to get.</param>
    /// <param name="body">The body if it was found, null otherwise.</param>
    /// <returns><c>true</c> if a body with the specified ID was found; otherwise <c>false</c>.</returns>
    bool TryGetBody(int id, [MaybeNullWhen(false)] out ICelestialBody body);
    /// <summary>
    /// Attempt to get a body with a given ID.
    /// </summary>
    /// <param name="id">The ID of the body to get.</param>
    /// <returns>The body if it was found, otherwise null.</returns>
    ICelestialBody? GetBodyOrNull(int id);
}

internal sealed class BodyManager : IBodyManager
{
    private const int DisabledIdx = -1;

    public BodyManager(int initialCapacity = 256)
    {
        _store = new(initialCapacity);
        _enabled = new(initialCapacity);
        _allBodies = new(_store);
    }

    private int _nextBodyId = 0;
    private readonly Dictionary<int, BodyEntry> _store;
    private readonly List<ICelestialBody> _enabled;
    private readonly BodyReadonlyDictionary _allBodies;

    #region Public Accessors & Methods

    public IReadOnlyList<ICelestialBody> EnabledBodies => _enabled;
    public IReadOnlyDictionary<int, ICelestialBody> AllBodies => _allBodies;

    public int BodyCount => _store.Count;
    public int EnabledCount => _enabled.Count;

    public event Action<ICelestialBody>? BodyAdded;
    public event Action<int>? BodyRemoved;
    public event Action? EnabledContentModified;

    public bool TryAddBody(ICelestialBody body)
    {
        if (_store.ContainsKey(body.Id)) return false;
        AddBody(body);
        return true;
    }

    public bool TryDeleteBody(int id)
    {
        ref var entry = ref GetEntryRef(id);
        if (Unsafe.IsNullRef(ref entry)) return false;
        RemoveBody(ref entry);
        return true;
    }

    public bool TryUpdateBody(int id, BodyDataUpdates updates)
    {
        ref var entry = ref GetEntryRef(id);
        if (Unsafe.IsNullRef(ref entry)) return false;
        UpdateBody(ref entry, updates);
        return true;
    }

    public ICelestialBody CreateBody(Func<int, ICelestialBody> bodyFactory)
    {
        // No need to recycle IDs here
        while (_store.ContainsKey(_nextBodyId)) _nextBodyId++;
        int id = _nextBodyId;
        _nextBodyId++;

        var body = bodyFactory(id);
#if DEBUG
        if (body.Id != id) throw new InvalidOperationException($"{nameof(bodyFactory)} failed to create body with the given ID.");
#endif
        AddBody(body);
        return body;
    }

    public bool HasBody(int id) => _store.ContainsKey(id);

    public bool TryGetBody(int id, [MaybeNullWhen(false)] out ICelestialBody body)
    {
        body = null;
        if (_store.TryGetValue(id, out var entry))
        {
            body = entry.Body;
            return true;
        }
        return false;
    }

    public ICelestialBody? GetBodyOrNull(int id)
    {
        if (TryGetBody(id, out var body)) return body;
        return null;
    }

    #endregion


    #region Helpers

    private ref BodyEntry GetEntryRef(int id) => ref CollectionsMarshal.GetValueRefOrNullRef(_store, id);

    private void AddEnabled(ref BodyEntry entry)
    {
        entry.EnabledIdx = _enabled.Count;
        _enabled.Add(entry.Body);
    }

    private void RemoveEnabled(ref BodyEntry entry)
    {
        // Get last enabled body
        var lastEnabled = _enabled[^1];
        // Move it to the position of the one being removed
        _enabled[entry.EnabledIdx] = lastEnabled;
        // Update the swapped body's entry
        ref var swappedEntry = ref GetEntryRef(lastEnabled.Id);
        swappedEntry.EnabledIdx = entry.EnabledIdx;
        // Remove the last element as it's a duplicate now
        _enabled.RemoveAt(_enabled.Count - 1);
        // Update the index of the removed body's entry
        entry.EnabledIdx = DisabledIdx;
    }

    #endregion


    #region Private Workers

    private void AddBody(ICelestialBody body)
    {
        var enabledIdx = DisabledIdx;
        _store.Add(body.Id, new(body, enabledIdx));
        if (body.Enabled) AddEnabled(ref GetEntryRef(body.Id));
        BodyAdded?.Invoke(body);
        if (body.Enabled) EnabledContentModified?.Invoke();
    }

    private void RemoveBody(ref BodyEntry entry)
    {
        var bodyId = entry.Body.Id;
        var wasEnabled = entry.EnabledIdx != DisabledIdx;
        if (wasEnabled) RemoveEnabled(ref entry);
        _store.Remove(bodyId);
        BodyRemoved?.Invoke(bodyId);
        if (wasEnabled) EnabledContentModified?.Invoke();
    }

    private void UpdateBody(ref BodyEntry entry, BodyDataUpdates updates)
    {
        bool wasEnabled = entry.Body.Enabled;
        entry.Body.Update(updates);
        bool isEnabled = entry.Body.Enabled;

        if (wasEnabled != isEnabled)
        {
            if (isEnabled) AddEnabled(ref entry);
            else RemoveEnabled(ref entry);
        }

        // Raise the event if the body was or is enabled.
        if (isEnabled || wasEnabled) EnabledContentModified?.Invoke();
    }

    #endregion


    #region Structs and Classes

    private struct BodyEntry(ICelestialBody body, int enabledIdx = DisabledIdx)
    {
        public readonly ICelestialBody Body = body;
        public int EnabledIdx { get; set; } = enabledIdx;
    }

    private sealed class BodyReadonlyDictionary(Dictionary<int, BodyEntry> source) : IReadOnlyDictionary<int, ICelestialBody>
    {
        private readonly Dictionary<int, BodyEntry> _source = source;
        public ICelestialBody this[int key] => _source[key].Body;
        public IEnumerable<int> Keys => _source.Keys;
        public IEnumerable<ICelestialBody> Values => _source.Values.Select(entry => entry.Body);
        public int Count => _source.Count;
        public bool ContainsKey(int key) => _source.ContainsKey(key);
        public bool TryGetValue(int key, [MaybeNullWhen(false)] out ICelestialBody value)
        {
            if (_source.TryGetValue(key, out var entry))
            {
                value = entry.Body;
                return true;
            }
            value = null;
            return false;
        }

        public Enumerator GetEnumerator() => new(_source.GetEnumerator());
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(_source.GetEnumerator());
        // Explicit interface implementation for IEnumerable<T>. This WILL box and allocate.
        IEnumerator<KeyValuePair<int, ICelestialBody>> IEnumerable<KeyValuePair<int, ICelestialBody>>.GetEnumerator() => GetEnumerator();
        public struct Enumerator(Dictionary<int, BodyEntry>.Enumerator sourceEnumerator) : IEnumerator<KeyValuePair<int, ICelestialBody>>
        {
            private Dictionary<int, BodyEntry>.Enumerator _sourceEnumerator = sourceEnumerator;
            public readonly KeyValuePair<int, ICelestialBody> Current => new(_sourceEnumerator.Current.Key, _sourceEnumerator.Current.Value.Body);
            readonly object IEnumerator.Current => Current;
            public void Dispose() => _sourceEnumerator.Dispose();
            public bool MoveNext() => _sourceEnumerator.MoveNext();
            public readonly void Reset() => ((IEnumerator)_sourceEnumerator).Reset();
        }
    }
    
    #endregion
}
namespace NativeUI.Graphics;

/// <summary>Owns backend resources retained across paint passes.</summary>
public sealed class DrawingResourceCache : IDisposable
{
    private readonly Dictionary<object, IDisposable> _resources = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<object> _used = new(ReferenceEqualityComparer.Instance);

    /// <summary>Starts tracking resources used by a paint pass.</summary>
    public void BeginPass() => _used.Clear();

    /// <summary>Gets or constructs a resource for an immutable definition.</summary>
    public T GetOrCreate<T>(object definition, Func<T> create) where T : class, IDisposable
    {
        _used.Add(definition);
        if (_resources.TryGetValue(definition, out IDisposable? resource)) return (T)resource;
        T result = create();
        _resources.Add(definition, result);
        return result;
    }

    /// <summary>Releases resources no longer used by the latest paint pass.</summary>
    public void EndPass()
    {
        foreach (object key in _resources.Keys.Where(key => !_used.Contains(key)).ToArray())
        {
            _resources[key].Dispose();
            _resources.Remove(key);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (IDisposable resource in _resources.Values) resource.Dispose();
        _resources.Clear();
        _used.Clear();
    }
}

using System.Reflection;

namespace NativeUI.Core;

/// <summary>
/// Represents a property-path binding used when the compile-time <c>x:DataType</c> is not specified on a view.
/// </summary>
public sealed class PathBinding
{
    /// <summary>
    /// Gets the property path observed on the source <see cref="BindableObject.DataContext"/>.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the binding direction mode.
    /// </summary>
    public BindingMode Mode { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PathBinding"/> class.
    /// </summary>
    public PathBinding(string path, BindingMode mode = BindingMode.OneWay)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Path = path;
        Mode = mode;
    }

    /// <summary>
    /// Attaches a <see cref="PathBinding"/> to the specified <paramref name="target"/> and <paramref name="property"/>.
    /// </summary>
    public static IDisposable Bind<TValue>(
        BindableObject target,
        BindableProperty<TValue> property,
        string path,
        BindingMode mode = BindingMode.OneWay)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(property);

        return target.SetBinding(
            property,
            new CompiledBinding<object, TValue>(
                path,
                source => ReadProperty(source, path, property.DefaultValue),
                mode == BindingMode.TwoWay ? (source, val) => WriteProperty(source, path, val) : null,
                mode));
    }

    private static TValue ReadProperty<TValue>(object source, string propertyName, TValue fallback)
    {
        PropertyInfo? prop = source.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (prop is null || !prop.CanRead)
        {
            return fallback;
        }

        object? raw = prop.GetValue(source);
        if (raw is TValue typed)
        {
            return typed;
        }

        if (typeof(TValue) == typeof(string) && raw is not null)
        {
            return (TValue)(object)(raw.ToString() ?? string.Empty);
        }

        return fallback;
    }

    private static void WriteProperty<TValue>(object source, string propertyName, TValue value)
    {
        PropertyInfo? prop = source.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (prop is not null && prop.CanWrite)
        {
            prop.SetValue(source, value);
        }
    }
}

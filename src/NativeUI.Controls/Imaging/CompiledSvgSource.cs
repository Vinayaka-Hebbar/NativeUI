using NativeUI.Media;

namespace NativeUI.Controls;

/// <summary>A reusable factory producing a fresh SVG visual instance for each Image attachment.</summary>
public sealed record CompiledSvgSource : ImageSource
{
    /// <summary>Creates a source from a generated or authored SVG factory.</summary>
    public CompiledSvgSource(Func<Svg> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        Factory = factory;
    }
    /// <summary>Gets the factory; painting never invokes it.</summary>
    public Func<Svg> Factory { get; }
}

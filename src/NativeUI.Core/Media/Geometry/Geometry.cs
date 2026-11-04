
namespace NativeUI.Media;

/// <summary>Immutable vector geometry independent of a graphics backend.</summary>
public abstract class Geometry
{
    /// <summary>Parses SVG path data for dynamic content.</summary>
    /// <exception cref="FormatException">The path data is invalid or exceeds supported limits.</exception>
    public static PathGeometry Parse(string data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return PathGeometry.FromCommands(PathDataParser.Parse(data));
    }
}

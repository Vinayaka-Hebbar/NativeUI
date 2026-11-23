using NativeUI.Media;

namespace NativeUI.Sample;

/// <summary>
/// Represents a navigation item in the gallery shell.
/// </summary>
public class NavigationItemViewModel
{
    /// <summary>
    /// Gets or sets the display name of the navigation item.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the identifier or tag for navigation routing.
    /// </summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the geometry drawn as the item's icon.
    /// </summary>
    public Geometry? IconData { get; set; }

    /// <inheritdoc />
    public override string ToString() => Name;
}

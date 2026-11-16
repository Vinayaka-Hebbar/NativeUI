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
    /// Gets or sets the SVG path geometry data string for the icon.
    /// </summary>
    public string IconData { get; set; } = string.Empty;

    /// <inheritdoc />
    public override string ToString() => Name;
}

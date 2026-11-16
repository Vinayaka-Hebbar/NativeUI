using NativeUI.Core;
using NativeUI.Layout;

namespace NativeUI.Controls;

/// <summary>
/// Represents a visual horizontal or vertical divider between navigation items in a <see cref="NavigationView"/>.
/// </summary>
public class NavigationViewItemSeparator : NavigationViewItemBase
{
    /// <summary>
    /// Identifies the <see cref="Orientation"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Orientation> OrientationProperty =
        BindableProperty.Create<NavigationViewItemSeparator, Orientation>(
            nameof(Orientation),
            defaultValue: Orientation.Horizontal,
            propertyChanged: static (owner, _, _) => owner.InvalidateStyle());

    /// <inheritdoc />
    public override string TagName => "navigationviewitemseparator";

    /// <summary>
    /// Gets or sets the direction of the divider line. Defaults to <see cref="Orientation.Horizontal"/>.
    /// </summary>
    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <inheritdoc />
    public override string? GetAttribute(string name)
    {
        if (string.Equals(name, "data-orientation", StringComparison.Ordinal))
        {
            return Orientation == Orientation.Vertical ? "vertical" : "horizontal";
        }

        return base.GetAttribute(name);
    }
}

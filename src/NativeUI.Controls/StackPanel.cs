using NativeUI.Core;
using NativeUI.Layout;

namespace NativeUI.Controls;

/// <summary>
/// Represents a view that arranges its children in a vertical or horizontal line with fixed spacing.
/// </summary>
public class StackPanel : View, IStackLayoutNode
{
    /// <summary>
    /// Identifies the <see cref="Orientation"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Orientation> OrientationProperty =
        BindableProperty.Create<StackPanel, Orientation>(
            nameof(Orientation),
            defaultValue: Orientation.Vertical,
            propertyChanged: static (owner, _, _) => owner.InvalidateVisual());

    /// <summary>
    /// Identifies the <see cref="Spacing"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> SpacingProperty =
        BindableProperty.Create<StackPanel, double>(
            nameof(Spacing),
            defaultValue: 0,
            propertyChanged: static (owner, _, _) => owner.InvalidateVisual());

    /// <summary>
    /// Gets or sets the direction in which children are arranged. Defaults to <see cref="Orientation.Vertical"/>.
    /// </summary>
    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>
    /// Gets or sets the logical pixels between adjacent children, in addition to their margins.
    /// </summary>
    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }
}

using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;

namespace NativeUI.Controls;

/// <summary>
/// Represents a row or column of related buttons. The CSS <c>gap</c> property sets the spacing between the buttons.
/// Descendant selectors such as <c>buttongroup button:first-child</c> style the buttons at either end, and
/// <c>[data-orientation]</c> selects the direction.
/// </summary>
public class ButtonGroup : ItemsControl, IStackLayoutNode
{
    static ButtonGroup()
    {
        WidthProperty.OverrideMetadata<ButtonGroup>(WidthValue.FitContent);
    }

    /// <summary>
    /// Identifies the <see cref="Orientation"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Orientation> OrientationProperty =
        BindableProperty.Create<ButtonGroup, Orientation>(
            nameof(Orientation),
            defaultValue: Orientation.Horizontal,
            propertyChanged: static (owner, _, _) => owner.OnOrientationChanged());

    /// <inheritdoc />
    public override string TagName => "buttongroup";

    /// <summary>
    /// Gets or sets the direction in which the buttons are arranged. Defaults to <see cref="Orientation.Horizontal"/>.
    /// </summary>
    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    Orientation IStackLayoutNode.Orientation => Orientation;

    double IStackLayoutNode.Spacing => 0;

    Alignment IStackLayoutNode.HorizontalAlignment => Alignment.Start;

    /// <summary>
    /// Returns <c>horizontal</c> or <c>vertical</c> for <c>data-orientation</c>.
    /// </summary>
    /// <param name="name">The attribute name.</param>
    public override string? GetAttribute(string name)
    {
        if (string.Equals(name, "data-orientation", StringComparison.Ordinal))
        {
            return Orientation == Orientation.Horizontal ? "horizontal" : "vertical";
        }

        return base.GetAttribute(name);
    }

    private void OnOrientationChanged()
    {
        InvalidateStyle();
        InvalidateVisual();
    }
}

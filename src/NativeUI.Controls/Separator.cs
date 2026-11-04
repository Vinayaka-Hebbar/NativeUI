using NativeUI.Core;
using NativeUI.Layout;

namespace NativeUI.Controls;

/// <summary>
/// Represents a line that separates content. The separator has <c>data-orientation</c> set to <c>horizontal</c> or
/// <c>vertical</c>, which stylesheets use to give it a thickness and a color. It has no appearance until a stylesheet
/// gives it one. A vertical separator in a horizontal stack also needs a height. A stack does not stretch its children.
/// </summary>
public class Separator : UIElement
{
    /// <summary>
    /// Identifies the <see cref="Orientation"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Orientation> OrientationProperty =
        BindableProperty.Create<Separator, Orientation>(
            nameof(Orientation),
            defaultValue: Orientation.Horizontal,
            propertyChanged: static (owner, _, _) => owner.InvalidateStyle());

    /// <inheritdoc />
    public override string TagName => "separator";

    /// <summary>
    /// Gets or sets the direction of the line. Defaults to <see cref="Orientation.Horizontal"/>.
    /// </summary>
    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>
    /// Returns <c>horizontal</c> or <c>vertical</c> for <c>data-orientation</c>.
    /// </summary>
    /// <param name="name">The attribute name.</param>
    public override string? GetAttribute(string name)
    {
        if (string.Equals(name, "data-orientation", StringComparison.Ordinal))
        {
            return Orientation == Orientation.Vertical ? "vertical" : "horizontal";
        }

        return base.GetAttribute(name);
    }
}

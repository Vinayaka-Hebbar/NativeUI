using NativeUI.Core;

namespace NativeUI.Controls;

/// <summary>
/// Displays read-only or bound text content, rendered via SkiaSharp on desktop and as a native HTML
/// <c>&lt;span&gt;</c> element on Web.
/// </summary>
public class Text : UIElement
{
    /// <summary>
    /// Identifies the <see cref="Value"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<string> ValueProperty =
        BindableProperty.Create<Text, string>(
            nameof(Value),
            defaultValue: string.Empty,
            propertyChanged: static (owner, _, _) => owner.InvalidateVisual());

    /// <summary>
    /// Identifies the <see cref="AriaLabel"/> accessibility bindable property.
    /// </summary>
    public static readonly BindableProperty<string?> AriaLabelProperty =
        BindableProperty.Create<Text, string?>(
            nameof(AriaLabel),
            defaultValue: null);

    /// <inheritdoc />
    public override string StyleTypeName => "text";

    /// <summary>
    /// Gets or sets the text string displayed by this control.
    /// </summary>
    public string Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>
    /// Gets or sets the accessible label announced by assistive technologies.
    /// </summary>
    public string? AriaLabel
    {
        get => GetValue(AriaLabelProperty);
        set => SetValue(AriaLabelProperty, value);
    }

    /// <inheritdoc />
    protected override string? GetLayoutTextContent()
    {
        return Value;
    }
}

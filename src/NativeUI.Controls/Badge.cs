using NativeUI.Media;

namespace NativeUI.Controls;

/// <summary>
/// Represents a short label, such as a status or a count, as wide as its <see cref="ContentControl.Content"/>. Variants
/// such as <c>secondary</c> or <c>outline</c> are style classes. The badge has no appearance until a stylesheet gives it one.
/// </summary>
public class Badge : ContentControl
{
    static Badge()
    {
        WidthProperty.OverrideMetadata<Badge>(WidthValue.FitContent);
    }

    /// <inheritdoc />
    public override string TagName => "badge";
}

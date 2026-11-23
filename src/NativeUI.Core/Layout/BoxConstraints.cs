using NativeUI.Media;

namespace NativeUI.Layout;

/// <summary>
/// Describes the numeric box sizing inputs resolved before content measurement. The width is definite because text
/// and child layout require it; a null height leaves the height to the content. These inputs contain no style state.
/// </summary>
/// <param name="Width">The offered border-box width before applying the size limits.</param>
/// <param name="Height">The requested border-box height, or null for content sizing.</param>
/// <param name="Minimum">The resolved minimum border-box size.</param>
/// <param name="Maximum">The resolved maximum border-box size.</param>
/// <param name="Margin">The resolved outer spacing.</param>
internal readonly record struct BoxConstraints(double Width, double? Height, Size Minimum, Size Maximum, Thickness Margin)
{
    /// <summary>Constrains a border-box width, giving the minimum precedence over the maximum.</summary>
    public double ConstrainWidth(double width)
    {
        return Math.Max(Minimum.Width, Math.Min(width, Maximum.Width));
    }

    /// <summary>Constrains a border-box height, giving the minimum precedence over the maximum.</summary>
    public double ConstrainHeight(double height)
    {
        return Math.Max(Minimum.Height, Math.Min(height, Maximum.Height));
    }
}

namespace NativeUI.Media;

/// <summary>
/// Represents one shadow of the CSS <c>box-shadow</c> property, in logical pixels.
/// </summary>
/// <param name="OffsetX">The horizontal offset. A positive value moves the shadow right.</param>
/// <param name="OffsetY">The vertical offset. A positive value moves the shadow down.</param>
/// <param name="BlurRadius">The blur radius. Zero draws a sharp edge.</param>
/// <param name="SpreadRadius">The distance the shadow grows before blurring. A negative value shrinks it.</param>
/// <param name="Color">The shadow color, or <see langword="null"/> to use the element's text color.</param>
/// <param name="IsInset">Whether the shadow is drawn inside the padding box instead of outside the border box.</param>
public readonly record struct BoxShadow(
    double OffsetX,
    double OffsetY,
    double BlurRadius = 0,
    double SpreadRadius = 0,
    Color? Color = null,
    bool IsInset = false);

namespace NativeUI.Media;

/// <summary>A vector brush containing a single color.</summary>
/// <param name="Color">The brush color.</param>
public sealed record SolidColorBrush(Color Color) : Brush;

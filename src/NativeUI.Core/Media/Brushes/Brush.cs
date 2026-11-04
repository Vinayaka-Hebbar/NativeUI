namespace NativeUI.Media;

/// <summary>An immutable vector fill or stroke.</summary>
public abstract record Brush
{
    /// <summary>Creates a solid brush from a color.</summary>
    public static implicit operator Brush(Color color) => new SolidColorBrush(color);
}

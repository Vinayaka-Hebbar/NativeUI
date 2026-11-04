namespace NativeUI.Media;

/// <summary>A brush referencing a gradient in the containing SVG definition.</summary>
/// <param name="Id">The local definition identity without a fragment prefix.</param>
public sealed record SvgReferenceBrush(string Id) : Brush;

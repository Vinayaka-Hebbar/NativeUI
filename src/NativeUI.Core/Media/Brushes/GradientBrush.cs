using System.Collections.Immutable;
using NativeUI.Graphics;

namespace NativeUI.Media;

/// <summary>An immutable vector gradient.</summary>
public abstract record GradientBrush : Brush
{
    /// <summary>Gets color stops ordered by increasing offset.</summary>
    public ImmutableArray<GradientStop> Stops { get; init; } = [];
}

using System.Collections.Immutable;
using NativeUI.Media;

namespace NativeUI.Css;

/// <summary>Represents a compiled position-area value.</summary>
/// <param name="Area">The compiled value.</param>
public sealed record CssPositionAreaValue(PositionArea Area) : CssValue;

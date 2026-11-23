using System.Collections.Immutable;
using NativeUI.Media;

namespace NativeUI.Css;

/// <summary>Represents ordered CSS position fallback groups.</summary>
/// <param name="Fallbacks">The compiled value.</param>
public sealed record CssPositionTryFallbacksValue(ImmutableArray<PositionTryTactic> Fallbacks) : CssValue;

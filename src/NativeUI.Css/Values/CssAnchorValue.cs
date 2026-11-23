using System.Collections.Immutable;
using NativeUI.Media;

namespace NativeUI.Css;

/// <summary>Represents a compiled anchor inset.</summary>
/// <param name="Inset">The compiled value.</param>
public sealed record CssAnchorValue(InsetValue Inset) : CssValue;

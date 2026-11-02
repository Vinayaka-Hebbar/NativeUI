using System.Collections.Immutable;
using NativeUI.Media;
using NativeUI.Media.Animation;

namespace NativeUI.Css;

/// <summary>
/// Represents a compiled CSS <c>transform</c> value.
/// </summary>
/// <param name="Transform">The transform, or <see langword="null"/> for <c>none</c>.</param>
public sealed record CssTransformValue(StyleTransform? Transform) : CssValue;

/// <summary>
/// Represents a compiled CSS <c>transition</c> value.
/// </summary>
/// <param name="Transitions">The entries, which are empty for <c>none</c>.</param>
public sealed record CssTransitionValue(ImmutableArray<TransitionSpec> Transitions) : CssValue;

/// <summary>
/// Represents a compiled CSS <c>animation</c> value.
/// </summary>
/// <param name="Animation">The animation, or <see langword="null"/> for <c>none</c>.</param>
public sealed record CssAnimationValue(AnimationSpec? Animation) : CssValue;

/// <summary>
/// Represents one compiled keyframe of a <c>@keyframes</c> rule.
/// </summary>
/// <param name="Offset">The position within an iteration, in the range [0, 1].</param>
/// <param name="Declarations">The declared properties.</param>
public sealed record CssKeyframe(double Offset, IReadOnlyDictionary<string, CssValue> Declarations);

/// <summary>
/// Represents a compiled <c>@keyframes</c> rule.
/// </summary>
/// <param name="Name">The keyframes name.</param>
/// <param name="Frames">The keyframes, in source order.</param>
public sealed record CssKeyframes(string Name, ImmutableArray<CssKeyframe> Frames);

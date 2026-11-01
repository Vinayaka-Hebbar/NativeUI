namespace NativeUI.Media;

/// <summary>
/// Identifies the style properties that transitions and keyframes can animate.
/// </summary>
[Flags]
public enum AnimatableProperty
{
    /// <summary>No property.</summary>
    None = 0,

    /// <summary>The <c>opacity</c> property.</summary>
    Opacity = 1 << 0,

    /// <summary>The <c>transform</c> property.</summary>
    Transform = 1 << 1,

    /// <summary>The <c>color</c> property.</summary>
    Color = 1 << 2,

    /// <summary>The <c>background-color</c> property.</summary>
    BackgroundColor = 1 << 3,

    /// <summary>The <c>border-color</c> property.</summary>
    BorderColor = 1 << 4,

    /// <summary>The <c>width</c> property.</summary>
    Width = 1 << 5,

    /// <summary>The <c>height</c> property.</summary>
    Height = 1 << 6,

    /// <summary>The <c>top</c> property.</summary>
    Top = 1 << 7,

    /// <summary>The <c>right</c> property.</summary>
    Right = 1 << 8,

    /// <summary>The <c>bottom</c> property.</summary>
    Bottom = 1 << 9,

    /// <summary>The <c>left</c> property.</summary>
    Left = 1 << 10,

    /// <summary>The CSS <c>all</c> keyword.</summary>
    All = Opacity | Transform | Color | BackgroundColor | BorderColor | Width | Height | Top | Right | Bottom | Left
}

/// <summary>
/// Identifies the order in which an animation plays its iterations.
/// </summary>
public enum AnimationDirection
{
    /// <summary>Every iteration plays forward.</summary>
    Normal,

    /// <summary>Every iteration plays backward.</summary>
    Reverse,

    /// <summary>Iterations alternate, starting forward.</summary>
    Alternate,

    /// <summary>Iterations alternate, starting backward.</summary>
    AlternateReverse
}

/// <summary>
/// Identifies when an animation applies its keyframes outside its active period.
/// </summary>
public enum AnimationFillMode
{
    /// <summary>The animation has no effect before it starts or after it ends.</summary>
    None,

    /// <summary>The final keyframe stays in effect after the animation ends.</summary>
    Forwards,

    /// <summary>The first keyframe is in effect during the delay.</summary>
    Backwards,

    /// <summary>The first keyframe applies during the delay, and the final keyframe after the end.</summary>
    Both
}

/// <summary>
/// Describes how changes to properties transition, from one entry of the CSS <c>transition</c> property.
/// </summary>
/// <param name="Property">The properties this entry applies to.</param>
/// <param name="DurationMilliseconds">The transition duration.</param>
/// <param name="DelayMilliseconds">The time before the transition starts.</param>
/// <param name="Easing">The timing function.</param>
public sealed record TransitionSpec(
    AnimatableProperty Property,
    double DurationMilliseconds,
    double DelayMilliseconds,
    Easing Easing);

/// <summary>
/// Describes how an element plays a named set of keyframes, from the CSS <c>animation</c> property.
/// </summary>
/// <param name="Name">The name of the <c>@keyframes</c> rule.</param>
/// <param name="DurationMilliseconds">The duration of one iteration.</param>
/// <param name="DelayMilliseconds">The time before the first iteration starts.</param>
/// <param name="Easing">The timing function, which applies between each pair of keyframes.</param>
/// <param name="Iterations">The number of iterations, or <see cref="double.PositiveInfinity"/> to repeat forever.</param>
/// <param name="Direction">The order of the iterations.</param>
/// <param name="FillMode">When the keyframes apply outside the active period.</param>
public sealed record AnimationSpec(
    string Name,
    double DurationMilliseconds,
    double DelayMilliseconds,
    Easing Easing,
    double Iterations,
    AnimationDirection Direction,
    AnimationFillMode FillMode);

/// <summary>
/// Represents one resolved keyframe of an animation.
/// </summary>
/// <param name="Offset">The position within an iteration, in the range [0, 1].</param>
/// <param name="Properties">The properties this keyframe specifies.</param>
/// <param name="Style">The style holding the values of <paramref name="Properties"/>.</param>
public sealed record AnimationKeyframe(double Offset, AnimatableProperty Properties, ComputedStyle Style);

/// <summary>
/// Reads and blends the animatable properties of <see cref="ComputedStyle"/> snapshots.
/// </summary>
public static class StyleInterpolation
{
    /// <summary>
    /// Returns whether <paramref name="first"/> and <paramref name="second"/> differ in <paramref name="property"/>.
    /// </summary>
    public static bool Differs(ComputedStyle first, ComputedStyle second, AnimatableProperty property)
    {
        return property switch
        {
            AnimatableProperty.Opacity => first.Opacity != second.Opacity,
            AnimatableProperty.Transform => first.Transform != second.Transform,
            AnimatableProperty.Color => first.TextColor != second.TextColor,
            AnimatableProperty.BackgroundColor => first.BackgroundColor != second.BackgroundColor,
            AnimatableProperty.BorderColor => first.BorderColor != second.BorderColor,
            AnimatableProperty.Width => first.Width != second.Width,
            AnimatableProperty.Height => first.Height != second.Height,
            AnimatableProperty.Top => first.Top != second.Top,
            AnimatableProperty.Right => first.Right != second.Right,
            AnimatableProperty.Bottom => first.Bottom != second.Bottom,
            AnimatableProperty.Left => first.Left != second.Left,
            _ => false
        };
    }

    /// <summary>
    /// Returns whether a transition between the values of <paramref name="property"/> can blend. As in CSS, a value such as
    /// an automatic height has no numeric form, so it changes immediately.
    /// </summary>
    public static bool CanBlend(ComputedStyle from, ComputedStyle to, AnimatableProperty property)
    {
        return property switch
        {
            AnimatableProperty.Width => CanBlendWidth(from.Width, to.Width),
            AnimatableProperty.Height => from.Height.HasValue && to.Height.HasValue,
            AnimatableProperty.Top => from.Top.HasValue && to.Top.HasValue,
            AnimatableProperty.Right => from.Right.HasValue && to.Right.HasValue,
            AnimatableProperty.Bottom => from.Bottom.HasValue && to.Bottom.HasValue,
            AnimatableProperty.Left => from.Left.HasValue && to.Left.HasValue,
            _ => true
        };
    }

    /// <summary>
    /// Returns <paramref name="style"/> with <paramref name="property"/> set to the blend of its values in
    /// <paramref name="from"/> and <paramref name="to"/>, where a <paramref name="progress"/> of 0 is <paramref name="from"/>.
    /// A value without a numeric form switches at the midpoint.
    /// </summary>
    public static ComputedStyle Blend(ComputedStyle style, AnimatableProperty property, ComputedStyle from, ComputedStyle to, double progress)
    {
        return property switch
        {
            AnimatableProperty.Opacity => style with { Opacity = Lerp(from.Opacity, to.Opacity, progress) },
            AnimatableProperty.Transform => style with { Transform = BlendTransform(from.Transform, to.Transform, progress) },
            AnimatableProperty.Color => style with { TextColor = BlendColor(from.TextColor, to.TextColor, progress) },
            AnimatableProperty.BackgroundColor => style with { BackgroundColor = BlendColor(from.BackgroundColor, to.BackgroundColor, progress) },
            AnimatableProperty.BorderColor => style with { BorderColor = BlendColor(from.BorderColor, to.BorderColor, progress) },
            AnimatableProperty.Width => style with { Width = BlendWidth(from.Width, to.Width, progress) },
            AnimatableProperty.Height => style with { Height = BlendLength(from.Height, to.Height, progress) },
            AnimatableProperty.Top => style with { Top = BlendLength(from.Top, to.Top, progress) },
            AnimatableProperty.Right => style with { Right = BlendLength(from.Right, to.Right, progress) },
            AnimatableProperty.Bottom => style with { Bottom = BlendLength(from.Bottom, to.Bottom, progress) },
            AnimatableProperty.Left => style with { Left = BlendLength(from.Left, to.Left, progress) },
            _ => style
        };
    }

    private static double Lerp(double from, double to, double progress)
    {
        return from + ((to - from) * progress);
    }

    private static double? BlendLength(double? from, double? to, double progress)
    {
        if (from is double start && to is double end)
        {
            return Lerp(start, end, progress);
        }

        return progress < 0.5 ? from : to;
    }

    private static bool CanBlendWidth(WidthValue from, WidthValue to)
    {
        return (from.IsPixels && to.IsPixels) || (from.IsPercent && to.IsPercent);
    }

    private static WidthValue BlendWidth(WidthValue from, WidthValue to, double progress)
    {
        if (!CanBlendWidth(from, to))
        {
            return progress < 0.5 ? from : to;
        }

        double value = Lerp(from.Value, to.Value, progress);
        return from.IsPercent ? WidthValue.Percent(value) : WidthValue.Pixels(value);
    }

    private static StyleTransform? BlendTransform(StyleTransform? from, StyleTransform? to, double progress)
    {
        StyleTransform start = from ?? StyleTransform.Identity;
        StyleTransform end = to ?? StyleTransform.Identity;
        return new StyleTransform(
            Lerp(start.TranslateX, end.TranslateX, progress),
            Lerp(start.TranslateY, end.TranslateY, progress),
            Lerp(start.TranslateXPercent, end.TranslateXPercent, progress),
            Lerp(start.TranslateYPercent, end.TranslateYPercent, progress),
            Lerp(start.ScaleX, end.ScaleX, progress),
            Lerp(start.ScaleY, end.ScaleY, progress));
    }

    /// <summary>
    /// Blends in sRGB. An unspecified color is the other color made fully transparent, so it fades instead of passing through black.
    /// </summary>
    private static Color? BlendColor(Color? from, Color? to, double progress)
    {
        if (from is null && to is null)
        {
            return null;
        }

        (byte startRed, byte startGreen, byte startBlue, byte startAlpha) = (from ?? to!.Value).ToSrgbBytes();
        (byte endRed, byte endGreen, byte endBlue, byte endAlpha) = (to ?? from!.Value).ToSrgbBytes();
        double startOpacity = from is null ? 0 : startAlpha / 255.0;
        double endOpacity = to is null ? 0 : endAlpha / 255.0;
        return Color.FromSrgb(
            Lerp(startRed, endRed, progress) / 255.0,
            Lerp(startGreen, endGreen, progress) / 255.0,
            Lerp(startBlue, endBlue, progress) / 255.0,
            Lerp(startOpacity, endOpacity, progress));
    }
}

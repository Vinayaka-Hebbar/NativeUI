using NativeUI.Media;

namespace NativeUI.Controls;

/// <summary>
/// Runs the transitions and keyframe animations of one element on hosts that draw their own frames, and produces the
/// element's effective style from its cascaded style. Operations must run on the UI thread.
/// </summary>
internal sealed class ElementAnimator(UIElement element) : IFrameTarget
{
    private static readonly AnimatableProperty[] s_properties =
    [
        AnimatableProperty.Opacity,
        AnimatableProperty.Transform,
        AnimatableProperty.Color,
        AnimatableProperty.BackgroundColor,
        AnimatableProperty.BorderColor,
        AnimatableProperty.Width,
        AnimatableProperty.Height,
        AnimatableProperty.Top,
        AnimatableProperty.Right,
        AnimatableProperty.Bottom,
        AnimatableProperty.Left
    ];

    private readonly List<RunningTransition> _transitions = [];
    private readonly List<RunningAnimation> _animations = [];
    private ComputedStyle? _target;

    /// <summary>
    /// Takes the element's new cascaded style, starts the transitions and animation it requires, and returns the effective style.
    /// </summary>
    public ComputedStyle Update(ComputedStyle target)
    {
        double now = AnimationClock.Now;

        // As in a browser, the first style an element is drawn with never transitions.
        if (_target is not null && element.HasRendered)
        {
            StartTransitions(_target, target, now);
        }

        UpdateStyleAnimation(target, now);
        _target = target;
        UpdateClockRegistration(now);
        return Compose(target, now);
    }

    /// <summary>
    /// Plays <paramref name="spec"/> in addition to the element's style animation and returns through <paramref name="animation"/>.
    /// </summary>
    public void Start(AnimationSpec spec, IReadOnlyList<AnimationKeyframe> frames, Animation animation)
    {
        _target ??= element.ComputedStyle;

        double now = AnimationClock.Now;
        RunningAnimation running = new(spec, frames, now, animation);
        animation.SetCancelHandler(() => CancelAnimation(running));
        _animations.Add(running);
        UpdateClockRegistration(now);
        element.ApplyAnimatedStyle(Compose(_target, now));
    }

    /// <summary>
    /// Advances to <paramref name="now"/>. An element that is not shown keeps its state, so a hidden animation costs no frames.
    /// </summary>
    public void Tick(double now)
    {
        if (_target is null || !element.IsVisibleInTree)
        {
            return;
        }

        _transitions.RemoveAll(transition => transition.IsFinished(now));

        List<RunningAnimation> finished = [];
        for (int i = _animations.Count - 1; i >= 0; i--)
        {
            RunningAnimation animation = _animations[i];
            if (animation.Handle is not null && animation.IsFinished(now))
            {
                finished.Add(animation);
                _animations.RemoveAt(i);
            }
        }

        element.ApplyAnimatedStyle(Compose(_target, now));
        UpdateClockRegistration(now);

        foreach (RunningAnimation animation in finished)
        {
            animation.Handle!.Complete();
        }
    }

    /// <summary>
    /// Stops everything and returns the element to its cascaded style.
    /// </summary>
    public void Cancel()
    {
        AnimationClock.Remove(this);
        RunningAnimation[] animations = [.. _animations];
        _transitions.Clear();
        _animations.Clear();
        if (_target is not null)
        {
            element.ApplyAnimatedStyle(_target);
        }

        foreach (RunningAnimation animation in animations)
        {
            animation.Handle?.Complete();
        }
    }

    private static TransitionSpec? FindTransitionSpec(ComputedStyle style, AnimatableProperty property)
    {
        // As in CSS, a later entry naming the same property replaces an earlier one.
        TransitionSpec? spec = null;
        foreach (TransitionSpec candidate in style.Transitions)
        {
            if (candidate.Property.HasFlag(property))
            {
                spec = candidate;
            }
        }

        return spec;
    }

    private void StartTransitions(ComputedStyle previousTarget, ComputedStyle target, double now)
    {
        ComputedStyle current = element.ComputedStyle;
        foreach (AnimatableProperty property in s_properties)
        {
            if (!StyleInterpolation.Differs(previousTarget, target, property))
            {
                continue;
            }

            _transitions.RemoveAll(transition => transition.Property == property);

            TransitionSpec? spec = FindTransitionSpec(target, property);
            if (spec is not null &&
                spec.DurationMilliseconds > 0 &&
                StyleInterpolation.CanBlend(current, target, property))
            {
                _transitions.Add(new RunningTransition(property, current, spec, now));
            }
        }
    }

    /// <summary>
    /// Keeps a running style animation while its name is unchanged, and otherwise replaces it, as a browser does.
    /// </summary>
    private void UpdateStyleAnimation(ComputedStyle target, double now)
    {
        string? name = target.Animation?.Name;
        RunningAnimation? running = _animations.Find(static animation => animation.Handle is null);
        if (running?.Spec.Name == name)
        {
            return;
        }

        if (running is not null)
        {
            _animations.Remove(running);
        }

        if (name is not null && element.StyleResolver?.ResolveKeyframes(name, element) is { } frames)
        {
            _animations.Add(new RunningAnimation(target.Animation!, frames, now, null));
        }
    }

    private void CancelAnimation(RunningAnimation animation)
    {
        _animations.Remove(animation);
        if (_target is not null)
        {
            double now = AnimationClock.Now;
            element.ApplyAnimatedStyle(Compose(_target, now));
            UpdateClockRegistration(now);
        }
    }

    /// <summary>
    /// Applies transitions and then animations over <paramref name="target"/>, because animations take precedence in the cascade.
    /// </summary>
    private ComputedStyle Compose(ComputedStyle target, double now)
    {
        ComputedStyle style = target;
        foreach (RunningTransition transition in _transitions)
        {
            style = StyleInterpolation.Blend(style, transition.Property, transition.From, target, transition.GetProgress(now));
        }

        foreach (RunningAnimation animation in _animations)
        {
            style = animation.Apply(style, now);
        }

        return style;
    }

    private void UpdateClockRegistration(double now)
    {
        bool needsTicks = _transitions.Count > 0;
        foreach (RunningAnimation animation in _animations)
        {
            needsTicks |= !animation.IsFinished(now);
        }

        if (needsTicks)
        {
            AnimationClock.Add(this);
        }
        else
        {
            AnimationClock.Remove(this);
        }
    }

    /// <summary>
    /// Holds one property moving from the value it had when the transition started to the element's target.
    /// </summary>
    private sealed class RunningTransition(AnimatableProperty property, ComputedStyle from, TransitionSpec spec, double startTime)
    {
        public AnimatableProperty Property => property;

        public ComputedStyle From => from;

        public bool IsFinished(double now)
        {
            return now - startTime - spec.DelayMilliseconds >= spec.DurationMilliseconds;
        }

        /// <summary>
        /// Returns the eased progress, which stays at zero during the delay.
        /// </summary>
        public double GetProgress(double now)
        {
            return spec.Easing.Evaluate((now - startTime - spec.DelayMilliseconds) / spec.DurationMilliseconds);
        }
    }

    /// <summary>
    /// Holds a playing set of keyframes. <see cref="Handle"/> is null for an animation that the element's style requests.
    /// </summary>
    private sealed class RunningAnimation(AnimationSpec spec, IReadOnlyList<AnimationKeyframe> frames, double startTime, Animation? handle)
    {
        public AnimationSpec Spec => spec;

        public Animation? Handle => handle;

        public bool IsFinished(double now)
        {
            double elapsed = now - startTime - spec.DelayMilliseconds;
            return elapsed >= 0 && (spec.DurationMilliseconds <= 0 || elapsed >= spec.DurationMilliseconds * spec.Iterations);
        }

        /// <summary>
        /// Returns <paramref name="style"/> with the keyframe values at <paramref name="now"/> applied. A property that has no
        /// keyframe at the start or end of an iteration uses its value in <paramref name="style"/> there.
        /// </summary>
        public ComputedStyle Apply(ComputedStyle style, double now)
        {
            if (GetOffset(now) is not double offset)
            {
                return style;
            }

            ComputedStyle result = style;
            foreach (AnimatableProperty property in s_properties)
            {
                AnimationKeyframe? lower = null;
                AnimationKeyframe? upper = null;
                foreach (AnimationKeyframe frame in frames)
                {
                    if (!frame.Properties.HasFlag(property))
                    {
                        continue;
                    }

                    if (frame.Offset <= offset)
                    {
                        lower = frame;
                    }
                    else
                    {
                        upper ??= frame;
                    }
                }

                if (lower is null && upper is null)
                {
                    continue;
                }

                double lowerOffset = lower?.Offset ?? 0;
                double upperOffset = upper?.Offset ?? 1;
                double progress = upperOffset > lowerOffset ? (offset - lowerOffset) / (upperOffset - lowerOffset) : 0;
                result = StyleInterpolation.Blend(
                    result,
                    property,
                    lower?.Style ?? style,
                    upper?.Style ?? style,
                    spec.Easing.Evaluate(progress));
            }

            return result;
        }

        /// <summary>
        /// Returns the position within an iteration at <paramref name="now"/>, or null when the keyframes have no effect then.
        /// </summary>
        private double? GetOffset(double now)
        {
            double elapsed = now - startTime - spec.DelayMilliseconds;
            if (elapsed < 0)
            {
                return spec.FillMode is AnimationFillMode.Backwards or AnimationFillMode.Both ? ApplyDirection(0, 0) : null;
            }

            if (IsFinished(now))
            {
                if (spec.FillMode is not (AnimationFillMode.Forwards or AnimationFillMode.Both))
                {
                    return null;
                }

                double lastIteration = Math.Ceiling(spec.Iterations) - 1;
                return ApplyDirection(lastIteration, spec.Iterations - lastIteration);
            }

            double position = elapsed / spec.DurationMilliseconds;
            double iteration = Math.Floor(position);
            return ApplyDirection(iteration, position - iteration);
        }

        private double ApplyDirection(double iteration, double progress)
        {
            bool isOddIteration = iteration % 2 == 1;
            bool isReversed = spec.Direction switch
            {
                AnimationDirection.Reverse => true,
                AnimationDirection.Alternate => isOddIteration,
                AnimationDirection.AlternateReverse => !isOddIteration,
                _ => false
            };

            return isReversed ? 1 - progress : progress;
        }
    }
}

using NativeUI.Core;

namespace NativeUI.Controls;

/// <summary>
/// Provides rich accessible label content and activates or focuses its associated control.
/// </summary>
public class Label : ContentControl
{
    /// <summary>
    /// Identifies an explicit target; null selects the first form control in the content.
    /// </summary>
    public static readonly BindableProperty<UIElement?> TargetProperty =
        BindableProperty.Create<Label, UIElement?>(
            nameof(Target),
            propertyChanged: static (owner, _, _) => owner.OnTargetChanged());

    /// <summary>
    /// Identifies the label supplying a target element's accessible name.
    /// </summary>
    internal static readonly BindableProperty<Label?> LabeledByProperty =
        BindableProperty.CreateAttached<Label?>("LabeledBy", typeof(Label));

    private static int _nextId;
    private UIElement? _associatedTarget;
    private readonly IDisposable _gesture;

    /// <summary>
    /// Creates a label with shared pointer and keyboard activation behavior.
    /// </summary>
    public Label()
    {
        _gesture = ClickGesture.Attach(this, Activate);
        VisualInvalidated += OnVisualInvalidated;
    }

    /// <summary>
    /// Gets or sets the explicitly associated control.
    /// </summary>
    public UIElement? Target
    {
        get => GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    /// <inheritdoc />
    public override string TagName => "label";
    internal UIElement? AssociatedControl => Target ?? Descendants(this).FirstOrDefault(static element => element is IFormControl);

    /// <summary>
    /// Activates the associated form control, or focuses another explicit target.
    /// </summary>
    public void Activate()
    {
        if (IsDisposed || !IsEffectivelyEnabled || AssociatedControl is not { IsDisposed: false, IsEffectivelyEnabled: true } target)
        {
            return;
        }

        if (target is IFormControl control)
        {
            control.Activate();
        }
        else
        {
            target.Focus(FocusState.Pointer);
        }
    }

    internal static Label? GetLabeledBy(UIElement target)
    {
        return target.GetValue(LabeledByProperty);
    }

    internal static string GetAccessibleName(UIElement target)
    {
        Label? label = GetLabeledBy(target);
        return label is null ? string.Empty : string.Join(" ", Descendants(label)
            .OfType<Text>()
            .Select(static text => text.Value)
            .Where(static text => !string.IsNullOrEmpty(text)));
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree()
    {
        base.OnAttachedToVisualTree();
        OnTargetChanged();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree()
    {
        ClearTarget();
        base.OnDetachedFromVisualTree();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            VisualInvalidated -= OnVisualInvalidated;
            _gesture.Dispose();
            ClearTarget();
        }

        base.Dispose(disposing);
    }

    private void OnVisualInvalidated(object? sender, EventArgs args)
    {
        OnTargetChanged();
    }

    private void OnTargetChanged()
    {
        if (!IsAttachedToVisualTree)
        {
            return;
        }

        UIElement? target = AssociatedControl is { IsDisposed: false } control ? control : null;
        if (ReferenceEquals(target, _associatedTarget))
        {
            return;
        }

        ClearTarget();
        _associatedTarget = target;
        if (target is not null)
        {
            Id ??= "nui-label-" + Interlocked.Increment(ref _nextId);
            target.Id ??= "nui-control-" + Interlocked.Increment(ref _nextId);
            target.SetValue(LabeledByProperty, this);
        }
    }

    private void ClearTarget()
    {
        UIElement? target = _associatedTarget;
        _associatedTarget = null;
        if (target is { IsDisposed: false } && ReferenceEquals(GetLabeledBy(target), this))
        {
            target.ClearValue(LabeledByProperty);
        }
    }

    private static IEnumerable<UIElement> Descendants(View view)
    {
        foreach (UIElement child in view.Children)
        {
            yield return child;
            if (child is View nested && child is not Label)
            {
                foreach (UIElement descendant in Descendants(nested))
                {
                    yield return descendant;
                }
            }
        }
    }
}

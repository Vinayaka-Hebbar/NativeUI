using NativeUI.Core;

namespace NativeUI.Controls;

/// <summary>
/// Represents a value-bearing choice within a RadioGroup.
/// </summary>
public class RadioGroupItem : RadioButton, IFormControl
{
    /// <summary>
    /// Identifies the value assigned to the containing group's SelectedValue when selected.
    /// </summary>
    public static readonly BindableProperty<object?> ValueProperty =
        BindableProperty.Create<RadioGroupItem, object?>(nameof(Value));

    internal static readonly BindableProperty<bool> IsTabStopProperty =
        BindableProperty.Create<RadioGroupItem, bool>(nameof(IsTabStop), defaultValue: false);

    private RadioGroup? _group;

    internal RadioGroup? Group => _group;

    /// <inheritdoc />
    public override string TagName => "radiogroupitem";

    /// <summary>
    /// Gets or sets this choice's value. Values should be distinct and non-null within the group.
    /// </summary>
    public object? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    internal bool IsTabStop
    {
        get => GetValue(IsTabStopProperty);
        set => SetValue(IsTabStopProperty, value);
    }

    /// <inheritdoc />
    protected override void BuildTemplate()
    {
        View indicator = DefinePart(new View(), "indicator");
        indicator.Children.Add(DefinePart(new View(), "icon"));
        Children.Add(indicator);
    }

    /// <inheritdoc />
    internal override void OnAncestorChanged()
    {
        base.OnAncestorChanged();
        if (IsDisposed)
        {
            return;
        }

        RadioGroup? group = null;
        for (UIElement? ancestor = Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ancestor is RadioGroup candidate)
            {
                group = candidate.IsDisposed ? null : candidate;
                break;
            }
        }

        if (ReferenceEquals(_group, group))
        {
            group?.RegisterItem(this);
            return;
        }

        RadioGroup? previous = _group;
        _group = group;
        IsTabStop = false;
        previous?.UnregisterItem(this);
        if (!ReferenceEquals(_group, group) || IsDisposed)
        {
            return;
        }

        group?.RegisterItem(this);
    }

    /// <inheritdoc />
    internal override void OnVisibilityChanged()
    {
        base.OnVisibilityChanged();
        _group?.UpdateSelection();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree()
    {
        base.OnAttachedToVisualTree();
        _group?.UpdateSelection();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree()
    {
        _group?.UpdateSelection();
        base.OnDetachedFromVisualTree();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        try
        {
            base.Dispose(disposing);
        }
        finally
        {
            if (disposing)
            {
                RadioGroup? group = _group;
                _group = null;
                group?.UnregisterItem(this);
            }
        }
    }

    /// <inheritdoc />
    void IFormControl.Reset()
    {
        ValidityState.ResetInteraction(this);
        CheckValidity();
    }
}

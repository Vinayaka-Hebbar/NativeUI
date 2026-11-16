using NativeUI.Core;
using NativeUI.Layout;

namespace NativeUI.Controls;

/// <summary>
/// Represents a panel within an enclosing <see cref="Resizable"/> container, defining its default,
/// minimum, maximum, and active proportional sizes.
/// </summary>
public class ResizablePanel : View, IResizablePanelLayoutNode
{
    /// <summary>
    /// Identifies the <see cref="DefaultSize"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> DefaultSizeProperty =
        BindableProperty.Create<ResizablePanel, double>(
            nameof(DefaultSize),
            defaultValue: 0.0,
            propertyChanged: static (owner, _, _) => owner.InvalidateMeasure());

    /// <summary>
    /// Identifies the <see cref="Size"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> SizeProperty =
        BindableProperty.Create<ResizablePanel, double>(
            nameof(Size),
            defaultValue: 0.0,
            propertyChanged: static (owner, _, _) => owner.InvalidateMeasure());

    /// <summary>
    /// Identifies the <see cref="MinSize"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> MinSizeProperty =
        BindableProperty.Create<ResizablePanel, double>(
            nameof(MinSize),
            defaultValue: 0.0,
            propertyChanged: static (owner, _, _) => owner.InvalidateMeasure());

    /// <summary>
    /// Identifies the <see cref="MaxSize"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> MaxSizeProperty =
        BindableProperty.Create<ResizablePanel, double>(
            nameof(MaxSize),
            defaultValue: 100.0,
            propertyChanged: static (owner, _, _) => owner.InvalidateMeasure());

    /// <summary>
    /// Identifies the <see cref="IsCollapsed"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> IsCollapsedProperty =
        BindableProperty.Create<ResizablePanel, bool>(
            nameof(IsCollapsed),
            defaultValue: false,
            propertyChanged: static (owner, _, _) => owner.InvalidateMeasure());

    /// <summary>
    /// Identifies the <see cref="CollapsedSize"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> CollapsedSizeProperty =
        BindableProperty.Create<ResizablePanel, double>(
            nameof(CollapsedSize),
            defaultValue: 0.0,
            propertyChanged: static (owner, _, _) => owner.InvalidateMeasure());

    /// <inheritdoc />
    public override string TagName => "resizablepanel";

    /// <summary>
    /// Gets or sets the initial size percentage of this panel. Defaults to 0.
    /// </summary>
    public double DefaultSize
    {
        get => GetValue(DefaultSizeProperty);
        set => SetValue(DefaultSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the current size percentage of this panel. When unset (0), <see cref="DefaultSize"/> is used.
    /// </summary>
    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the minimum size percentage allowed when resizing. Defaults to 0.
    /// </summary>
    public double MinSize
    {
        get => GetValue(MinSizeProperty);
        set => SetValue(MinSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the maximum size percentage allowed when resizing. Defaults to 100.
    /// </summary>
    public double MaxSize
    {
        get => GetValue(MaxSizeProperty);
        set => SetValue(MaxSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether this panel is collapsed. Defaults to <see langword="false"/>.
    /// </summary>
    public bool IsCollapsed
    {
        get => GetValue(IsCollapsedProperty);
        set => SetValue(IsCollapsedProperty, value);
    }

    /// <summary>
    /// Gets or sets the proportional size percentage used when <see cref="IsCollapsed"/> is <see langword="true"/>. Defaults to 0.
    /// </summary>
    public double CollapsedSize
    {
        get => GetValue(CollapsedSizeProperty);
        set => SetValue(CollapsedSizeProperty, value);
    }

    /// <summary>
    /// Gets the effective proportional size of this panel.
    /// </summary>
    public double EffectiveSize => IsCollapsed
        ? CollapsedSize
        : (Size > 0 ? Size : (DefaultSize > 0 ? DefaultSize : 0.0));

    /// <summary>
    /// Collapses this panel.
    /// </summary>
    public void Collapse()
    {
        IsCollapsed = true;
    }

    /// <summary>
    /// Expands this panel from its collapsed state.
    /// </summary>
    public void Expand()
    {
        IsCollapsed = false;
    }

    /// <inheritdoc />
    public override string? GetAttribute(string name)
    {
        if (string.Equals(name, "data-collapsed", StringComparison.Ordinal))
        {
            return IsCollapsed ? "true" : "false";
        }

        return base.GetAttribute(name);
    }
}

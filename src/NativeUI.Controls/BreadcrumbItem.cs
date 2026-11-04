using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;

namespace NativeUI.Controls;

/// <summary>
/// Hosts one item of a <see cref="Breadcrumb"/> as the breadcrumb's <c>item</c> part: a <c>separator</c> part before
/// every item but the first, followed by the item's element, in a row. The last item is the current page. It has
/// <c>data-current="true"</c>, and the user cannot click it or move focus to it.
/// </summary>
public class BreadcrumbItem : View, IStackLayoutNode
{
    static BreadcrumbItem()
    {
        WidthProperty.OverrideMetadata<BreadcrumbItem>(WidthValue.FitContent);
    }

    /// <summary>
    /// Identifies the <see cref="IsCurrent"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> IsCurrentProperty =
        BindableProperty.Create<BreadcrumbItem, bool>(
            nameof(IsCurrent),
            defaultValue: false,
            propertyChanged: static (owner, _, isCurrent) => owner.OnIsCurrentChanged(isCurrent));

    /// <summary>
    /// Initializes a new instance of the <see cref="BreadcrumbItem"/> class.
    /// </summary>
    public BreadcrumbItem()
    {
        OnIsCurrentChanged(false);
    }

    /// <inheritdoc />
    public override string TagName => "breadcrumbitem";

    /// <summary>
    /// Gets a value indicating whether this item is the current page, the last item of the breadcrumb.
    /// </summary>
    public bool IsCurrent
    {
        get => GetValue(IsCurrentProperty);
        internal set => SetValue(IsCurrentProperty, value);
    }

    Orientation IStackLayoutNode.Orientation => Orientation.Horizontal;

    double IStackLayoutNode.Spacing => 0;

    Alignment IStackLayoutNode.HorizontalAlignment => Alignment.Start;

    /// <summary>
    /// Returns <c>true</c> for <c>data-current</c> while this item is the current page.
    /// </summary>
    /// <param name="name">The attribute name.</param>
    public override string? GetAttribute(string name)
    {
        if (string.Equals(name, "data-current", StringComparison.Ordinal))
        {
            return IsCurrent ? "true" : null;
        }

        return base.GetAttribute(name);
    }

    private void OnIsCurrentChanged(bool isCurrent)
    {
        Focusable = !isCurrent;
        SetValue(CursorProperty, isCurrent ? null : Cursor.Pointer);
        InvalidateStyle();
    }
}

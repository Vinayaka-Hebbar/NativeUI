using NativeUI.Core;

namespace NativeUI.Controls;

/// <summary>
/// Hosts the element of one item in a <see cref="List"/> or <see cref="VirtualizedList"/>, as the list's <c>item</c>
/// template part. The item element is its only child. It has <see cref="ElementStates.Selected"/>, selected by
/// <c>:selected</c>, while the list has the item selected.
/// </summary>
public class ListItem : View
{
    /// <summary>
    /// Identifies the <see cref="IsSelected"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> IsSelectedProperty =
        BindableProperty.Create<ListItem, bool>(
            nameof(IsSelected),
            defaultValue: false,
            propertyChanged: static (owner, _, isSelected) => owner.OnIsSelectedChanged(isSelected));

    /// <inheritdoc />
    public override string TagName => "listitem";

    /// <summary>
    /// Gets a value indicating whether the list has this item selected.
    /// </summary>
    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        internal set => SetValue(IsSelectedProperty, value);
    }

    private void OnIsSelectedChanged(bool isSelected)
    {
        SetState(ElementStates.Selected, isSelected);
        InvalidateStyle();
        InvalidateVisual();
    }
}

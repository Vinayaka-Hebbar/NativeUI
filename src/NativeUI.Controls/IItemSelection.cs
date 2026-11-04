namespace NativeUI.Controls;

/// <summary>
/// Changes the selection of a <see cref="Selector"/> by item index. Every change follows the selector's
/// <see cref="Selector.SelectionMode"/>: no item can be selected while it is <see cref="SelectionMode.None"/>, and selecting
/// an item replaces the selection while it is <see cref="SelectionMode.Single"/>.
/// </summary>
public interface IItemSelection
{
    /// <summary>
    /// Returns whether the item at <paramref name="index"/> is selected.
    /// </summary>
    bool IsSelected(int index);

    /// <summary>
    /// Selects the item at <paramref name="index"/>.
    /// </summary>
    void Select(int index);

    /// <summary>
    /// Deselects the item at <paramref name="index"/>.
    /// </summary>
    void Deselect(int index);

    /// <summary>
    /// Selects the item at <paramref name="index"/> when it is not selected, and deselects it otherwise.
    /// </summary>
    void Toggle(int index);

    /// <summary>
    /// Replaces the selection with the items from <paramref name="startIndex"/> to <paramref name="endIndex"/>, inclusive,
    /// in either order. While the mode is <see cref="SelectionMode.Single"/>, only <paramref name="endIndex"/> is selected.
    /// </summary>
    void SelectRange(int startIndex, int endIndex);

    /// <summary>
    /// Deselects every item.
    /// </summary>
    void Clear();
}

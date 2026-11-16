namespace NativeUI.Controls;

/// <summary>
/// Specifies how a <see cref="SplitView"/> displays its pane relative to its content.
/// </summary>
public enum SplitViewDisplayMode
{
    /// <summary>
    /// Places the pane beside the content when open and collapses it completely when closed.
    /// </summary>
    Inline,

    /// <summary>
    /// Reserves <see cref="SplitView.CompactPaneLength"/> when closed and expands to <see cref="SplitView.OpenPaneLength"/>
    /// beside the content when open.
    /// </summary>
    CompactInline,

    /// <summary>
    /// Hides the pane when closed and presents it over the content when open, dismissing on Escape or outside press.
    /// </summary>
    Overlay,

    /// <summary>
    /// Reserves <see cref="SplitView.CompactPaneLength"/> beside the content and expands the pane over the content when open,
    /// dismissing on Escape or outside press.
    /// </summary>
    CompactOverlay
}

namespace NativeUI.Controls;

/// <summary>
/// Specifies how the navigation pane is displayed within a <see cref="NavigationView"/>.
/// </summary>
public enum NavigationViewPaneDisplayMode
{
    /// <summary>
    /// The pane is docked beside content when open and completely hidden when closed.
    /// </summary>
    Inline = 0,

    /// <summary>
    /// The pane shows a compact icon rail when closed and expands beside content when open.
    /// </summary>
    CompactInline = 1,

    /// <summary>
    /// The pane opens as a floating overlay above content with a dismiss backdrop.
    /// </summary>
    Overlay = 2,

    /// <summary>
    /// The pane shows a compact icon rail and expands as an overlay above content when open.
    /// </summary>
    CompactOverlay = 3,

    /// <summary>
    /// The navigation bar is arranged horizontally across the top rather than in a side pane.
    /// </summary>
    Top = 4
}

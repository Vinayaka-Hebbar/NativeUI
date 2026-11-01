namespace NativeUI.Controls;

using NativeUI.Layout;

/// <summary>
/// Implemented by a native or browser DOM peer that can query layout bounds on demand.
/// </summary>
internal interface IGeometryPeer
{
    /// <summary>
    /// Queries the current layout bounds from the underlying platform element, or null if unavailable.
    /// </summary>
    Rect? GetBounds();
}

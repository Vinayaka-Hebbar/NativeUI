using NativeUI.Controls;
using NativeUI.Layout;

namespace NativeUI.Graphics;

/// <summary>
/// Describes one overlay scrollbar that the desktop renderer draws for a scrolled view.
/// </summary>
/// <param name="View">The view whose content the scrollbar scrolls.</param>
/// <param name="Orientation">The axis the scrollbar scrolls along.</param>
/// <param name="Track">The area along the view's edge that the thumb moves in, in layout coordinates.</param>
/// <param name="Thumb">The thumb, whose position and length show the visible part of the content, in layout coordinates.</param>
public readonly record struct ScrollbarGeometry(View View, Orientation Orientation, Rect Track, Rect Thumb);

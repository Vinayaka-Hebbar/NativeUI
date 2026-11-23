using System.Collections.Immutable;
using NativeUI.Media;

namespace NativeUI.Layout;

public sealed partial class LayoutEngine
{
    private static bool HasAnchorPositioning(ComputedStyle style)
    {
        return style.PositionArea is { IsNone: false } || style.Top.IsAnchor || style.Right.IsAnchor ||
            style.Bottom.IsAnchor || style.Left.IsAnchor;
    }

    private static bool ContainsAnchors(ILayoutNode node)
    {
        LayoutState state = GetState(node);
        if (state.HasAnchors is bool retained)
        {
            return retained;
        }

        bool contains = HasAnchorPositioning(node.ComputedStyle) || node is IPortalLayoutNode { Anchor: not null };
        foreach (ILayoutNode child in node.LayoutChildren)
        {
            if (child.IsVisible && child is not IPortalLayoutNode && ContainsAnchors(child))
            {
                contains = true;
            }
        }

        state.HasAnchors = contains;
        return contains;
    }

    /// <summary>
    /// Owns anchor lookup and positioned-layout dependencies for one layout call. Geometry is recorded by arrange,
    /// rather than retained across windows or inferred from paint order.
    /// </summary>
    private sealed class AnchorLayout
    {
        private readonly LayoutEngine _engine;
        private readonly ILayoutNode _root;
        private readonly Dictionary<ILayoutNode, Entry> _entries = new(ReferenceEqualityComparer.Instance);
        private readonly List<Entry> _order = [];
        private readonly Dictionary<ILayoutNode, Rect> _external = new(ReferenceEqualityComparer.Instance);
        private readonly List<Entry> _pending = [];

        internal AnchorLayout(LayoutEngine engine, ILayoutNode root, IReadOnlyList<LayoutBox> precedingTrees)
        {
            _engine = engine;
            _root = root;
            Index(root, null, null);
            foreach (LayoutBox tree in precedingTrees)
            {
                IndexExternal(tree);
            }
        }

        internal void Record(ILayoutNode node, Rect bounds)
        {
            if (_entries.TryGetValue(node, out Entry? entry))
            {
                entry.Bounds = bounds;
            }
        }

        internal void Defer(ILayoutNode node, Rect block, Point parentOrigin, Point scrollOffset)
        {
            Entry entry = _entries[node];
            entry.Block = block;
            entry.ParentOrigin = parentOrigin;
            entry.ScrollOffset = node.ComputedStyle.Position == Position.Fixed ? default : scrollOffset;
            if (!entry.IsPending)
            {
                entry.IsPending = true;
                _pending.Add(entry);
            }
        }

        internal void PlaceRoot(IPortalLayoutNode portal, Size viewport)
        {
            Entry entry = _entries[_root];
            entry.Block = new Rect(0, 0, viewport.Width, viewport.Height);
            entry.IsPending = true;
            Place(entry, portal.ComputedStyle, viewport);
        }

        internal void Complete(Size viewport)
        {
            for (int i = 0; i < _pending.Count; i++)
            {
                Resolve(_pending[i], viewport);
            }
        }

        private void Index(ILayoutNode node, Entry? parent, Entry? containingBlock)
        {
            if (!node.IsVisible)
            {
                return;
            }

            ComputedStyle style = node.ComputedStyle;
            Entry entry = new(node, parent, style.Position == Position.Fixed ? null : containingBlock, _order.Count);
            _entries.Add(node, entry);
            _order.Add(entry);
            foreach (ILayoutNode child in node.LayoutChildren)
            {
                if (child is not IPortalLayoutNode)
                {
                    Index(child, entry, style.IsPositioned ? entry : containingBlock);
                }
            }
        }

        private void IndexExternal(LayoutBox box)
        {
            if (box.Element is ILayoutNode node)
            {
                _external[node] = box.Style.Transform is StyleTransform transform
                    ? transform.TransformBounds(box.BorderBox, box.BorderBox)
                    : box.BorderBox;
            }

            foreach (LayoutBox child in box.Children)
            {
                IndexExternal(child);
            }
        }

        private void Resolve(Entry entry, Size viewport)
        {
            if (entry.IsResolved || entry.IsResolving)
            {
                return;
            }

            if (!entry.IsPending)
            {
                if (entry.Parent is Entry parent)
                {
                    Resolve(parent, viewport);
                }

                if (!entry.IsPending)
                {
                    return;
                }
            }

            entry.IsResolving = true;
            Place(entry, entry.Node.ComputedStyle, viewport);
            entry.IsResolving = false;
            entry.IsResolved = true;
        }

        private void Place(Entry entry, ComputedStyle source, Size viewport)
        {
            LayoutState state = GetState(entry.Node);
            Rect? anchor = FindAnchor(entry, source.PositionAnchor, viewport);
            ImmutableArray<PositionTryTactic> options = (entry.Node is IPortalLayoutNode portal
                ? portal.PositionTryFallbacks : source.PositionTryFallbacks).GetValueOrDefault();
            PositionTryTactic? retained = state.PositionFallback;
            List<PositionTryTactic> candidates = [];
            if (retained is PositionTryTactic last && (last == PositionTryTactic.None || (!options.IsDefault && options.Contains(last))))
            {
                candidates.Add(last);
            }

            if (!candidates.Contains(PositionTryTactic.None))
            {
                candidates.Add(PositionTryTactic.None);
            }

            if (!options.IsDefaultOrEmpty)
            {
                foreach (PositionTryTactic option in options)
                {
                    if (!candidates.Contains(option))
                    {
                        candidates.Add(option);
                    }
                }
            }

            PositionTryTactic selected = PositionTryTactic.None;
            bool fits = false;
            foreach (PositionTryTactic tactic in candidates)
            {
                LayoutCandidate(entry, source, tactic, anchor, viewport);
                Rect bounds = state.Bounds;
                Rect absolute = new(entry.ParentOrigin.X + bounds.X, entry.ParentOrigin.Y + bounds.Y, bounds.Width, bounds.Height);
                Thickness margin = state.Margin;
                if (absolute.X - margin.Left >= entry.Block.X && absolute.Y - margin.Top >= entry.Block.Y &&
                    absolute.Right + margin.Right <= entry.Block.Right && absolute.Bottom + margin.Bottom <= entry.Block.Bottom)
                {
                    selected = tactic;
                    fits = true;
                    break;
                }
            }

            if (!fits)
            {
                LayoutCandidate(entry, source, PositionTryTactic.None, anchor, viewport);
            }

            state.PositionFallback = fits ? selected : null;
            Rect finalBounds = state.Bounds;
            _engine.Arrange(entry.Node,
                new Point(entry.ParentOrigin.X + finalBounds.X, entry.ParentOrigin.Y + finalBounds.Y), entry.Block, viewport);
        }

        private void LayoutCandidate(Entry entry, ComputedStyle source, PositionTryTactic tactic, Rect? anchor, Size viewport)
        {
            PositionArea? area = TransformArea(entry.Node is IPortalLayoutNode portal ? portal.PositionArea : source.PositionArea, tactic);
            Rect region = anchor is Rect anchorBounds && area is { IsNone: false } positionArea
                ? GetArea(entry.Block, anchorBounds, positionArea) : entry.Block;
            Thickness margin = TransformMargin(entry.Node is IPortalLayoutNode portalNode ? portalNode.Margin : source.Margin, tactic).Resolve(region.Width);
            (InsetValue Top, InsetValue Right, InsetValue Bottom, InsetValue Left) insets = TransformInsets(source, tactic);
            insets = (
                ResolveAnchorInset(entry, insets.Top, true, false, region, viewport),
                ResolveAnchorInset(entry, insets.Right, false, true, region, viewport),
                ResolveAnchorInset(entry, insets.Bottom, true, true, region, viewport),
                ResolveAnchorInset(entry, insets.Left, false, false, region, viewport));
            (double? Top, double? Right, double? Bottom, double? Left) offsets = (
                ResolveInset(insets.Top, region.Height), ResolveInset(insets.Right, region.Width),
                ResolveInset(insets.Bottom, region.Height), ResolveInset(insets.Left, region.Width));
            BoxConstraints box = ResolveCandidateConstraints(entry.Node, region.Size, tactic, margin, offsets);
            LayoutConstraints constraints = new(region.Size, region.Size, viewport, false, false);
            _engine.Measure(entry.Node, constraints, box);
            ArrangeOutOfFlowChild(entry.Node, region, entry.ParentOrigin, offsets);
            LayoutState state = GetState(entry.Node);
            if (anchor is Rect alignmentAnchor && area is { IsNone: false } alignmentArea)
            {
                Rect bounds = state.Bounds;
                double x = Align(alignmentArea.Inline, region.X, region.Width, alignmentAnchor.X, alignmentAnchor.Width,
                    bounds.Width, margin.Left, margin.Right, insets.Left, insets.Right);
                double y = Align(alignmentArea.Block, region.Y, region.Height, alignmentAnchor.Y, alignmentAnchor.Height,
                    bounds.Height, margin.Top, margin.Bottom, insets.Top, insets.Bottom);
                state.Bounds = new Rect(x - entry.ParentOrigin.X, y - entry.ParentOrigin.Y, bounds.Width, bounds.Height);
            }

            bool hasResolvedAnchor = anchor is not null ||
                (source.Top.IsAnchor && insets.Top.IsPixels) || (source.Right.IsAnchor && insets.Right.IsPixels) ||
                (source.Bottom.IsAnchor && insets.Bottom.IsPixels) || (source.Left.IsAnchor && insets.Left.IsPixels);
            if (!hasResolvedAnchor && entry.ScrollOffset != default)
            {
                state.Bounds = state.Bounds with { X = state.Bounds.X - entry.ScrollOffset.X, Y = state.Bounds.Y - entry.ScrollOffset.Y };
            }

            state.VisualOffset = new Point(state.Bounds.X, state.Bounds.Y);
        }

        private BoxConstraints ResolveCandidateConstraints(
            ILayoutNode node,
            Size area,
            PositionTryTactic tactic,
            Thickness margin,
            (double? Top, double? Right, double? Bottom, double? Left) offsets)
        {
            ComputedStyle style = node.ComputedStyle;
            bool transpose = tactic.HasFlag(PositionTryTactic.FlipStart);
            WidthValue width = transpose ? SwapHeight(style.Height) : style.Width;
            HeightValue height = transpose ? SwapWidth(style.Width) : style.Height;
            double resolvedWidth = ResolvePositionedWidth(ResolveWidthLimit(width, area.Width),
                (offsets.Left, offsets.Right), area.Width, margin.Horizontal,
                _engine.MeasureMaxContentWidth(node, ignoreSpecifiedWidth: transpose));
            double? resolvedHeight = ResolvePositionedHeight(ResolveHeight(height, area.Height),
                (offsets.Top, offsets.Bottom), area.Height, margin.Vertical);
            Size sourceArea = transpose ? new Size(area.Height, area.Width) : area;
            (Size minimum, Size maximum) = ResolveSizeLimits(style, sourceArea);
            if (transpose)
            {
                minimum = new Size(minimum.Height, minimum.Width);
                maximum = new Size(maximum.Height, maximum.Width);
            }

            return new BoxConstraints(resolvedWidth, resolvedHeight, minimum, maximum, margin);
        }

        private static WidthValue SwapHeight(HeightValue height)
        {
            return height.IsPixels ? WidthValue.Pixels(height.Value) : height.IsPercent ? WidthValue.Percent(height.Value) : WidthValue.Auto;
        }

        private static HeightValue SwapWidth(WidthValue width)
        {
            return width.IsPixels ? HeightValue.Pixels(width.Value) : width.IsPercent ? HeightValue.Percent(width.Value) : HeightValue.Auto;
        }

        private InsetValue ResolveAnchorInset(Entry entry, InsetValue inset, bool vertical, bool end, Rect block, Size viewport)
        {
            if (!inset.IsAnchor)
            {
                return inset;
            }

            Rect? anchor = FindAnchor(entry, inset.AnchorName ?? entry.Node.ComputedStyle.PositionAnchor, viewport);
            if (anchor is not Rect bounds || (vertical && inset.AnchorSide is AnchorSide.Left or AnchorSide.Right) ||
                (!vertical && inset.AnchorSide is AnchorSide.Top or AnchorSide.Bottom))
            {
                return InsetValue.Auto;
            }

            double start = vertical ? bounds.Y : bounds.X;
            double finish = vertical ? bounds.Bottom : bounds.Right;
            double coordinate = inset.AnchorSide switch
            {
                AnchorSide.Center => (start + finish) / 2,
                AnchorSide.Right or AnchorSide.Bottom or AnchorSide.End or AnchorSide.SelfEnd => finish,
                AnchorSide.Inside => end ? finish : start,
                AnchorSide.Outside => end ? start : finish,
                _ => start
            };
            return InsetValue.Pixels(end
                ? (vertical ? block.Bottom : block.Right) - coordinate
                : coordinate - (vertical ? block.Y : block.X));
        }

        private Rect? FindAnchor(Entry query, string? name, Size viewport)
        {
            if (name is null or "auto")
            {
                return query.Node is IPortalLayoutNode { Anchor: ILayoutNode implicitAnchor } && _external.TryGetValue(implicitAnchor, out Rect bounds)
                    ? bounds : null;
            }

            if (name == "none")
            {
                return null;
            }

            for (Entry? ancestor = query.Parent; ancestor is not null; ancestor = ancestor.Parent)
            {
                if (ancestor.Node.ComputedStyle.AnchorName == name && IsAcceptable(ancestor, query))
                {
                    Resolve(ancestor, viewport);
                    return AnchorBounds(ancestor);
                }
            }

            for (int i = _order.Count - 1; i >= 0; i--)
            {
                Entry candidate = _order[i];
                if (candidate.Node.ComputedStyle.AnchorName == name && IsAcceptable(candidate, query))
                {
                    Resolve(candidate, viewport);
                    if (AnchorBounds(candidate) is Rect bounds)
                    {
                        return bounds;
                    }
                }
            }

            Rect? external = null;
            foreach ((ILayoutNode node, Rect bounds) in _external)
            {
                if (node.ComputedStyle.AnchorName == name)
                {
                    external = bounds;
                }
            }

            return external;
        }

        private static Rect? AnchorBounds(Entry entry)
        {
            if (entry.Bounds is not Rect bounds)
            {
                return null;
            }

            Point[] corners = [new(bounds.X, bounds.Y), new(bounds.Right, bounds.Y), new(bounds.Right, bounds.Bottom), new(bounds.X, bounds.Bottom)];
            for (Entry? ancestor = entry; ancestor is not null; ancestor = ancestor.Parent)
            {
                if (ancestor.Bounds is Rect origin && ancestor.Node.ComputedStyle.Transform is StyleTransform transform)
                {
                    for (int i = 0; i < corners.Length; i++)
                    {
                        corners[i] = transform.TransformPoint(corners[i], origin);
                    }
                }
            }

            double left = corners.Min(static point => point.X);
            double top = corners.Min(static point => point.Y);
            return new Rect(left, top, corners.Max(static point => point.X) - left, corners.Max(static point => point.Y) - top);
        }

        private static bool IsAcceptable(Entry candidate, Entry query)
        {
            if (ReferenceEquals(candidate, query) || candidate.IsResolving)
            {
                return false;
            }

            for (Entry? ancestor = candidate.Parent; ancestor is not null; ancestor = ancestor.Parent)
            {
                if (ReferenceEquals(ancestor, query))
                {
                    return false;
                }
            }

            if (ReferenceEquals(candidate.ContainingBlock, query.ContainingBlock))
            {
                return !candidate.Node.ComputedStyle.IsOutOfFlow || candidate.Order < query.Order;
            }

            return candidate.ContainingBlock is Entry block && IsAcceptable(block, query);
        }

        private static Rect GetArea(Rect block, Rect anchor, PositionArea area)
        {
            (double left, double right) = GetTrack(block.X, block.Right, anchor.X, anchor.Right, area.Inline);
            (double top, double bottom) = GetTrack(block.Y, block.Bottom, anchor.Y, anchor.Bottom, area.Block);
            return new Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
        }

        private static (double Start, double End) GetTrack(double blockStart, double blockEnd, double start, double end, PositionAreaTrack track)
        {
            return track switch
            {
                PositionAreaTrack.Start => (Math.Min(blockStart, start), start),
                PositionAreaTrack.Center => (start, end),
                PositionAreaTrack.End => (end, Math.Max(blockEnd, end)),
                PositionAreaTrack.SpanStart => (Math.Min(blockStart, start), end),
                PositionAreaTrack.SpanEnd => (start, Math.Max(blockEnd, end)),
                _ => (Math.Min(blockStart, start), Math.Max(blockEnd, end))
            };
        }

        private static double Align(PositionAreaTrack track, double start, double length, double anchorStart, double anchorLength,
            double size, double startMargin, double endMargin, InsetValue startInset, InsetValue endInset)
        {
            double first = ResolveInset(startInset, length) ?? 0;
            double last = ResolveInset(endInset, length) ?? 0;
            return track switch
            {
                PositionAreaTrack.Start or PositionAreaTrack.SpanStart => start + length - size - endMargin - last,
                PositionAreaTrack.End or PositionAreaTrack.SpanEnd => start + startMargin + first,
                _ => anchorStart + (anchorLength - size + startMargin - endMargin + first - last) / 2
            };
        }

        private static PositionArea? TransformArea(PositionArea? area, PositionTryTactic tactic)
        {
            if (area is not { IsNone: false } value)
            {
                return area;
            }

            ReadOnlySpan<PositionTryTactic> order = [PositionTryTactic.FlipBlock, PositionTryTactic.FlipInline,
                PositionTryTactic.FlipStart, PositionTryTactic.FlipX, PositionTryTactic.FlipY];
            foreach (PositionTryTactic transform in order)
            {
                if (!tactic.HasFlag(transform))
                {
                    continue;
                }

                value = transform switch
                {
                    PositionTryTactic.FlipBlock or PositionTryTactic.FlipY => new PositionArea(FlipTrack(value.Block), value.Inline),
                    PositionTryTactic.FlipInline or PositionTryTactic.FlipX => new PositionArea(value.Block, FlipTrack(value.Inline)),
                    _ => new PositionArea(value.Inline, value.Block)
                };
            }

            return value;
        }

        private static ThicknessValue TransformMargin(ThicknessValue margin, PositionTryTactic tactic)
        {
            ReadOnlySpan<PositionTryTactic> order = [PositionTryTactic.FlipBlock, PositionTryTactic.FlipInline,
                PositionTryTactic.FlipStart, PositionTryTactic.FlipX, PositionTryTactic.FlipY];
            foreach (PositionTryTactic transform in order)
            {
                if (!tactic.HasFlag(transform))
                {
                    continue;
                }

                margin = transform switch
                {
                    PositionTryTactic.FlipBlock or PositionTryTactic.FlipY => new ThicknessValue(margin.Bottom, margin.Right, margin.Top, margin.Left),
                    PositionTryTactic.FlipInline or PositionTryTactic.FlipX => new ThicknessValue(margin.Top, margin.Left, margin.Bottom, margin.Right),
                    _ => new ThicknessValue(margin.Left, margin.Bottom, margin.Right, margin.Top)
                };
            }

            return margin;
        }

        private static (InsetValue Top, InsetValue Right, InsetValue Bottom, InsetValue Left) TransformInsets(ComputedStyle source, PositionTryTactic tactic)
        {
            (InsetValue Top, InsetValue Right, InsetValue Bottom, InsetValue Left) insets = (source.Top, source.Right, source.Bottom, source.Left);
            ReadOnlySpan<PositionTryTactic> order = [PositionTryTactic.FlipBlock, PositionTryTactic.FlipInline,
                PositionTryTactic.FlipStart, PositionTryTactic.FlipX, PositionTryTactic.FlipY];
            foreach (PositionTryTactic transform in order)
            {
                if (!tactic.HasFlag(transform))
                {
                    continue;
                }

                insets = transform switch
                {
                    PositionTryTactic.FlipBlock or PositionTryTactic.FlipY => (FlipInset(insets.Bottom, true), insets.Right, FlipInset(insets.Top, true), insets.Left),
                    PositionTryTactic.FlipInline or PositionTryTactic.FlipX => (insets.Top, FlipInset(insets.Left, false), insets.Bottom, FlipInset(insets.Right, false)),
                    _ => (SwapAxis(insets.Left), SwapAxis(insets.Bottom), SwapAxis(insets.Right), SwapAxis(insets.Top))
                };
            }

            return insets;
        }

        private static PositionAreaTrack FlipTrack(PositionAreaTrack track) => track switch
        {
            PositionAreaTrack.Start => PositionAreaTrack.End, PositionAreaTrack.End => PositionAreaTrack.Start,
            PositionAreaTrack.SpanStart => PositionAreaTrack.SpanEnd, PositionAreaTrack.SpanEnd => PositionAreaTrack.SpanStart, _ => track
        };

        private static InsetValue FlipInset(InsetValue inset, bool vertical)
        {
            if (!inset.IsAnchor)
            {
                return inset;
            }
            AnchorSide side = inset.AnchorSide switch
            {
                AnchorSide.Top when vertical => AnchorSide.Bottom, AnchorSide.Bottom when vertical => AnchorSide.Top,
                AnchorSide.Left when !vertical => AnchorSide.Right, AnchorSide.Right when !vertical => AnchorSide.Left,
                AnchorSide.Start => AnchorSide.End, AnchorSide.End => AnchorSide.Start,
                AnchorSide.SelfStart => AnchorSide.SelfEnd, AnchorSide.SelfEnd => AnchorSide.SelfStart, _ => inset.AnchorSide
            };
            return InsetValue.Anchor(side, inset.AnchorName);
        }

        private static InsetValue SwapAxis(InsetValue inset)
        {
            if (!inset.IsAnchor)
            {
                return inset;
            }
            return InsetValue.Anchor(inset.AnchorSide switch
            {
                AnchorSide.Top => AnchorSide.Left, AnchorSide.Left => AnchorSide.Top,
                AnchorSide.Bottom => AnchorSide.Right, AnchorSide.Right => AnchorSide.Bottom, _ => inset.AnchorSide
            }, inset.AnchorName);
        }

        private sealed class Entry(ILayoutNode node, Entry? parent, Entry? containingBlock, int order)
        {
            internal ILayoutNode Node { get; } = node;
            internal Entry? Parent { get; } = parent;
            internal Entry? ContainingBlock { get; } = containingBlock;
            internal int Order { get; } = order;
            internal Rect? Bounds { get; set; }
            internal Rect Block { get; set; }
            internal Point ParentOrigin { get; set; }
            internal Point ScrollOffset { get; set; }
            internal bool IsPending { get; set; }
            internal bool IsResolving { get; set; }
            internal bool IsResolved { get; set; }
        }
    }
}

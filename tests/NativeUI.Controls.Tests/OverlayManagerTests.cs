using NativeUI.Tests;
using NativeUI.Controls.MacOS;
using NativeUI.Core;
using NativeUI.Graphics;
using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Styling;

namespace NativeUI.Controls.Tests;

internal static class OverlayManagerTests
{
    public static void VerifyRootIsolationAndContainment()
    {
        using View firstRoot = new();
        using View secondRoot = new();
        using Window firstHost = new() { Content = firstRoot };
        using Window secondHost = new() { Content = secondRoot };
        OverlayManager firstManager = OverlayManager.FindFor(firstRoot) ?? throw new InvalidOperationException("Missing first manager.");
        OverlayManager secondManager = OverlayManager.FindFor(secondRoot) ?? throw new InvalidOperationException("Missing second manager.");
        Portal parent = new();
        Portal child = new();
        View item = new();
        View childItem = new();
        parent.Children.Add(item);
        parent.Children.Add(child);
        child.Children.Add(childItem);
        firstRoot.Children.Add(parent);
        Portal otherWindow = new();
        secondRoot.Children.Add(otherWindow);
        parent.Show();
        child.Show(item);
        otherWindow.Show();

        firstManager.HandlePointerPressed(childItem);
        Assert.True(parent.IsOpen);
        Assert.True(child.IsOpen);
        firstManager.HandlePointerPressed(parent);
        Assert.True(parent.IsOpen);
        Assert.False(child.IsOpen);
        firstManager.HandlePointerPressed(firstRoot);
        Assert.False(parent.IsOpen);
        Assert.True(otherWindow.IsOpen);
    }

    public static void VerifyStackingAndParentClosure()
    {
        using View root = new();
        using Window host = new() { Content = root };
        OverlayManager manager = OverlayManager.FindFor(root) ?? throw new InvalidOperationException("Missing hosted manager.");
        Portal first = new();
        Portal second = new();
        Portal child = new();
        first.Children.Add(child);
        root.Children.Add(first);
        root.Children.Add(second);
        second.Show();
        first.Show();
        child.Show();
        List<Portal> order = [first, child, second];

        manager.OrderOverlays(order);

        Assert.Same(second, order[0]);
        Assert.Same(first, order[1]);
        Assert.Same(child, order[2]);
        first.Close();
        Assert.False(child.IsOpen);
        Assert.True(second.IsOpen);
    }

    public static void VerifyReparentingAndTeardown()
    {
        using View firstRoot = new();
        using View secondRoot = new();
        using Window firstWindow = new() { Content = firstRoot };
        using Window secondWindow = new() { Content = secondRoot };
        View container = new();
        Portal overlay = new();
        container.Children.Add(overlay);
        firstRoot.Children.Add(container);
        overlay.Show();

        firstRoot.Children.Remove(container);
        secondRoot.Children.Add(container);
        OverlayManager.FindFor(firstRoot)?.HandlePointerPressed(firstRoot);
        Assert.True(overlay.IsOpen);
        secondWindow.Content = null;
        Assert.False(overlay.IsOpen);
    }

    public static void VerifyVisualTreeAttachment()
    {
        using AttachmentView root = new();
        AttachmentView subtree = new();
        AttachmentView leaf = new();
        Portal popup = new();
        subtree.Children.Add(leaf);
        subtree.Children.Add(popup);
        root.Children.Add(subtree);
        popup.Show();
        Assert.Equal(0, leaf.Attachments);

        using Window firstWindow = new() { Content = root };
        Assert.Equal(1, root.Attachments);
        Assert.Equal(1, subtree.Attachments);
        Assert.Equal(1, leaf.Attachments);
        OverlayManager.FindFor(root)?.HandlePointerPressed(root);
        Assert.False(popup.IsOpen);

        AttachmentView added = new();
        subtree.Children.Add(added);
        Assert.Equal(1, added.Attachments);
        subtree.Children.Remove(added);
        Assert.Equal(1, added.Detachments);

        using AttachmentView secondRoot = new();
        using Window secondWindow = new() { Content = secondRoot };
        popup.Show();
        root.Children.Remove(subtree);
        Assert.Equal(1, leaf.Detachments);
        OverlayManager.FindFor(root)?.HandlePointerPressed(root);
        Assert.True(popup.IsOpen);
        secondRoot.Children.Add(subtree);
        Assert.Equal(2, leaf.Attachments);
        OverlayManager.FindFor(secondRoot)?.HandlePointerPressed(secondRoot);
        Assert.False(popup.IsOpen);

        secondWindow.Content = null;
        Assert.Equal(2, leaf.Detachments);
        firstWindow.Content = null;
        Assert.Equal(1, root.Detachments);
        secondWindow.Content = root;
        Assert.Equal(2, root.Attachments);
        secondWindow.Content = null;
        Assert.Equal(2, root.Detachments);
    }

    public static void VerifyDedicatedDialogRoot()
    {
        using AttachmentView root = new();
        Dialog dialog = new();
        AttachmentView content = new();
        Portal popup = new();
        content.Children.Add(popup);
        dialog.Children.Add(content);
        root.Children.Add(dialog);
        using Window parentWindow = new() { Content = root };
        Assert.Same(root, content.VisualRoot ?? throw new InvalidOperationException("Missing visual root."));
        Assert.Equal(1, content.Attachments);

        using Window dialogWindow = new() { Content = dialog };
        Assert.Same(dialog, dialog.VisualRoot ?? throw new InvalidOperationException("Missing dialog root."));
        Assert.Same(dialog, content.VisualRoot ?? throw new InvalidOperationException("Missing content root."));
        Assert.Equal(2, content.Attachments);
        Assert.Equal(1, content.Detachments);
        popup.Show();
        OverlayManager.FindFor(root)?.HandlePointerPressed(root);
        Assert.True(popup.IsOpen);

        parentWindow.Content = null;
        Assert.Same(dialog, content.VisualRoot ?? throw new InvalidOperationException("Dialog lost its host."));
        Assert.Equal(1, content.Detachments);
        OverlayManager.FindFor(content)?.HandlePointerPressed(dialog);
        Assert.False(popup.IsOpen);
        dialogWindow.Content = null;
        Assert.False(content.IsAttachedToVisualTree);
        Assert.Equal(2, content.Detachments);
    }

    public static void VerifyDismissalCannotFocusReplacedContent()
    {
        using View root = new() { Width = 300, Height = 200 };
        Button target = new() { Width = 80, Height = 50 };
        Portal popup = new() { Width = 40, Height = 40 };
        root.Children.Add(target);
        root.Children.Add(popup);
        using Window window = new() { Width = 300, Height = 200, Content = root };
        popup.Show(new Point(220, 120));
        window.RenderFrame()?.Dispose();
        int focuses = 0;
        int presses = 0;
        target.GotFocus += (_, _) => focuses++;
        target.PointerPressed += (_, _) => presses++;
        View replacement = new();
        popup.Closed += (_, _) => window.Content = replacement;

        window.HandlePointerPressed(new Point(20, 20));

        Assert.Same(replacement, window.Content ?? throw new InvalidOperationException("Missing replacement content."));
        Assert.Equal(0, focuses);
        Assert.Equal(0, presses);
    }

    public static void VerifyAnchorRelationshipsAndReopening()
    {
        using View root = new();
        using Window host = new() { Content = root };
        OverlayManager manager = OverlayManager.FindFor(root) ?? throw new InvalidOperationException("Missing hosted manager.");
        Portal parent = new();
        Portal child = new();
        View anchor = new();
        View childContent = new();
        parent.Children.Add(anchor);
        child.Children.Add(childContent);
        root.Children.Add(parent);
        root.Children.Add(child);
        child.Show(anchor);
        parent.Show();
        List<Portal> order = [parent, child];

        manager.OrderOverlays(order);
        Assert.Same(child, order[0]);
        Assert.Same(parent, order[1]);
        manager.OrderForLayout(order);
        Assert.Same(parent, order[0]);
        Assert.Same(child, order[1]);
        manager.HandlePointerPressed(childContent);
        Assert.True(parent.IsOpen);
        Assert.True(child.IsOpen);
        child.Close();
        child.Show(anchor);
        manager.OrderOverlays(order);
        Assert.Same(parent, order[0]);
        Assert.Same(child, order[1]);
        parent.Close();
        Assert.False(child.IsOpen);
    }

    public static void VerifyIndependentDismissalPolicy()
    {
        using View root = new();
        using Window host = new() { Content = root };
        OverlayManager manager = OverlayManager.FindFor(root) ?? throw new InvalidOperationException("Missing hosted manager.");
        Portal background = new();
        PersistentPortal persistent = new();
        root.Children.Add(background);
        root.Children.Add(persistent);
        background.Show();
        persistent.Show();

        manager.HandlePointerPressed(root);

        Assert.False(background.IsOpen);
        Assert.True(persistent.IsOpen);
    }

    public static void VerifyRenderedOverlayPressDismissal()
    {
        using View root = new() { Width = 300, Height = 200 };
        Portal popup = new() { Width = 60, Height = 40 };
        root.Children.Add(popup);
        using Window window = new() { Width = 300, Height = 200, Content = root };
        popup.Show(new Point(180, 120));
        ConsumingOverlay overlay = new();
        window.Overlays.Add(overlay);
        window.RenderFrame()?.Dispose();

        window.HandlePointerPressed(new Point(20, 20));

        Assert.True(overlay.WasPressed);
        Assert.False(popup.IsOpen);
    }

    public static void VerifyScrollbarPressDismissal()
    {
        using View root = new() { Style = new Style { Width = 120, Height = 80, OverflowY = Overflow.Auto } };
        root.Children.Add(new View { Width = 120, Height = 300 });
        Portal popup = new() { Width = 60, Height = 40 };
        root.Children.Add(popup);
        using Window window = new() { Width = 300, Height = 200, Content = root };
        popup.Show(new Point(200, 120));
        window.RenderFrame()?.Dispose();
        LayoutBox layout = window.LastLayout ?? throw new InvalidOperationException("Expected rendered layout.");
        IReadOnlyList<ScrollbarGeometry> scrollbars = VisualTreeRenderer.GetScrollbars(layout);
        Assert.Equal(1, scrollbars.Count);
        Rect track = scrollbars[0].Track;

        window.HandlePointerPressed(new Point(track.X + track.Width / 2, track.Bottom - 1));

        Assert.False(popup.IsOpen);
        Assert.True(root.VerticalOffset > 0);
    }

    public static void VerifyDismissalDoesNotRetargetPress()
    {
        using View root = new() { Width = 300, Height = 200 };
        Portal targetPopup = new() { Width = 80, Height = 50 };
        Text target = new() { Value = "Target", Width = 80, Height = 50, IsTextSelectionEnabled = true };
        targetPopup.NativeHandle = new DeferredExitPeer();
        targetPopup.Children.Add(target);
        Portal dismissedPopup = new() { Width = 40, Height = 40 };
        root.Children.Add(targetPopup);
        root.Children.Add(dismissedPopup);
        using Window window = new() { Width = 300, Height = 200, Content = root };
        targetPopup.Show(new Point(50, 50));
        dismissedPopup.Show(new Point(220, 120));
        window.RenderFrame()?.Dispose();
        int targetPresses = 0;
        int rootPresses = 0;
        target.PointerPressed += (_, _) => targetPresses++;
        root.PointerPressed += (_, _) => rootPresses++;
        dismissedPopup.Closed += (_, _) => targetPopup.Close();
        window.HandlePointerMoved(new Point(70, 70), isPressed: false);
        Assert.True(window.IsTextCursorActive);

        window.HandlePointerPressed(new Point(70, 70));

        Assert.False(dismissedPopup.IsOpen);
        Assert.False(targetPopup.IsOpen);
        Assert.Equal(0, targetPresses);
        Assert.Equal(0, rootPresses);
        Assert.True(targetPopup.IsVisible);
        Assert.False(window.IsTextCursorActive);
    }

    public static void VerifyFocusCallbackCannotDispatchIntoClosedPopup()
    {
        using View root = new() { Width = 300, Height = 200 };
        Portal popup = new() { Width = 80, Height = 50 };
        Button target = new() { Width = 80, Height = 50, Text = "Target" };
        popup.Children.Add(target);
        root.Children.Add(popup);
        using Window window = new() { Width = 300, Height = 200, Content = root };
        popup.Show(new Point(50, 50));
        window.RenderFrame()?.Dispose();
        int presses = 0;
        target.PointerPressed += (_, _) => presses++;
        target.GotFocus += (_, _) => popup.Close();

        window.HandlePointerPressed(new Point(70, 70));

        Assert.False(popup.IsOpen);
        Assert.Equal(0, presses);
    }

    public static void VerifyDismissalUsesStableRelationships()
    {
        using View root = new();
        using Window host = new() { Content = root };
        OverlayManager manager = OverlayManager.FindFor(root) ?? throw new InvalidOperationException("Missing hosted manager.");
        Portal parent = new();
        View anchor = new();
        parent.Children.Add(anchor);
        Portal child = new();
        View childContent = new();
        child.Children.Add(childContent);
        Portal outside = new();
        View otherAnchor = new();
        root.Children.Add(parent);
        root.Children.Add(child);
        root.Children.Add(outside);
        root.Children.Add(otherAnchor);
        parent.Show();
        child.Show(anchor);
        outside.Show();
        outside.Closed += (_, _) => child.Show(otherAnchor);

        manager.HandlePointerPressed(childContent);

        Assert.False(outside.IsOpen);
        Assert.True(child.IsOpen);
        Assert.True(parent.IsOpen);
    }

    public static void VerifyFallbackRejectsCycles()
    {
        using View root = new();
        using Window host = new() { Content = root };
        OverlayManager manager = OverlayManager.FindFor(root) ?? throw new InvalidOperationException("Missing hosted manager.");
        Portal first = new();
        Portal second = new();
        Portal nested = new();
        second.Children.Add(nested);
        root.Children.Add(first);
        root.Children.Add(second);
        first.Show(nested);
        second.Show(first);
        nested.Show();
        List<Portal> order = [second, first, nested];

        manager.OrderForLayout(order);

        Assert.Equal(3, order.Count);
        Assert.Same(nested, order[0]);
        Assert.Same(first, order[1]);
        Assert.Same(second, order[2]);
    }

    public static void VerifyExitKeepsStackPosition()
    {
        using View root = new();
        using Window host = new() { Content = root };
        OverlayManager manager = OverlayManager.FindFor(root) ?? throw new InvalidOperationException("Missing hosted manager.");
        Portal below = new();
        Portal exiting = new();
        Portal above = new();
        DeferredExitPeer peer = new();
        exiting.NativeHandle = peer;
        root.Children.Add(below);
        root.Children.Add(exiting);
        root.Children.Add(above);
        below.Show();
        exiting.Show();
        above.Show();
        exiting.Close();
        Assert.True(exiting.IsVisible);
        List<Portal> order = [above, exiting, below];

        manager.OrderOverlays(order);

        Assert.Same(below, order[0]);
        Assert.Same(exiting, order[1]);
        Assert.Same(above, order[2]);
        peer.CompleteExit();
        Assert.False(exiting.IsVisible);
        order = [above, exiting, below];
        manager.OrderOverlays(order);
        Assert.Same(exiting, order[1]);
    }

    public static void VerifyLaterOpeningRegistersBeforeVisibilityObservers()
    {
        using View root = new();
        using Window host = new() { Content = root };
        OverlayManager manager = OverlayManager.FindFor(root) ?? throw new InvalidOperationException("Missing hosted manager.");
        Portal previous = new();
        Portal later = new();
        later.SetValue(UIElement.IsVisibleProperty, false);
        root.Children.Add(previous);
        root.Children.Add(later);
        previous.Show();
        later.SetValue(UIElement.IsVisibleProperty, true);
        bool observed = false;
        later.VisibilityChanged += (_, _) =>
        {
            if (later.IsOpen)
            {
                List<Portal> order = [later, previous];
                manager.OrderOverlays(order);
                Assert.Same(previous, order[0]);
                Assert.Same(later, order[1]);
                observed = true;
            }
        };

        later.Show();

        Assert.True(observed);
    }

    public static void VerifyDynamicReanchoring()
    {
        using View root = new();
        Portal first = new();
        Portal second = new();
        View firstAnchor = new();
        View secondAnchor = new();
        View outside = new();
        first.Children.Add(firstAnchor);
        second.Children.Add(secondAnchor);
        root.Children.Add(first);
        root.Children.Add(second);
        root.Children.Add(outside);
        using Window host = new() { Content = root };
        OverlayManager manager = OverlayManager.FindFor(root) ?? throw new InvalidOperationException("Missing hosted manager.");
        second.Show();
        first.Show(secondAnchor);
        second.Show(firstAnchor);
        first.Show(outside);
        List<Portal> order = [second, first];
        manager.OrderForLayout(order);
        Assert.Same(first, order[0]);
        Assert.Same(second, order[1]);

        for (int i = 0; i < 5; i++)
        {
            second.Show(outside);
            first.Show(secondAnchor);
            order = [first, second];
            manager.OrderForLayout(order);
            Assert.Same(second, order[0]);
            first.Show(outside);
            second.Show(firstAnchor);
            order = [second, first];
            manager.OrderForLayout(order);
            Assert.Same(first, order[0]);
        }

        second.Show(outside);
        first.Close();
        Assert.True(second.IsOpen);

        root.Children.Remove(second);
        first.Children.Add(second);
        first.Show();
        second.Show();
        first.Children.Remove(second);
        root.Children.Add(second);
        first.Close();
        Assert.True(second.IsOpen);
        first.Show();
        manager.HandlePointerPressed(outside);
        Assert.False(first.IsOpen);
        Assert.False(second.IsOpen);
    }

    public static void VerifyDetachedLookup()
    {
        using View root = new();
        View child = new();
        root.Children.Add(child);
        using Window host = new() { Content = root };
        Assert.True(OverlayManager.FindFor(child) is not null);
        root.Children.Remove(child);
        Assert.True(OverlayManager.FindFor(child) is null);
        host.Content = null;
        Assert.True(OverlayManager.FindFor(root) is null);
    }

    public static void VerifyPopupAttributeOwnership()
    {
        using View root = new();
        Button target = new();
        root.Children.Add(target);
        Popover popover = new() { Target = target, HasArrow = true, Content = new Text { Value = "Content" } };
        root.Children.Add(popover);
        using Window host = new() { Content = root };
        Assert.Null(root.GetAttribute("data-popup-open"));
        Assert.False(new CompoundSelector(null, []).Attribute("data-popup-open").Matches(target));
        popover.IsOpen = true;
        Assert.True(new CompoundSelector(null, []).Attribute("data-popup-open").Matches(target));
        UIElement previousArrow = popover.ArrowPart ?? throw new InvalidOperationException("Missing arrow.");
        Assert.Equal("bottom", previousArrow.GetAttribute("data-side"));
        Assert.Equal("center", previousArrow.GetAttribute("data-align"));
        popover.Side = PortalSide.Left;
        popover.Align = Alignment.Start;
        Assert.Equal("left", previousArrow.GetAttribute("data-side"));
        Assert.Equal("start", previousArrow.GetAttribute("data-align"));
        View replacement = new();
        popover.Arrow = replacement;
        Assert.Null(previousArrow.GetAttribute("data-side"));
        Assert.Null(previousArrow.GetAttribute("data-align"));
        Assert.Equal("left", replacement.GetAttribute("data-side"));
        popover.IsOpen = false;
        Assert.False(new CompoundSelector(null, []).Attribute("data-popup-open").Matches(target));
        Assert.Null(replacement.GetAttribute("data-side"));

        Tooltip tooltip = new() { Target = target, HasArrow = true, Content = new Text { Value = "Help" } };
        root.Children.Add(tooltip);
        InputManager.Current.PrimaryKeyboardDevice.ClearFocus();
        Assert.True(target.Focus(FocusState.Keyboard));
        Assert.True(tooltip.IsOpen);
        Assert.True(new CompoundSelector(null, []).Attribute("data-popup-open").Matches(target));
        UIElement tooltipArrow = tooltip.ArrowPart ?? throw new InvalidOperationException("Missing tooltip arrow.");
        Assert.Equal("top", tooltipArrow.GetAttribute("data-side"));
        tooltip.Side = PortalSide.Right;
        tooltip.Align = Alignment.End;
        Assert.Equal("right", tooltipArrow.GetAttribute("data-side"));
        Assert.Equal("end", tooltipArrow.GetAttribute("data-align"));
        tooltip.ContentPart.Close();
        Assert.False(new CompoundSelector(null, []).Attribute("data-popup-open").Matches(target));
        Assert.Null(tooltipArrow.GetAttribute("data-side"));
    }

    private sealed class AttachmentView : View
    {
        internal int Attachments { get; private set; }

        internal int Detachments { get; private set; }

        protected override void OnAttachedToVisualTree()
        {
            base.OnAttachedToVisualTree();
            Attachments++;
        }

        protected override void OnDetachedFromVisualTree()
        {
            Detachments++;
            base.OnDetachedFromVisualTree();
        }
    }

    private sealed class DeferredExitPeer : INativeHandle, IPortalPeer
    {
        private Action? _completeExit;

        public void RequestAnimationFrame(Action callback) => callback();

        public void WaitForAnimations(Action callback) => _completeExit = callback;

        public void CompleteExit()
        {
            Action? callback = _completeExit;
            _completeExit = null;
            callback?.Invoke();
        }

        public void Dispose()
        {
            _completeExit = null;
        }
    }

    private sealed class ConsumingOverlay : IRenderOverlay
    {
        public bool WasPressed { get; private set; }

        public void Draw(DrawingContext context, RenderPass pass)
        {
        }

        public bool ContainsPoint(Point point) => new Rect(0, 0, 50, 50).Contains(point);

        public bool HandlePointer(OverlayPointerAction action, Point point)
        {
            if (action == OverlayPointerAction.Pressed && ContainsPoint(point))
            {
                WasPressed = true;
                return true;
            }

            return false;
        }
    }

    private sealed class PersistentPortal : Portal
    {
        internal override bool ClosesOnOutsidePress => false;
    }
}

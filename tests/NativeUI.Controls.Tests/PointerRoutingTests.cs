using NativeUI.Controls.MacOS;
using NativeUI.Core;
using NativeUI.Css;
using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Styling;
using NativeUI.Tests;

namespace NativeUI.Controls.Tests;

/// <summary>
/// Verifies routed event delivery, hit-testing, hover state, cursor requests, and drag behavior.
/// </summary>
internal static class PointerRoutingTests
{
    private static readonly RoutedEvent<RoutedEventArgs> s_tunnelEvent =
        RoutedEvent.Register<UIElement, RoutedEventArgs>("TestTunnel", RoutingStrategy.Tunnel);

    private static readonly RoutedEvent<RoutedEventArgs> s_bubbleEvent =
        RoutedEvent.Register<UIElement, RoutedEventArgs>("TestBubble", RoutingStrategy.Bubble);

    private static readonly RoutedEvent<RoutedEventArgs> s_directEvent =
        RoutedEvent.Register<UIElement, RoutedEventArgs>("TestDirect", RoutingStrategy.Direct);

    internal static void VerifyRoutingOrder()
    {
        using View root = new();
        View middle = new();
        Text leaf = new();
        middle.Children.Add(leaf);
        root.Children.Add(middle);

        List<string> order = [];
        foreach ((UIElement element, string name) in new (UIElement, string)[] { (root, "root"), (middle, "middle"), (leaf, "leaf") })
        {
            element.AddHandler(s_bubbleEvent, (_, _) => order.Add(name));
            element.AddHandler(s_tunnelEvent, (_, _) => order.Add(name));
            element.AddHandler(s_directEvent, (_, _) => order.Add(name));
        }

        leaf.RaiseEvent(s_bubbleEvent, new RoutedEventArgs());
        Assert.Equal("leaf,middle,root", string.Join(',', order));

        order.Clear();
        leaf.RaiseEvent(s_tunnelEvent, new RoutedEventArgs());
        Assert.Equal("root,middle,leaf", string.Join(',', order));

        order.Clear();
        leaf.RaiseEvent(s_directEvent, new RoutedEventArgs());
        Assert.Equal("leaf", string.Join(',', order));
    }

    internal static void VerifyHandledSourceAndRemoval()
    {
        View root = new();
        Text leaf = new();
        root.Children.Add(leaf);

        object? rootSender = null;
        object? rootSource = null;
        int ordinaryCalls = 0;
        int handledTooCalls = 0;
        EventHandler<RoutedEventArgs> ordinary = (_, _) => ordinaryCalls++;

        leaf.AddHandler(s_bubbleEvent, (_, args) => args.Handled = true);
        root.AddHandler(s_bubbleEvent, ordinary);
        root.AddHandler(s_bubbleEvent, (sender, args) =>
        {
            handledTooCalls++;
            rootSender = sender;
            rootSource = args.Source;
        }, handledEventsToo: true);

        leaf.RaiseEvent(s_bubbleEvent, new RoutedEventArgs());
        Assert.Equal(0, ordinaryCalls);
        Assert.Equal(1, handledTooCalls);
        Assert.Same(root, rootSender!);
        Assert.Same(leaf, rootSource!);

        root.RaiseEvent(s_bubbleEvent, new RoutedEventArgs());
        Assert.Equal(1, ordinaryCalls);

        root.RemoveHandler(s_bubbleEvent, ordinary);
        root.RaiseEvent(s_bubbleEvent, new RoutedEventArgs());
        Assert.Equal(1, ordinaryCalls);

        root.Dispose();
        int callsBeforeDisposedRaise = handledTooCalls;
        root.RaiseEvent(s_bubbleEvent, new RoutedEventArgs());
        Assert.Equal(callsBeforeDisposedRaise, handledTooCalls);
    }

    internal static void VerifyDispatchSnapshots()
    {
        using View root = new();
        using View otherParent = new();
        Text leaf = new();
        root.Children.Add(leaf);

        int rootCalls = 0;
        int addedCalls = 0;
        int removedCalls = 0;
        EventHandler<RoutedEventArgs> removed = (_, _) => removedCalls++;
        EventHandler<RoutedEventArgs> added = (_, _) => addedCalls++;
        root.AddHandler(s_bubbleEvent, (_, _) => rootCalls++);
        leaf.AddHandler(s_bubbleEvent, (_, _) =>
        {
            leaf.RemoveHandler(s_bubbleEvent, removed);
            leaf.AddHandler(s_bubbleEvent, added);
            root.Children.Remove(leaf);
            otherParent.Children.Add(leaf);
        });
        leaf.AddHandler(s_bubbleEvent, removed);

        leaf.RaiseEvent(s_bubbleEvent, new RoutedEventArgs());
        Assert.Equal(1, removedCalls);
        Assert.Equal(0, addedCalls);
        Assert.Equal(1, rootCalls);
        Assert.Same(otherParent, leaf.Parent!);

        leaf.RaiseEvent(s_bubbleEvent, new RoutedEventArgs());
        Assert.Equal(1, removedCalls);
        Assert.Equal(1, addedCalls);
    }

    internal static void VerifyHoverOrderAndSelectors()
    {
        CssStyleRegistry.Register(new CssStyleSheet(nameof(HoverView), string.Empty,
        [
            new CssRule(CssSelector.Class("card"), new Dictionary<string, CssValue>
            {
                ["color"] = new CssColorValue(Colors.Green)
            }),
            new CssRule(
                CssSelector.Descendant(
                    CssSelector.Class("card").State(ElementStates.Hover),
                    CssSelector.Class("title")),
                new Dictionary<string, CssValue>
                {
                    ["color"] = new CssColorValue(Colors.Blue)
                }),
            new CssRule(
                CssSelector.Class("title").State(ElementStates.Hover),
                new Dictionary<string, CssValue>
                {
                    ["font-size"] = new CssLengthValue(20)
                })
        ]));

        using HoverView root = new() { Class = "card" };
        View nested = new();
        Text title = new() { Class = "title", Value = "Title" };
        Text sibling = new() { Value = "Sibling" };
        nested.Children.Add(title);
        root.Children.Add(nested);
        root.Children.Add(sibling);
        using Window window = new() { Width = 300, Height = 200, Content = root };

        List<string> order = [];
        Color? titleColorAtEnter = null;
        root.PointerEntered += (_, _) => order.Add("enter root");
        nested.PointerEntered += (_, _) => order.Add("enter nested");
        title.PointerEntered += (_, _) =>
        {
            order.Add("enter title");
            titleColorAtEnter = title.ComputedStyle.TextColor;
        };
        root.PointerExited += (_, _) => order.Add("exit root");
        nested.PointerExited += (_, _) => order.Add("exit nested");
        title.PointerExited += (_, _) => order.Add("exit title");

        Assert.Equal(Colors.Green, title.ComputedStyle.TextColor);

        window.HandlePointerMoved(GetPointIn(window, title), isPressed: false);
        Assert.Equal("enter root,enter nested,enter title", string.Join(',', order));
        Assert.Equal<Color?>(Colors.Blue, titleColorAtEnter);
        Assert.Equal(Colors.Blue, title.ComputedStyle.TextColor);
        Assert.Equal(20.0, title.ComputedStyle.FontSize);

        order.Clear();
        window.HandlePointerMoved(GetPointIn(window, sibling), isPressed: false);
        Assert.Equal("exit title,exit nested", string.Join(',', order));
        Assert.Equal(Colors.Blue, title.ComputedStyle.TextColor);
        Assert.Equal(16.0, title.ComputedStyle.FontSize);

        order.Clear();
        window.HandlePointerExited();
        Assert.Equal("exit root", string.Join(',', order));
        Assert.Equal(Colors.Green, title.ComputedStyle.TextColor);
        Assert.Equal(ElementStates.None, root.States);
    }

    internal static void VerifyHitTestingAndSingleDispatch()
    {
        using View root = new();
        Text covered = new() { Value = "Covered", Style = new Style { Height = 40 } };
        Text top = new() { Value = "Top", Style = new Style { Height = 40, Margin = new Thickness(-40, 0, 0, 0) } };
        View hiddenParent = new() { IsVisible = false };
        Text hiddenChild = new() { Value = "Hidden" };
        hiddenParent.Children.Add(hiddenChild);
        root.Children.Add(covered);
        root.Children.Add(top);
        root.Children.Add(hiddenParent);
        using Window window = new() { Width = 300, Height = 200, Content = root };

        int coveredCalls = 0;
        int topCalls = 0;
        int rootPressed = 0;
        int rootMoved = 0;
        int rootReleased = 0;
        int hiddenCalls = 0;
        object? source = null;
        object? sender = null;
        PointerEventArgs? pressedArgs = null;
        covered.PointerPressed += (_, _) => coveredCalls++;
        top.PointerPressed += (_, _) => topCalls++;
        hiddenChild.PointerEntered += (_, _) => hiddenCalls++;
        root.PointerMoved += (_, _) => rootMoved++;
        root.PointerReleased += (_, _) => rootReleased++;
        root.PointerPressed += (s, args) =>
        {
            rootPressed++;
            sender = s;
            source = args.Source;
            pressedArgs = args;
        };

        Point point = new(5, 20);
        window.HandlePointerMoved(point, isPressed: false);
        window.HandlePointerPressed(point, clickCount: 2, PointerModifiers.Shift);
        window.HandlePointerReleased(point);

        Assert.Equal(0, coveredCalls);
        Assert.Equal(1, topCalls);
        Assert.Equal(1, rootPressed);
        Assert.Equal(1, rootMoved);
        Assert.Equal(1, rootReleased);
        Assert.Equal(0, hiddenCalls);
        Assert.Same(root, sender!);
        Assert.Same(top, source!);
        Assert.Equal(2, pressedArgs!.ClickCount);
        Assert.Equal(PointerButtons.Primary, pressedArgs.Buttons);
        Assert.Equal(PointerModifiers.Shift, pressedArgs.Modifiers);
        Assert.Equal(point, pressedArgs.Position);
    }

    internal static void VerifyStationaryPointerRefresh()
    {
        using View root = new();
        Text first = new() { Value = "First", Style = new Style { Height = 40 } };
        Text second = new() { Value = "Second", Style = new Style { Height = 40 } };
        root.Children.Add(first);
        root.Children.Add(second);
        using Window window = new() { Width = 300, Height = 200, Content = root };

        int firstExits = 0;
        int secondEnters = 0;
        first.PointerExited += (_, _) => firstExits++;
        second.PointerEntered += (_, _) => secondEnters++;

        window.HandlePointerMoved(new Point(5, 20), isPressed: false);
        int rendersBefore = window.RenderCount;

        first.IsVisible = false;

        Assert.Equal(1, firstExits);
        Assert.Equal(1, secondEnters);
        Assert.True(window.RenderCount - rendersBefore <= 2, "Hover refresh must not render recursively.");

        window.Content = null;
        Assert.False(window.IsTextCursorActive);
        Assert.Equal(ElementStates.None, second.States);
    }

    internal static void VerifyCursorAndSelectionDrag()
    {
        using View root = new();
        Text selectable = new() { Value = "Selectable text content", Style = new Style { Height = 40 } };
        Text plain = new() { Value = "Plain", IsTextSelectionEnabled = false, Style = new Style { Height = 40 } };
        root.Children.Add(selectable);
        root.Children.Add(plain);
        using Window window = new() { Width = 300, Height = 200, Content = root };

        Point onSelectable = new(5, 20);
        Point onPlain = new(5, 60);
        Point outside = new(280, 190);

        window.HandlePointerMoved(onSelectable, isPressed: false);
        Assert.True(window.IsTextCursorActive);

        window.HandlePointerMoved(onPlain, isPressed: false);
        Assert.False(window.IsTextCursorActive);

        window.HandlePointerPressed(onSelectable);
        Assert.True(window.IsTextCursorActive);

        window.HandlePointerMoved(outside, isPressed: true);
        Assert.True(selectable.HasSelection);
        Assert.Equal(ElementStates.None, selectable.States);
        Assert.True(window.IsTextCursorActive);

        window.HandlePointerExited();
        Assert.True(window.IsTextCursorActive);

        window.HandlePointerReleased(outside);
        Assert.False(window.IsTextCursorActive);

        window.HandlePointerPressed(onSelectable);
        window.HandlePointerCancelled();
        Assert.False(window.IsTextCursorActive);
        Assert.Equal(ElementStates.None, selectable.States);

        int selectionLength = selectable.SelectionLength;
        window.HandlePointerMoved(outside, isPressed: true);
        Assert.Equal(selectionLength, selectable.SelectionLength);

        window.HandlePointerMoved(onSelectable, isPressed: false);
        Assert.True(window.IsTextCursorActive);
        selectable.IsTextSelectionEnabled = false;
        Assert.False(window.IsTextCursorActive);
    }

    private static Point GetPointIn(Window window, UIElement element)
    {
        LayoutBox box = FindBox(window.LastLayout!, element)
            ?? throw new InvalidOperationException("The element has no layout box.");
        return new Point(box.BorderBox.X + 2, box.BorderBox.Y + (box.BorderBox.Height / 2));
    }

    private static readonly RoutedEvent<RoutedEventArgs> s_observableEvent =
        RoutedEvent.Register<TestObservableElement, RoutedEventArgs>("TestObservable", RoutingStrategy.Direct);

    internal static void VerifyRoutedEventHandlerStoreLifecycle()
    {
        int observeStarts = 0;
        int observeStops = 0;

        EventManager.RegisterEventObserver<TestObservableElement>(
            s_observableEvent,
            _ =>
            {
                observeStarts++;
                return new ObservationToken(() => observeStops++);
            });

        TestObservableElement element = new();
        Assert.False(element.HasHandler(s_observableEvent));
        Assert.Equal(0, observeStarts);

        EventHandler<RoutedEventArgs> handler1 = (_, _) => { };
        EventHandler<RoutedEventArgs> handler2 = (_, _) => { };

        element.AddHandler(s_observableEvent, handler1);
        Assert.True(element.HasHandler(s_observableEvent));
        Assert.Equal(1, observeStarts);
        Assert.Equal(0, observeStops);

        element.AddHandler(s_observableEvent, handler2);
        Assert.Equal(1, observeStarts);
        Assert.Equal(0, observeStops);

        element.RemoveHandler(s_observableEvent, handler1);
        Assert.True(element.HasHandler(s_observableEvent));
        Assert.Equal(1, observeStarts);
        Assert.Equal(0, observeStops);

        element.RemoveHandler(s_observableEvent, handler2);
        Assert.False(element.HasHandler(s_observableEvent));
        Assert.Equal(1, observeStarts);
        Assert.Equal(1, observeStops);

        element.AddHandler(s_observableEvent, handler1);
        Assert.Equal(2, observeStarts);
        Assert.Equal(1, observeStops);

        element.Dispose();
        Assert.Equal(2, observeStops);
    }

    private static LayoutBox? FindBox(LayoutBox box, UIElement element)
    {
        if (ReferenceEquals(box.Element, element))
        {
            return box;
        }

        foreach (LayoutBox child in box.Children)
        {
            if (FindBox(child, element) is LayoutBox match)
            {
                return match;
            }
        }

        return null;
    }

    private sealed class HoverView : View;

    private sealed class TestObservableElement : View;

    private sealed class ObservationToken(Action onDispose) : IDisposable
    {
        private readonly Action _onDispose = onDispose;

        public void Dispose()
        {
            _onDispose();
        }
    }
}

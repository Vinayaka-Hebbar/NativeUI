using System.Collections.ObjectModel;
using NativeUI.Controls;
using NativeUI.Controls.Web;
using NativeUI.Core;
using NativeUI.Css;
using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Sample;
using NativeUI.Styling;
using NativeUI.Tests;

// Verifies proxy state and ownership on the .NET host without creating browser DOM elements.
int exitCode = TestRunner.RunSuite("NativeUI.Controls.Web.Tests",
[
    ("HelloView mounts its proxy hierarchy and performs targeted incremental updates", TestIncrementalUpdates),
    ("DOM proxies synchronize text and keep their identity across property changes", TestPropertyChanges),
    ("View proxies reconcile children without recreating unchanged proxies", TestChildCollections),
    ("WebHost unmounts subscriptions and proxies before remounting", TestMountLifetime),
    ("WebHost rejects elements mounted by another host without disposing their proxies", TestHostOwnership),
    ("Disposing a control disposes its proxy and the proxies of its children", TestControlDisposal),
    ("Each browser pointer event routes once and updates hover in C#", TestPointerEventRouting),
    ("Unmounting clears hover state and pointer node mappings", TestPointerCleanup),
    ("C# stylesheets translate to scoped browser CSS with matching specificity", TestStyleSheetCss),
    ("Only elements matching a custom selector type receive a type token", TestStyleSheetTypeTokens),
    ("Generated selectors use the renderer's tag mapping and reject untranslatable parts", TestStyleSheetTypeMapping),
    ("View-scoped stylesheets generate view-scoped browser CSS rules", TestViewScopedStyleSheetCss),
    ("View-scoped stylesheets manage view scope attributes on DOM mount and update", TestViewScopedStyleSheetLifecycle),
    ("View stylesheet changes between compiled, scoped, and empty sheets keep scope state consistent", TestViewStyleSheetTransitions),
    ("The base stylesheet writes zero-specificity block, button, and color-scheme rules", TestBaseStyleSheetCss),
    ("Button mounts as HTML button element and synchronizes disabled and text attributes", TestButtonDomProxy)
]);

return exitCode;

static void TestIncrementalUpdates()
{
    HelloViewModel viewModel = new();
    HelloView view = new()
    {
        DataContext = viewModel
    };

    using HtmlDomRoot domRoot = new();
    domRoot.MountHeadless(view);

    HtmlDomElement root = GetElement(view);
    HtmlDomElement title = GetElement(view.TitleText);
    Assert.Equal("div", root.TagName);
    Assert.Equal("card", root.ClassName);
    Assert.Equal("span", title.TagName);
    Assert.Equal("title", title.ClassName);

    viewModel.Title = "Incremental Web Update!";
    Assert.Equal("Incremental Web Update!", title.TextContent);
    Assert.Same(title, GetElement(view.TitleText));
}

static void TestPropertyChanges()
{
    Style original = new() { FontSize = 18, Color = Colors.Blue };
    using Text text = new() { Id = "title", AriaLabel = "Heading", Value = "Initial", Style = original };
    using HtmlDomRoot host = new();
    host.MountHeadless(text);
    HtmlDomElement element = GetElement(text);
    Assert.True(element is HtmlDomText);
    Assert.Equal("span", element.TagName);
    Assert.Equal("Initial", element.TextContent);

    original.FontSize = 24;
    text.Id = null;
    text.AriaLabel = null;
    text.Style = new() { Padding = new Thickness(12, 8) };
    text.IsVisible = false;
    text.IsTextSelectionEnabled = false;
    Assert.Same(element, GetElement(text));

    text.Value = "Updated";
    Assert.Equal("Updated", element.TextContent);
    Assert.Same(element, GetElement(text));
}

static void TestChildCollections()
{
    using View root = new();
    Text first = new() { Value = "First" };
    Text second = new() { Value = "Second" };
    root.Children.Add(first);
    root.Children.Add(second);
    using HtmlDomRoot host = new();
    host.MountHeadless(root);
    Assert.True(root.NativePeer is HtmlDomView);
    HtmlDomElement firstElement = GetElement(first);
    HtmlDomElement secondElement = GetElement(second);

    ObservableCollection<UIElement> children = (ObservableCollection<UIElement>)root.Children;
    children.Move(1, 0);
    Assert.Same(firstElement, GetElement(first));
    Assert.Same(secondElement, GetElement(second));
    Assert.Same(root, second.Parent ?? throw new InvalidOperationException("Moved child lost its parent."));

    Text inserted = new() { Value = "Inserted" };
    children.Insert(1, inserted);
    HtmlDomElement insertedElement = GetElement(inserted);
    Assert.Equal("Inserted", insertedElement.TextContent);
    Assert.Same(firstElement, GetElement(first));
    children.Remove(second);
    Assert.Null(second.NativePeer);
    Assert.Null(second.Parent);
    AssertDisposed(() => secondElement.SetTextContent("Invalid"));

    children.Clear();
    Assert.Null(first.NativePeer);
    Assert.Null(inserted.NativePeer);
    Assert.Null(first.Parent);
    Assert.Null(inserted.Parent);
    AssertDisposed(() => firstElement.SetTextContent("Invalid"));
    AssertDisposed(() => insertedElement.SetTextContent("Invalid"));
    first.Dispose();
    second.Dispose();
    inserted.Dispose();
}

static void TestMountLifetime()
{
    using View original = new();
    Style style = new() { FontSize = 18 };
    Text child = new() { Style = style };
    original.Children.Add(child);
    using Text replacement = new() { Value = "Replacement" };
    using HtmlDomRoot host = new();
    host.MountHeadless(original);
    HtmlDomElement originalElement = GetElement(child);
    host.MountHeadless(replacement);
    Assert.Null(original.NativePeer);
    Assert.Null(child.NativePeer);
    AssertDisposed(() => originalElement.SetTextContent("Invalid"));
    child.Value = "Detached";
    style.FontSize = 20;
    Text detachedChild = new();
    original.Children.Add(detachedChild);
    Assert.Null(detachedChild.NativePeer);
    Assert.Equal("Replacement", GetElement(replacement).TextContent);

    host.Unmount();
    Assert.Null(replacement.NativePeer);
    host.MountHeadless(original);
    Assert.False(ReferenceEquals(originalElement, GetElement(child)));
    Assert.Equal("Detached", GetElement(child).TextContent);
    host.Dispose();
    Assert.Null(child.NativePeer);
    Assert.Null(detachedChild.NativePeer);
    AssertDisposed(() => host.MountHeadless(replacement));
}

static void TestHostOwnership()
{
    using Text text = new() { Value = "Owned" };
    using HtmlDomRoot owner = new();
    using HtmlDomRoot other = new();
    owner.MountHeadless(text);
    HtmlDomElement element = GetElement(text);
    bool rejected = false;
    try
    {
        other.MountHeadless(text);
    }
    catch (InvalidOperationException)
    {
        rejected = true;
    }

    Assert.True(rejected);
    Assert.Same(element, GetElement(text));
    other.Dispose();
    text.Value = "Still owned";
    Assert.Equal("Still owned", element.TextContent);
}

static void TestControlDisposal()
{
    View root = new();
    Text child = new() { Value = "Child" };
    root.Children.Add(child);
    using HtmlDomRoot host = new();
    host.MountHeadless(root);
    HtmlDomElement rootElement = GetElement(root);
    HtmlDomElement childElement = GetElement(child);

    root.Dispose();

    Assert.Null(root.NativePeer);
    Assert.Null(child.NativePeer);
    AssertDisposed(() => rootElement.SetTextContent("Invalid"));
    AssertDisposed(() => childElement.SetTextContent("Invalid"));

    host.Unmount();
    using Text remounted = new() { Value = "Remounted" };
    host.MountHeadless(remounted);
    Assert.Equal("Remounted", GetElement(remounted).TextContent);
}

static void TestPointerEventRouting()
{
    using View root = new();
    Text child = new() { Value = "Child" };
    root.Children.Add(child);
    using HtmlDomRoot host = new();
    host.MountHeadless(root);
    int childNode = ((HtmlDomElement)child.NativePeer!).NodeId;

    List<string> order = [];
    PointerEventArgs? pressed = null;
    object? pressedSender = null;
    root.PointerEntered += (_, _) => order.Add("enter root");
    child.PointerEntered += (_, _) => order.Add("enter child");
    child.PointerExited += (_, _) => order.Add("exit child");
    root.PointerExited += (_, _) => order.Add("exit root");
    child.PointerMoved += (_, _) => order.Add("move child");
    root.PointerMoved += (_, _) => order.Add("move root");
    child.PointerReleased += (_, args) =>
    {
        order.Add("up child");
        args.Handled = true;
    };
    root.PointerReleased += (_, _) => order.Add("up root");
    root.PointerPressed += (sender, args) =>
    {
        order.Add("down root");
        pressedSender = sender;
        pressed = args;
    };

    Point position = new(5.5, 6);
    host.DispatchPointerEvent(new PointerEventData { Kind = PointerEventKind.Move, NodeId = childNode, PointerId = 7, Position = position });
    Assert.Equal("enter root,enter child,move child,move root", string.Join(',', order));
    Assert.Equal(ElementStates.Hover, root.States);
    Assert.Equal(ElementStates.Hover, child.States);

    order.Clear();
    host.DispatchPointerEvent(new PointerEventData
    {
        Kind = PointerEventKind.Press,
        NodeId = childNode,
        PointerId = 7,
        Position = position,
        Buttons = PointerButtons.Primary,
        Modifiers = PointerModifiers.Shift | PointerModifiers.Meta,
        ClickCount = 2
    });
    host.DispatchPointerEvent(new PointerEventData
    {
        Kind = PointerEventKind.Release,
        NodeId = childNode,
        PointerId = 7,
        Position = position,
        ClickCount = 2
    });
    Assert.Equal("down root,up child", string.Join(',', order));
    Assert.Same(root, pressedSender!);
    Assert.Same(child, pressed!.Source!);
    Assert.Equal(7L, pressed.PointerId);
    Assert.Equal(5.5, pressed.Position.X);
    Assert.Equal(PointerButtons.Primary, pressed.Buttons);
    Assert.Equal(PointerModifiers.Shift | PointerModifiers.Meta, pressed.Modifiers);
    Assert.Equal(2, pressed.ClickCount);

    order.Clear();
    host.DispatchPointerEvent(new PointerEventData { Kind = PointerEventKind.Leave, PointerId = 7, Position = new Point(-1, -1) });
    Assert.Equal("exit child,exit root", string.Join(',', order));
    Assert.Equal(ElementStates.None, root.States);
}

static void TestPointerCleanup()
{
    using View root = new();
    Text child = new() { Value = "Child" };
    root.Children.Add(child);
    using HtmlDomRoot host = new();
    host.MountHeadless(root);
    int childNode = ((HtmlDomElement)child.NativePeer!).NodeId;
    host.DispatchPointerEvent(new PointerEventData { Kind = PointerEventKind.Move, NodeId = childNode, PointerId = 1 });
    Assert.NotNull(HtmlDomElement.FindByNodeId(childNode));

    host.Unmount();

    Assert.Null(HtmlDomElement.FindByNodeId(childNode));
    Assert.Equal(ElementStates.None, child.States);

    int moves = 0;
    child.PointerMoved += (_, _) => moves++;
    host.DispatchPointerEvent(new PointerEventData { Kind = PointerEventKind.Move, NodeId = childNode, PointerId = 1 });
    Assert.Equal(0, moves);
}

static void TestStyleSheetCss()
{
    StyleSheet styleSheet = new()
    {
        Rules =
        {
            StyleRule.Selector(s => s.Type<Text>().Class("title"))
                .Style(new Style { FontSize = 26, Padding = new Thickness(12, 8), Opacity = 0.5 }),
            StyleRule.Selector(s => s.Type<Text>().Class("title").Hover())
                .Style(new Style { Color = Colors.Blue }),
            StyleRule.Selector(s => s.Class("card").Hover().Descendant(d => d.Type<Text>()))
                .Style(new Style { FontFamily = "Arial" }),
            StyleRule.Selector(s => s.Type<View>())
                .Style(new Style { Width = 100 }),
            StyleRule.Selector(s => s.Type<UIElement>())
                .Style(new Style { Height = 10 }),
            StyleRule.Selector(s => s)
                .Style(new Style { BorderRadius = 2 }),
            StyleRule.Selector(s => s.Class("a:b").Class("1x"))
                .Style(new Style { Margin = new Thickness(1) }),
            StyleRule.Selector(s => s.Class("empty"))
                .Style(new Style())
        }
    };

    string[] lines = StyleSheetCss.Write(styleSheet, 7).TrimEnd().Split('\n');
    const string Scope = ":where(native-ui[data-nui-scope=\"7\"])";

    Assert.Equal($"{Scope} span.title {{ font-size: 26px; padding: 12px 8px 12px 8px; opacity: 0.5; }}", lines[0]);
    Assert.Equal($"{Scope} span.title:hover {{ color: {Colors.Blue.ToCssString()}; }}", lines[1]);
    Assert.Equal($"{Scope} .card:hover span {{ font-family: Arial; }}", lines[2]);
    Assert.Equal($"{Scope} div:where([data-nui-type~=\"t0\"]) {{ width: 100px; }}", lines[3]);
    Assert.Equal($"{Scope} :is(span, div):where([data-nui-type~=\"t1\"]) {{ height: 10px; }}", lines[4]);
    Assert.Equal($"{Scope} * {{ border-radius: 2px; }}", lines[5]);
    Assert.Equal($"{Scope} .a\\3a b.\\31 x {{ margin: 1px 1px 1px 1px; }}", lines[6]);
    Assert.Equal($"{Scope} .empty {{ }}", lines[7]);
}

static void TestStyleSheetTypeMapping()
{
    Assert.Equal("span", HtmlTagMapping.GetTagSelector(typeof(Text)));
    Assert.Equal("span", HtmlTagMapping.GetTagSelector(typeof(CustomText)));
    Assert.Equal("div", HtmlTagMapping.GetTagSelector(typeof(View)));
    Assert.Equal("div", HtmlTagMapping.GetTagSelector(typeof(CustomElement)));
    Assert.Equal(":is(span, div)", HtmlTagMapping.GetTagSelector(typeof(UIElement)));
    Assert.Equal(":is(span, div)", HtmlTagMapping.GetTagSelector(typeof(IUIElement)));

    using CustomElement customElement = new();
    Assert.Equal("div", HtmlTagMapping.GetTag(customElement));

    Assert.Contains("span.title:hover", WriteRule(new CompoundSelector("text", ["title"], ElementStates.Hover)));
    Assert.Contains(" div {", WriteRule(new CompoundSelector("view", [])));
    Assert.Contains(" button {", WriteRule(new CompoundSelector("button", [])));
    AssertNotSupported(new CompoundSelector("slider", []));
    AssertNotSupported(new CompoundSelector("text", [], clrType: typeof(Text)));

    static string WriteRule(CompoundSelector part)
    {
        StyleSheet styleSheet = new()
        {
            Rules = { new StyleRule(new StyleSelector(part, []), new Style()) }
        };
        return StyleSheetCss.Write(styleSheet, 1);
    }

    static void AssertNotSupported(CompoundSelector part)
    {
        bool rejected = false;
        try
        {
            WriteRule(part);
        }
        catch (NotSupportedException)
        {
            rejected = true;
        }

        Assert.True(rejected, "Expected the selector part to be rejected.");
    }
}

static void TestStyleSheetTypeTokens()
{
    StyleSheet styleSheet = new()
    {
        Rules =
        {
            StyleRule.Selector(s => s.Type<Text>()).Style(new Style()),
            StyleRule.Selector(s => s.Type<View>().Descendant(d => d.Type<CustomText>())).Style(new Style()),
            StyleRule.Selector(s => s.Type<View>()).Style(new Style())
        }
    };

    using View view = new();
    using Text text = new();
    using CustomText custom = new();

    Assert.Equal("t0", StyleSheetCss.GetTypeTokens(styleSheet, view));
    Assert.Null(StyleSheetCss.GetTypeTokens(styleSheet, text));
    Assert.Equal("t1", StyleSheetCss.GetTypeTokens(styleSheet, custom));
}

static void TestViewScopedStyleSheetCss()
{
    StyleSheet styleSheet = new()
    {
        Rules =
        {
            StyleRule.Selector(s => s.Type<Text>().Class("title"))
                .Style(new Style { FontSize = 26 }),
            StyleRule.Selector("text.title:hover")
                .Style(new Style { Color = Colors.Blue }),
            StyleRule.Selector(s => s.Class("card").Hover().Descendant(d => d.Type<Text>()))
                .Style(new Style { FontFamily = "Arial" }),
            StyleRule.Selector(s => s)
                .Style(new Style { BorderRadius = 4 }),
            StyleRule.Selector(".outer .inner .leaf")
                .Style(new Style { BorderRadius = 2 })
        }
    };

    string[] lines = StyleSheetCss.WriteScoped(styleSheet, "v1").TrimEnd().Split('\n');
    Assert.Equal(":where([data-nui-view-scope=\"v1\"]) span.title, span.title:where([data-nui-view-scope=\"v1\"]) { font-size: 26px; }", lines[0]);
    Assert.Equal($":where([data-nui-view-scope=\"v1\"]) span.title:hover, span.title:hover:where([data-nui-view-scope=\"v1\"]) {{ color: {Colors.Blue.ToCssString()}; }}", lines[1]);
    Assert.Equal(":where([data-nui-view-scope=\"v1\"]) .card:hover span, .card:hover:where([data-nui-view-scope=\"v1\"]) span { font-family: Arial; }", lines[2]);
    Assert.Equal(":where([data-nui-view-scope=\"v1\"]) *, *:where([data-nui-view-scope=\"v1\"]) { border-radius: 4px; }", lines[3]);
    Assert.Equal(":where([data-nui-view-scope=\"v1\"]) .outer .inner .leaf, .outer:where([data-nui-view-scope=\"v1\"]) .inner .leaf { border-radius: 2px; }", lines[4]);
}

static void TestViewScopedStyleSheetLifecycle()
{
    StyleSheet styleSheet = new()
    {
        Rules =
        {
            StyleRule.Selector(".title")
                .Style(new Style { FontSize = 20 })
        }
    };

    using View view = new()
    {
        StyleSheet = styleSheet
    };
    Text child = new() { Class = "title" };
    view.Children.Add(child);

    using HtmlDomRoot domRoot = new();
    domRoot.MountHeadless(view);

    HtmlDomView viewProxy = (HtmlDomView)GetElement(view);
    HtmlDomElement childElement = GetElement(child);

    string? scopeId = viewProxy.ViewScopeId;
    Assert.NotNull(scopeId);
    Assert.True(scopeId!.StartsWith("v", StringComparison.Ordinal));
    Assert.False(childElement is HtmlDomView { ViewScopeId: not null });

    view.StyleSheet = null;
    Assert.Null(viewProxy.ViewScopeId);

    view.StyleSheet = styleSheet;
    string? newScopeId = viewProxy.ViewScopeId;
    Assert.NotNull(newScopeId);
}

static void TestBaseStyleSheetCss()
{
    string[] lines = StyleSheetCss.BaseStyleSheet.RawCss.TrimEnd().Split('\n');

    Assert.Equal(4, lines.Length);
    Assert.Equal(":where(native-ui, native-ui span, native-ui button) { display: block; }", lines[0]);
    Assert.Equal(":where(native-ui button) { margin: 0; padding: 0; border: none; background: none; color: inherit; font: inherit; user-select: none; }", lines[1]);
    Assert.Equal(":where(native-ui[data-theme=\"light\"]) { color-scheme: light; }", lines[2]);
    Assert.Equal(":where(native-ui[data-theme=\"dark\"]) { color-scheme: dark; }", lines[3]);
}

static void TestViewStyleSheetTransitions()
{
    StyleSheet scopedSheet = new()
    {
        Rules =
        {
            StyleRule.Selector(".title")
                .Style(new Style { FontSize = 20 })
        }
    };
    CssStyleSheet compiledSheet = new("Compiled", ".title { font-size: 20px; }", []);

    using View view = new() { StyleSheet = compiledSheet };
    using HtmlDomRoot domRoot = new();
    domRoot.MountHeadless(view);
    HtmlDomView viewProxy = (HtmlDomView)GetElement(view);
    Assert.Null(viewProxy.ViewScopeId);

    view.StyleSheet = scopedSheet;
    string? firstScopeId = viewProxy.ViewScopeId;
    Assert.NotNull(firstScopeId);

    view.StyleSheet = compiledSheet;
    Assert.Null(viewProxy.ViewScopeId);

    view.StyleSheet = scopedSheet;
    Assert.NotNull(viewProxy.ViewScopeId);
    Assert.False(viewProxy.ViewScopeId == firstScopeId, "A removed scope must not be reused.");

    view.StyleSheet = new StyleSheet();
    Assert.Null(viewProxy.ViewScopeId);
}

static void TestButtonDomProxy()
{
    Button button = new()
    {
        Text = "Submit",
        Class = ["btn", "btn-primary"]
    };

    using HtmlDomRoot domRoot = new();
    domRoot.MountHeadless(button);

    HtmlDomElement element = GetElement(button);
    Assert.Equal("button", element.TagName);
    Assert.Equal("btn btn-primary", element.ClassName);
    Assert.Equal("Submit", element.TextContent);

    // Verify text update
    button.Text = "Save Changes";
    Assert.Equal("Save Changes", element.TextContent);
}

static HtmlDomElement GetElement(UIElement element)
{
    return element.NativePeer as HtmlDomElement
        ?? throw new InvalidOperationException("Expected a mounted HtmlDomElement.");
}

static void AssertDisposed(Action action)
{
    bool rejected = false;
    try
    {
        action();
    }
    catch (ObjectDisposedException)
    {
        rejected = true;
    }

    Assert.True(rejected, "Expected disposed objects to reject updates.");
}

file sealed class CustomText : Text;

file sealed class CustomElement : UIElement
{
    public override string TagName => "custom";
}

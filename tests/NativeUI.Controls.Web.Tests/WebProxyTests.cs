using System.Collections.ObjectModel;
using NativeUI.Controls;
using NativeUI.Controls.Web;
using NativeUI.Core;
using NativeUI.Css;
using NativeUI.Graphics;
using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Sample;
using NativeUI.Styling;
using NativeUI.Tests;

// Verifies proxy state and ownership on the .NET host without creating browser DOM elements.
if (args.Contains("--tabs"))
{
    return TestRunner.RunSuite("Tabs Web", [("Tab proxies retain pages, update shared selection, and detach cleanly", TestTabs)]);
}

if (args.Contains("--radio"))
{
    return TestRunner.RunSuite("RadioButton Web", [("Radio proxies preserve shared selection, labels, and disposal", TestRadios)]);
}

int exitCode = TestRunner.RunSuite("NativeUI.Controls.Web.Tests",
[
    ("Radio proxies preserve shared selection, labels, and disposal", TestRadios),
    ("SVG updates emit only changed attributes", NativeUI.Controls.Web.Tests.SvgDomBuilderTests.AttributeChangesAreTargeted),
    ("Immutable SVG edits update only affected attributes", NativeUI.Controls.Web.Tests.SvgDomBuilderTests.ImmutableDefinitionsProduceLocalChanges),
    ("SVG attributes are applied when added and removed when unset", NativeUI.Controls.Web.Tests.SvgDomBuilderTests.AddedAndRemovedAttributesAreApplied),
    ("SVG aspect ratios and definition references use explicit browser syntax", NativeUI.Controls.Web.Tests.SvgDomBuilderTests.AspectAndReferencesAreScoped),
    ("Browser image formats use validated signatures instead of a JPEG fallback", TestImageContentTypes),
    ("SVG snapshots reflect live geometry and retain unchanged geometry definitions", TestSvgSnapshots),
    ("Natural-size vector icons retain their geometry scale", TestNaturalSizeIcon),
    ("Input sample mounts its proxy hierarchy and performs targeted incremental updates", TestIncrementalUpdates),
    ("DOM proxies synchronize text and keep their identity across property changes", TestPropertyChanges),
    ("View proxies reconcile children without recreating unchanged proxies", TestChildCollections),
    ("WebHost unmounts subscriptions and proxies before remounting", TestMountLifetime),
    ("Web hosts attach existing trees and detach them on unmount", TestVisualTreeAttachment),
    ("Web focus reports share the keyboard device and respect host teardown", TestKeyboardFocusLifetime),
    ("Native form tags and element-owned actions use shared MVVM form logic", TestForms),
    ("Sibling forms isolate actions and retain independent lifetimes", TestFormOwnership),
    ("Element callbacks preserve control ownership and stop after disposal", TestElementEventOwnership),
    ("Context menu peers retain identity when targets change", TestContextMenuTarget),
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
    ("Button mounts as native button element and synchronizes disabled and text attributes", TestButtonDomProxy),
    ("CanvasView mounts as HTML canvas element, synchronizes dimensions, and records 2D drawing", TestCanvasDomProxy),
    ("Select mounts as nui-select custom element and manages selection attributes", TestSelectDomProxy),
    ("Image uses native roots and Portal uses a custom root while Dialog retains native semantics", TestCustomElementRoots),
    ("UIElement SizeChanged event tracks rendered dimensions and notifies peer", TestSizeChangedSubscription)
]);

return exitCode;

static void TestImageContentTypes()
{
    Assert.Equal("image/png", ImageResourceResolver.GetContentType([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]));
    Assert.Equal("image/jpeg", ImageResourceResolver.GetContentType([0xFF, 0xD8, 0xFF, 0xE0]));
    Assert.Equal("image/svg+xml", ImageResourceResolver.GetContentType(System.Text.Encoding.UTF8.GetBytes("<svg/>")));
    bool rejected = false;
    try
    {
        ImageResourceResolver.GetContentType([0x89, 0x00]);
    }
    catch (InvalidDataException)
    {
        rejected = true;
    }

    Assert.True(rejected, "An incomplete PNG signature must not be treated as PNG or JPEG.");
}

static void TestSvgSnapshots()
{
    using ImageSampleView page = new();
    CompiledSvgSource compiled = page.GoogleLogo.Source as CompiledSvgSource
        ?? throw new InvalidOperationException("Google SVG must be compiled.");
    using Svg logo = compiled.Factory();
    SvgNode snapshot = SvgDomBuilder.Definition(logo);
    Assert.Equal(new SolidColorBrush(Color.FromRgbBytes(66, 133, 244)), snapshot.Children[0].Fill);
    Assert.True(ReferenceEquals(snapshot, SvgDomBuilder.Definition(logo)));

    PathGeometry first = Geometry.Parse("M0 0 L24 0 L12 24 Z");
    using NativeUI.Controls.Shapes.Path shape = new() { Data = first, Fill = Brushes.Blue };
    SvgNode before = SvgDomBuilder.CreateShapeDefinition(shape, new Size(24, 24));
    Assert.True(ReferenceEquals(first, before.Children[0].Geometry));
    PathGeometry replacement = Geometry.Parse("M0 0 H24 V24 H0 Z");
    shape.Data = replacement;
    SvgNode after = SvgDomBuilder.CreateShapeDefinition(shape, new Size(24, 24));
    Assert.True(ReferenceEquals(replacement, after.Children[0].Geometry));
    Assert.True(ReferenceEquals(first, before.Children[0].Geometry));
    shape.Fill = new LinearGradientBrush
    {
        Start = new Point(0, 0),
        End = new Point(24, 24),
        Stops = [new GradientStop(0, Colors.Blue), new GradientStop(1, Colors.Green)]
    };
    SvgNode gradient = SvgDomBuilder.CreateShapeDefinition(shape, new Size(24, 24));
    Assert.Equal(SvgNodeKind.Definitions, gradient.Children[0].Kind);
    Assert.Equal(SvgNodeKind.LinearGradient, gradient.Children[0].Children[0].Kind);
    Assert.True(ReferenceEquals(replacement, gradient.Children[1].Geometry));
}

static void TestNaturalSizeIcon()
{
    NativeUI.Controls.Shapes.Path icon = new()
    {
        Data = Icons.ChevronRight,
        Stretch = Stretch.None,
        Width = 16,
        Height = 16
    };
    SvgNode definition = SvgDomBuilder.CreateShapeDefinition(icon, new Size(16, 16));
    Assert.Equal(new Rect(-0.125, 0, 16, 16), definition.ViewBox ?? Rect.Empty);
    Assert.Same(Icons.ChevronRight, definition.Children[0].Geometry ?? throw new InvalidOperationException("Missing geometry."));
    SvgNode largerViewport = SvgDomBuilder.CreateShapeDefinition(icon, new Size(32, 32));
    Assert.Equal(32.0, largerViewport.ViewBox?.Width ?? 0);
    Assert.Equal(32.0, largerViewport.ViewBox?.Height ?? 0);
    icon.Dispose();
}

static void TestTabs()
{
    using TabControl tabs = new();
    TabItem first = new() { Header = "One", Content = new Input { Value = "First" } };
    TabItem second = new() { Header = "Two", Content = new Input { Value = "Second" } };
    tabs.Items.Add(first);
    tabs.Items.Add(second);
    using HtmlDomRoot host = new();
    host.MountHeadless(tabs);
    Assert.True(GetElement(tabs) is HtmlDomTabControl);
    Assert.True(GetElement(first) is HtmlDomTabItem);
    Assert.Equal("nui-tabcontrol", GetElement(tabs).TagName);
    Assert.Equal("nui-tabitem", GetElement(first).TagName);
    Assert.Equal("nui-tabitem", HtmlTagMapping.GetTagSelector(typeof(TabItem)));
    HtmlDomElement retained = GetElement(first.Page.Children[0]);
    Assert.True(KeyboardNavigation.GetIsTabStop(first));
    tabs.SelectedIndex = 1;
    Assert.False(first.Page.IsVisible);
    Assert.True(second.Page.IsVisible);
    Assert.False(KeyboardNavigation.GetIsTabStop(first));
    Assert.True(KeyboardNavigation.GetIsTabStop(second));
    tabs.SelectedIndex = 0;
    Assert.Same(retained, GetElement(first.Page.Children[0]));
    tabs.Items.Remove(second);
    Assert.Null(second.NativeHandle);
    Assert.Null(second.Page.NativeHandle);
    tabs.TabStripPlacement = Dock.Left;
    host.Unmount();
    Assert.Null(first.NativeHandle);
    Assert.Null(first.Page.NativeHandle);
    Assert.Null(first.Page.Children[0].NativeHandle);
    second.Dispose();
}

static void TestRadios()
{
    using RadioGroup root = new();
    root.SelectedValue = "first";
    RadioGroupItem first = new() { Value = "first" };
    RadioGroupItem second = new() { Value = "second" };
    Label label = new() { Target = second, Content = "Second" };
    root.Children.Add(first);
    root.Children.Add(second);
    root.Children.Add(label);
    using HtmlDomRoot host = new();
    host.MountHeadless(root);
    Assert.True(GetElement(root) is HtmlDomStackPanel);
    Assert.Equal("div", GetElement(root).TagName);
    root.Orientation = Orientation.Horizontal;
    HtmlDomElement proxy = GetElement(first);
    Assert.True(proxy is HtmlDomRadioButton);
    Assert.Equal("nui-radiogroupitem", proxy.TagName);
    Assert.True(first.IsTabStop);
    Assert.False(second.IsTabStop);
    ((HtmlDomLabel)GetElement(label)).OnBrowserActivated();
    Assert.Equal(true, second.IsChecked);
    Assert.Equal(false, first.IsChecked);
    Assert.Equal("second", root.SelectedValue);
    Assert.Same(proxy, GetElement(first));
    root.Children.Remove(second);
    second.Dispose();
    root.IsEnabled = false;
    root.IsEnabled = true;
    Assert.True(first.IsTabStop);
    host.Unmount();
    Assert.Null(first.NativeHandle);
    first.IsChecked = true;
}

static void TestForms()
{
    using Form form = new();
    Fieldset group = new() { Name = "personal" };
    Legend legend = new() { Content = "Personal information" };
    Input input = new() { Required = true, Value = "Initial" };
    Label label = new() { Target = input, Content = "Full name" };
    Button save = new() { Type = ButtonType.Submit };
    Button reset = new() { Type = ButtonType.Reset };
    group.Children.Add(legend);
    group.Children.Add(label);
    group.Children.Add(input);
    form.Children.Add(group);
    form.Children.Add(save);
    form.Children.Add(reset);
    using HtmlDomRoot host = new();
    host.MountHeadless(form);
    Assert.Equal("form", GetElement(form).TagName);
    Assert.Equal("fieldset", GetElement(group).TagName);
    Assert.Equal("legend", GetElement(legend).TagName);
    Assert.Equal("input", GetElement(input).TagName);
    Assert.Equal("button", GetElement(save).TagName);
    int submits = 0;
    form.Submit += (_, args) => { submits++; Assert.Same(save, args.Submitter ?? throw new InvalidOperationException("Missing submitter.")); };
    HtmlDomForm formElement = (HtmlDomForm)GetElement(form);
    formElement.OnBrowserAction(0, save);
    Assert.Equal(1, submits);
    input.Value = string.Empty;
    formElement.OnBrowserAction(0, save);
    Assert.Equal(1, submits);
    Assert.False(Validation.GetHasError(input));
    Assert.True(input.Validity.ValueMissing);
    Assert.True(input.States.HasFlag(ElementStates.Invalid));
    Assert.Equal("label", GetElement(label).TagName);
    formElement.OnBrowserAction(1, reset);
    Assert.Equal("Initial", input.Value);
    Assert.False(Validation.GetHasError(input));
    formElement.OnBrowserAction(2, save);
    Assert.Equal(2, submits);
    group.IsEnabled = false;
    Assert.False(input.IsEffectivelyEnabled);
    host.Unmount();
    formElement.OnBrowserAction(0, save);
    Assert.Equal(2, submits);
}

static void TestFormOwnership()
{
    using View root = new();
    Form first = new();
    Form second = new();
    Button firstButton = new() { Type = ButtonType.Submit };
    Button secondButton = new() { Type = ButtonType.Submit };
    first.Children.Add(firstButton);
    second.Children.Add(secondButton);
    root.Children.Add(first);
    root.Children.Add(second);
    using HtmlDomRoot host = new();
    host.MountHeadless(root);
    HtmlDomForm firstElement = (HtmlDomForm)GetElement(first);
    HtmlDomForm secondElement = (HtmlDomForm)GetElement(second);
    int firstSubmits = 0;
    int secondSubmits = 0;
    first.Submit += (_, _) => firstSubmits++;
    second.Submit += (_, _) => secondSubmits++;

    firstElement.OnBrowserAction(0, firstButton);
    secondElement.OnBrowserAction(0, secondButton);
    firstElement.OnBrowserAction(0, secondButton);
    secondElement.OnBrowserAction(2, firstButton);
    Assert.Equal(1, firstSubmits);
    Assert.Equal(1, secondSubmits);

    root.Children.Remove(first);
    firstElement.OnBrowserAction(0, firstButton);
    secondElement.OnBrowserAction(0, secondButton);
    Assert.Equal(1, firstSubmits);
    Assert.Equal(2, secondSubmits);
    Assert.Same(secondElement, GetElement(second));
    first.Dispose();
}

static void TestElementEventOwnership()
{
    using View root = new();
    Input first = new() { Value = "first" };
    Input second = new() { Value = "second" };
    View scroll = new();
    Dialog dialog = new();
    Dialog otherDialog = new();
    root.Children.Add(first);
    root.Children.Add(second);
    root.Children.Add(scroll);
    root.Children.Add(dialog);
    root.Children.Add(otherDialog);
    using HtmlDomRoot host = new();
    host.MountHeadless(root);
    HtmlDomInput inputElement = (HtmlDomInput)GetElement(first);
    HtmlDomInput secondElement = (HtmlDomInput)GetElement(second);
    HtmlDomView scrollElement = (HtmlDomView)GetElement(scroll);
    HtmlDomDialog dialogElement = (HtmlDomDialog)GetElement(dialog);
    int scrolls = 0;
    scroll.ScrollChanged += (_, _) => scrolls++;

    inputElement.OnBrowserInput("updated");
    Assert.Equal("updated", first.Value);
    Assert.Equal("second", second.Value);
    first.IsReadOnly = true;
    inputElement.OnBrowserInput("ignored");
    Assert.Equal("updated", first.Value);
    first.IsReadOnly = false;
    first.IsEnabled = false;
    inputElement.OnBrowserInput("disabled");
    Assert.Equal("updated", first.Value);
    first.IsEnabled = true;
    scrollElement.OnBrowserScrolled();
    Assert.Equal(1, scrolls);
    dialog.Show();
    otherDialog.Show();
    dialogElement.OnBrowserClosed();
    Assert.False(dialog.IsOpen);
    Assert.True(otherDialog.IsOpen);

    root.Children.Remove(first);
    inputElement.OnBrowserInput("detached");
    Assert.Equal("updated", first.Value);
    secondElement.OnBrowserInput("retained");
    Assert.Equal("retained", second.Value);
    Assert.Same(secondElement, GetElement(second));
    first.Dispose();
    host.Unmount();
    secondElement.OnBrowserInput("unmounted");
    scrollElement.OnBrowserScrolled();
    Assert.Equal("retained", second.Value);
    Assert.Equal(1, scrolls);
    dialog.Show();
    dialogElement.OnBrowserClosed();
    Assert.True(dialog.IsOpen);
    dialog.Close();
    otherDialog.Close();
}

static void TestContextMenuTarget()
{
    using View root = new();
    Button first = new();
    Button second = new();
    ContextMenu menu = new() { Target = first };
    Popover popover = new();
    root.Children.Add(menu);
    root.Children.Add(second);
    root.Children.Add(popover);
    using HtmlDomRoot host = new();
    host.MountHeadless(root);
    HtmlDomElement peer = GetElement(menu);
    Assert.True(peer is HtmlDomContextMenu);
    Assert.True(GetElement(popover) is HtmlDomPopover);
    HtmlDomElement firstElement = GetElement(first);
    HtmlDomElement secondElement = GetElement(second);
    host.DispatchPointerEvent(new PointerEventData
    {
        Kind = PointerEventKind.Press,
        NodeId = firstElement.NodeId,
        PointerId = 1,
        Buttons = PointerButtons.Secondary
    });
    Assert.True(menu.IsOpen);
    menu.IsOpen = false;
    menu.Target = second;
    Assert.Same(peer, GetElement(menu));
    Assert.Same(secondElement, GetElement(second));
    Assert.Null(first.NativeHandle);
    host.DispatchPointerEvent(new PointerEventData
    {
        Kind = PointerEventKind.Press,
        NodeId = secondElement.NodeId,
        PointerId = 2,
        Buttons = PointerButtons.Secondary
    });
    Assert.True(menu.IsOpen);
    menu.IsOpen = false;
    menu.Target = null;
    host.DispatchPointerEvent(new PointerEventData
    {
        Kind = PointerEventKind.Press,
        NodeId = secondElement.NodeId,
        PointerId = 3,
        Buttons = PointerButtons.Secondary
    });
    Assert.False(menu.IsOpen);
    host.Unmount();
    Assert.Null(menu.NativeHandle);
    first.Dispose();
}

static void TestKeyboardFocusLifetime()
{
    using View root = new();
    Button first = new();
    root.Children.Add(first);
    using HtmlDomRoot host = new();
    host.MountHeadless(root);
    KeyboardDevice keyboard = InputManager.Current.PrimaryKeyboardDevice;
    Assert.False(first.Focus(FocusState.Keyboard));
    Assert.False(first.IsFocused);
    keyboard.HandleGotFocus(first, FocusState.Keyboard);
    Assert.True(first.IsFocused);
    Assert.True(first.States.HasFlag(ElementStates.FocusVisible));
    Assert.Same(first, FocusManager.GetFocusedElement(root) ?? throw new InvalidOperationException("Missing logical focus."));

    using View otherRoot = new();
    Button second = new();
    otherRoot.Children.Add(second);
    using HtmlDomRoot otherHost = new();
    otherHost.MountHeadless(otherRoot);
    keyboard.HandleGotFocus(second, FocusState.Pointer);
    Assert.False(first.IsFocused);
    Assert.True(second.IsFocused);
    Assert.False(second.States.HasFlag(ElementStates.FocusVisible));
    host.Unmount();
    Assert.True(second.IsFocused);
    keyboard.HandleGotFocus(first, FocusState.Keyboard);
    Assert.True(second.IsFocused);
    otherHost.Unmount();
    Assert.Null(keyboard.FocusedElement);
    Assert.False(second.IsFocused);
    Assert.Null(FocusManager.GetFocusedElement(otherRoot));
}

static void TestVisualTreeAttachment()
{
    using AttachmentView root = new();
    AttachmentView child = new();
    Portal popup = new();
    root.Children.Add(child);
    root.Children.Add(popup);
    popup.Show();
    using HtmlDomRoot host = new();
    host.MountHeadless(root);
    Assert.Equal(1, root.Attachments);
    Assert.Equal(1, child.Attachments);

    AttachmentView added = new();
    root.Children.Add(added);
    Assert.Equal(1, added.Attachments);
    root.Children.Remove(added);
    Assert.Equal(1, added.Detachments);

    host.Unmount();
    Assert.Equal(1, root.Detachments);
    Assert.Equal(1, child.Detachments);
    Assert.False(popup.IsOpen);
    host.MountHeadless(root);
    Assert.Equal(2, root.Attachments);
    Assert.Equal(2, child.Attachments);
    host.Unmount();
    Assert.Equal(2, child.Detachments);
}

static void TestIncrementalUpdates()
{
    HelloViewModel viewModel = new();
    InputSampleView view = new()
    {
        DataContext = viewModel
    };

    using HtmlDomRoot domRoot = new();
    domRoot.MountHeadless(view);

    HtmlDomElement root = GetElement(view);
    HtmlDomElement title = GetElement(view.GreetingText);
    Assert.Equal("div", root.TagName);
    Assert.Equal("span", title.TagName);
    Assert.Equal("scroll-status", title.ClassName);

    viewModel.UserName = "NativeUI";
    Assert.Equal("Hello, NativeUI", title.TextContent);
    Assert.Same(title, GetElement(view.GreetingText));
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
    Assert.True(root.NativeHandle is HtmlDomView);
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
    Assert.Null(second.NativeHandle);
    Assert.Null(second.Parent);
    AssertDisposed(() => secondElement.SetTextContent("Invalid"));

    children.Clear();
    Assert.Null(first.NativeHandle);
    Assert.Null(inserted.NativeHandle);
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
    Assert.Null(original.NativeHandle);
    Assert.Null(child.NativeHandle);
    AssertDisposed(() => originalElement.SetTextContent("Invalid"));
    child.Value = "Detached";
    style.FontSize = 20;
    Text detachedChild = new();
    original.Children.Add(detachedChild);
    Assert.Null(detachedChild.NativeHandle);
    Assert.Equal("Replacement", GetElement(replacement).TextContent);

    host.Unmount();
    Assert.Null(replacement.NativeHandle);
    host.MountHeadless(original);
    Assert.False(ReferenceEquals(originalElement, GetElement(child)));
    Assert.Equal("Detached", GetElement(child).TextContent);
    host.Dispose();
    Assert.Null(child.NativeHandle);
    Assert.Null(detachedChild.NativeHandle);
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

    Assert.Null(root.NativeHandle);
    Assert.Null(child.NativeHandle);
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
    int childNode = ((HtmlDomElement)child.NativeHandle!).NodeId;

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
        Modifiers = InputModifiers.Shift | InputModifiers.Meta,
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
    Assert.Equal(InputModifiers.Shift | InputModifiers.Meta, pressed.Modifiers);
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
    int childNode = ((HtmlDomElement)child.NativeHandle!).NodeId;
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

    Assert.Equal(35, lines.Length);
    Assert.True(lines[0].StartsWith(":where(native-ui, native-ui *) { "));
    Assert.Contains("box-sizing: border-box;", lines[0]);
    Assert.Contains("border: 0 solid;", lines[0]);
    Assert.Equal(":where(native-ui) { line-height: 1.25; }", lines[1]);
    Assert.Equal(":where(native-ui, native-ui span, native-ui button, native-ui img, native-ui svg) { display: block; }", lines[2]);
    Assert.True(lines[3].StartsWith(":where(native-ui button) { "));
    Assert.Contains("margin: 0;", lines[3]);
    Assert.Contains("padding: 0;", lines[3]);
    Assert.Contains("background: none;", lines[3]);
    Assert.Contains("color: inherit;", lines[3]);
    Assert.Contains("font: inherit;", lines[3]);
    Assert.Contains("user-select: none;", lines[3]);
    Assert.Contains("width: fit-content;", lines[3]);
    Assert.True(lines[5].StartsWith(":where(native-ui canvas) { "));
    Assert.Contains("display: block;", lines[5]);
    Assert.Contains("width: 100%;", lines[5]);
    Assert.Contains("height: 0;", lines[5]);
    Assert.Equal(":where([data-nui-scroll-view]) { overflow: auto; }", lines[9]);
    Assert.True(lines[10].StartsWith(":where(native-ui, native-ui *)::-webkit-scrollbar { "));
    Assert.Contains("width: 10px;", lines[10]);
    Assert.Contains("height: 10px;", lines[10]);
    Assert.Equal(":where(native-ui[data-theme=\"dark\"], native-ui[data-theme=\"dark\"] *)::-webkit-scrollbar-thumb { background-color: rgb(255 255 255 / 0.35); }", lines[15]);
    Assert.Equal(":where(native-ui[data-theme=\"dark\"]) { color-scheme: dark; }", lines[17]);
    Assert.True(lines[19].StartsWith(":where(native-ui nui-switch, native-ui nui-toggle) { "));
    Assert.Contains("display: block;", lines[19]);
    Assert.True(lines[20].StartsWith(":where(native-ui nui-checkbox) { "));
    Assert.Contains("display: flex;", lines[20]);
    Assert.True(lines[21].StartsWith(":where(native-ui nui-togglegroup) { "));
    Assert.Contains("flex-direction: row;", lines[21]);
    Assert.True(lines[22].StartsWith(":where(native-ui nui-list) { "));
    Assert.Contains("overflow-y: auto;", lines[22]);
    Assert.True(lines[23].StartsWith(":where(native-ui nui-virtualizedlist) { "));
    Assert.Contains("overflow-y: auto;", lines[23]);
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

static void TestCanvasDomProxy()
{
    bool drawn = false;
    CanvasView canvas = new();
    canvas.Draw += (_, _) => drawn = true;

    using HtmlDomRoot domRoot = new();
    domRoot.MountHeadless(canvas);

    Assert.Equal("canvas", GetElement(canvas).TagName);

    // Without a browser element, invalidating cleanly does nothing.
    canvas.InvalidateVisual();
    Assert.False(drawn);
}

static void TestSelectDomProxy()
{
    using Select select = new() { Placeholder = "Choose fruit" };
    select.Items.Add("Apple");
    select.Items.Add("Banana");

    using HtmlDomRoot domRoot = new();
    domRoot.MountHeadless(select);

    HtmlDomElement element = GetElement(select);
    Assert.True(element is HtmlDomSelect);
    Assert.Equal("nui-select", element.TagName);
    Assert.Equal("nui-select", HtmlTagMapping.GetTagSelector(typeof(Select)));
    Assert.Equal("nui-select", HtmlTagMapping.GetTagSelector("select"));
    Assert.Equal("", select.GetAttribute("data-closed"));
    Assert.Equal("", select.GetAttribute("data-placeholder"));

    select.IsDropDownOpen = true;
    Assert.Equal("", select.GetAttribute("data-open"));
    Assert.Null(select.GetAttribute("data-closed"));

    select.SelectedIndex = 1;
    Assert.Null(select.GetAttribute("data-placeholder"));
}

static void TestSizeChangedSubscription()
{
    View view = new();
    using HtmlDomRoot domRoot = new();
    domRoot.MountHeadless(view);

    int sizeChangedCount = 0;
    Size prev = default;
    Size next = default;
    EventHandler<SizeChangedEventArgs> handler = (_, e) =>
    {
        sizeChangedCount++;
        prev = e.PreviousSize;
        next = e.NewSize;
    };

    view.SizeChanged += handler;
    Assert.True(UIElement.BoundsProperty.HasSubscribers(view));
    Assert.True(view.HasHandler(UIElement.SizeChangedEvent));

    view.UpdateRenderedBounds(new Rect(0, 0, 200, 100));
    Assert.Equal(1, sizeChangedCount);
    Assert.Equal(new Size(0, 0), prev);
    Assert.Equal(new Size(200, 100), next);
    Assert.Equal(new Size(200, 100), view.ActualSize);
    Assert.Equal(200, view.ActualWidth);
    Assert.Equal(100, view.ActualHeight);
    Assert.Equal(new Rect(0, 0, 200, 100), view.Bounds);

    int widthChangedCount = 0;
    void OnPropChanged(object? sender, BindablePropertyChangedEventArgs e)
    {
        if (e.Property == UIElement.ActualWidthProperty)
        {
            widthChangedCount++;
        }
    }

    view.PropertyChanged += OnPropChanged;

    view.UpdateRenderedBounds(new Rect(0, 0, 250, 100));
    Assert.Equal(1, widthChangedCount);
    Assert.Equal(250, view.ActualWidth);

    view.PropertyChanged -= OnPropChanged;
    view.SizeChanged -= handler;
    Assert.False(UIElement.BoundsProperty.HasSubscribers(view));
    Assert.False(view.HasHandler(UIElement.SizeChangedEvent));
}

static void TestCustomElementRoots()
{
    using View root = new();
    Image image = new() { AltText = "Picture" };
    Portal portal = new();
    Dialog dialog = new();
    root.Children.Add(image);
    root.Children.Add(portal);
    root.Children.Add(dialog);
    using HtmlDomRoot host = new();
    host.MountHeadless(root);

    HtmlDomElement imageElement = GetElement(image);
    Assert.Equal("img", imageElement.TagName);
    Assert.Equal("nui-portal", GetElement(portal).TagName);
    Assert.Equal("dialog", GetElement(dialog).TagName);
    Assert.Equal(":is(img, svg)", HtmlTagMapping.GetTagSelector(typeof(Image)));
    Assert.Equal(":is(img, svg)", HtmlTagMapping.GetTagSelector("image"));
    Assert.Equal("nui-portal", HtmlTagMapping.GetTagSelector(typeof(Portal)));
    Assert.Equal("nui-portal", HtmlTagMapping.GetTagSelector("portal"));
    Assert.Equal("dialog", HtmlTagMapping.GetTagSelector(typeof(Dialog)));

    image.AltText = "Updated picture";
    image.IsDecorative = true;
    Assert.Same(imageElement, GetElement(image));
}

static HtmlDomElement GetElement(UIElement element)
{
    return element.NativeHandle as HtmlDomElement
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

file sealed class AttachmentView : View
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

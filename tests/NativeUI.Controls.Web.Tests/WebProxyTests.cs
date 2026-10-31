using System.Collections.ObjectModel;
using NativeUI.Controls;
using NativeUI.Controls.Web;
using NativeUI.Media;
using NativeUI.Sample;
using NativeUI.Tests;

// Verifies proxy state and ownership on the .NET host without creating browser DOM elements.
int exitCode = TestRunner.RunSuite("NativeUI.Controls.Web.Tests",
[
    ("HelloView mounts its proxy hierarchy and performs targeted incremental updates", TestIncrementalUpdates),
    ("DOM proxies synchronize text and keep their identity across property changes", TestPropertyChanges),
    ("View proxies reconcile children without recreating unchanged proxies", TestChildCollections),
    ("WebHost unmounts subscriptions and proxies before remounting", TestMountLifetime),
    ("WebHost rejects elements mounted by another host without disposing their proxies", TestHostOwnership),
    ("Disposing a control disposes its proxy and the proxies of its children", TestControlDisposal)
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

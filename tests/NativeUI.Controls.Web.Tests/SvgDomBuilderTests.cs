using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Tests;

namespace NativeUI.Controls.Web.Tests;

internal static class SvgDomBuilderTests
{
    internal static void AttributeChangesAreTargeted()
    {
        PathGeometry geometry = Geometry.Parse("M0 0 L24 0 L12 24 Z");
        SvgNode original = new(SvgNodeKind.Path)
        {
            Geometry = geometry, Fill = Brushes.Blue, Stroke = Brushes.Black,
            StrokeWidth = new SvgLength(2), StrokeDashArray = [2, 4]
        };
        List<(string Name, string? Value)> changes = [];
        SvgDomBuilder.VisitAttributeChanges(null, original, "first_", false, (name, value) => changes.Add((name, value)));
        Assert.Equal(5, changes.Count);
        Assert.True(changes.All(static change => change.Value is not null), "Initial creation must not remove absent attributes.");

        changes.Clear();
        SvgNode recolored = original with { Fill = Brushes.Red };
        SvgDomBuilder.VisitAttributeChanges(original, recolored, "first_", false, (name, value) => changes.Add((name, value)));
        Assert.Equal(1, changes.Count);
        Assert.Equal(("fill", (string?)Colors.Red.ToCssString()), changes[0]);
        Assert.Same(geometry, recolored.Geometry);

        changes.Clear();
        SvgDomBuilder.VisitAttributeChanges(recolored, recolored with { Fill = null }, "first_", false, (name, value) => changes.Add((name, value)));
        Assert.Equal(1, changes.Count);
        Assert.Equal(("fill", (string?)null), changes[0]);

        changes.Clear();
        SvgDomBuilder.VisitAttributeChanges(original, original with { StrokeDashArray = [2, 4] }, "first_", false, (name, value) => changes.Add((name, value)));
        Assert.Equal(0, changes.Count);
        SvgDomBuilder.VisitAttributeChanges(original, original with { Text = "updated" }, "first_", false, (name, value) => changes.Add((name, value)));
        Assert.Equal(0, changes.Count, "Text changes must not rewrite element attributes.");

        SvgDomBuilder.VisitAttributeChanges(original, original with { Geometry = Geometry.Parse("M0 0 H24 V24 Z") }, "first_", false, (name, value) => changes.Add((name, value)));
        Assert.Equal(1, changes.Count);
        Assert.Equal("d", changes[0].Name);
    }

    internal static void ImmutableDefinitionsProduceLocalChanges()
    {
        SvgNode first = new(SvgNodeKind.Path) { Id = "a", Fill = Brushes.Blue };
        SvgNode second = new(SvgNodeKind.Path) { Id = "b", Fill = Brushes.Black };
        SvgNode original = new(SvgNodeKind.Svg) { Children = [first, second] };
        SvgNode updatedChild = first with { Fill = Brushes.Red };
        SvgNode updated = original with { Children = [updatedChild, second] };
        List<(string Name, string? Value)> changes = [];
        SvgDomBuilder.VisitAttributeChanges(original, updated, "instance_", true, (name, value) => changes.Add((name, value)));
        Assert.Equal(0, changes.Count, "Editing a child must not rewrite parent attributes.");
        Assert.Same(second, updated.Children[1]);
        Assert.Equal(Brushes.Blue, original.Children[0].Fill);
        SvgDomBuilder.VisitAttributeChanges(first, updatedChild, "instance_", false, (name, value) => changes.Add((name, value)));
        Assert.Equal(1, changes.Count);
        Assert.Equal("fill", changes[0].Name);

        changes.Clear();
        SvgDomBuilder.VisitAttributeChanges(first, first with { Fill = new SolidColorBrush(Colors.Blue) }, "instance_", false, (name, value) => changes.Add((name, value)));
        Assert.Equal(0, changes.Count, "Equal supported immutable brushes must not produce DOM writes.");
    }

    internal static void AddedAndRemovedAttributesAreApplied()
    {
        SvgNode empty = new(SvgNodeKind.Group);
        SvgNode populated = empty with
        {
            Id = "group", Opacity = .5, Color = Colors.Blue, FontFamily = "sans-serif",
            Transform = System.Numerics.Matrix3x2.Identity, AspectRatio = new SvgAspectRatio(SvgAlignment.XMidYMid),
            StrokeDashArray = [], IsVisible = false, IsAccessibilityHidden = false
        };
        List<(string Name, string? Value)> added = [];
        List<(string Name, string? Value)> removed = [];
        SvgDomBuilder.VisitAttributeChanges(empty, populated, "instance_", false, (name, value) => added.Add((name, value)));
        SvgDomBuilder.VisitAttributeChanges(populated, empty, "instance_", false, (name, value) => removed.Add((name, value)));
        Assert.Equal(9, added.Count);
        Assert.Equal(added.Count, removed.Count);
        Assert.True(added.All(static attribute => attribute.Value is not null));
        Assert.True(removed.All(static attribute => attribute.Value is null));
        Assert.Equal(string.Join(',', added.Select(static attribute => attribute.Name)), string.Join(',', removed.Select(static attribute => attribute.Name)));

        removed.Clear();
        SvgDomBuilder.VisitAttributeChanges(populated, populated with { Id = "different" }, "instance_", true, (name, value) => removed.Add((name, value)));
        Assert.Equal(0, removed.Count, "Initial and subsequent root updates must both preserve the control ID.");
    }

    internal static void AspectAndReferencesAreScoped()
    {
        (SvgAlignment Alignment, string Text)[] alignments =
        [
            (SvgAlignment.XMinYMin, "xMinYMin"), (SvgAlignment.XMidYMin, "xMidYMin"), (SvgAlignment.XMaxYMin, "xMaxYMin"),
            (SvgAlignment.XMinYMid, "xMinYMid"), (SvgAlignment.XMidYMid, "xMidYMid"), (SvgAlignment.XMaxYMid, "xMaxYMid"),
            (SvgAlignment.XMinYMax, "xMinYMax"), (SvgAlignment.XMidYMax, "xMidYMax"), (SvgAlignment.XMaxYMax, "xMaxYMax")
        ];
        foreach ((SvgAlignment alignment, string text) in alignments)
        {
            Assert.Equal(text + " meet", SvgDomBuilder.FormatAspect(new SvgAspectRatio(alignment)));
            Assert.Equal(text + " slice", SvgDomBuilder.FormatAspect(new SvgAspectRatio(alignment, Stretch.UniformToFill)));
        }
        Assert.Equal("none", SvgDomBuilder.FormatAspect(new SvgAspectRatio(SvgAlignment.None)));
        Assert.Equal("none", SvgDomBuilder.FormatAspect(new SvgAspectRatio(SvgAlignment.XMidYMid, Stretch.Fill)));
        SvgNode node = new(SvgNodeKind.Use) { Id = "shape", Href = "target", Fill = new SvgReferenceBrush("gradient"), ClipPathId = "clip", MaskId = "mask" };
        Dictionary<string, string?> first = [];
        Dictionary<string, string?> second = [];
        SvgDomBuilder.VisitAttributeChanges(null, node, "one_", false, (name, value) => first.Add(name, value));
        SvgDomBuilder.VisitAttributeChanges(null, node, "two_", false, (name, value) => second.Add(name, value));
        Assert.Equal("one_shape", first["id"]);
        Assert.Equal("two_shape", second["id"]);
        Assert.Equal("#one_target", first["href"]);
        Assert.Equal("url(#one_gradient)", first["fill"]);
        Assert.Equal("url(#two_gradient)", second["fill"]);
        Assert.Equal("url(#one_clip)", first["clip-path"]);
        Assert.Equal("url(#one_mask)", first["mask"]);
        first.Clear();
        SvgDomBuilder.VisitAttributeChanges(null, node, "one_", true, (name, value) => first.Add(name, value));
        Assert.False(first.ContainsKey("id"), "Artwork updates must preserve the outer control's DOM id.");
    }
}

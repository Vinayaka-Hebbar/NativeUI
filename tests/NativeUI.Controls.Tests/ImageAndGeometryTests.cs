using System.Collections.Concurrent;
using NativeUI.Core;
using NativeUI.Graphics;
using NativeUI.Graphics.Skia;
using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Sample;
using NativeUI.Tests;
using SkiaSharp;
using ShapePath = NativeUI.Controls.Shapes.Path;

namespace NativeUI.Controls.Tests;

internal static class ImageAndGeometryTests
{
    public static void ImagePageUsesItsCompiledStylesOnDesktop()
    {
        Theme previous = Application.Current.RequestedTheme;
        try
        {
            Application.Current.RequestedTheme = Theme.Dark;
            using GalleryView gallery = new();
            GalleryViewModel model = (GalleryViewModel)(gallery.DataContext ?? throw new InvalidOperationException("Missing gallery model."));
            model.SelectedPage = model.NavigationItems.Single(item => item.Tag == "Images");
            ImageSampleView page = (ImageSampleView)(model.CurrentPage ?? throw new InvalidOperationException("Missing image page."));
            Text title = (Text)page.Children[0];
            Text description = (Text)page.Children[1];
            page.RecomputeStyle();
            title.RecomputeStyle();
            description.RecomputeStyle();
            page.ImageStatus.RecomputeStyle();
            Assert.Equal(24d, title.ComputedStyle.FontSize);
            Assert.Equal(14d, description.ComputedStyle.FontSize);
            Assert.Equal(13d, page.ImageStatus.ComputedStyle.FontSize);
            Assert.Equal(Color.FromRgbBytes(250, 250, 250), title.ComputedStyle.TextColor);
            Assert.Equal(Color.FromRgbBytes(250, 250, 250), page.ImageStatus.ComputedStyle.TextColor);
            Application.Current.RequestedTheme = Theme.Light;
            gallery.InvalidateStyle();
            title.RecomputeStyle();
            Assert.Equal(Color.FromRgbBytes(15, 23, 42), title.ComputedStyle.TextColor);
        }
        finally
        {
            Application.Current.RequestedTheme = previous;
        }
    }

    public static void PercentageSizingUsesDefiniteDimensions()
    {
        SvgNode definition = SvgParser.Parse(TestRunner.ReadFixture("Images/percentages.svg"));
        Assert.Equal(new Size(100, 50), definition.GetIntrinsicSize(new Size(200, double.PositiveInfinity)));
        Assert.Equal(new Size(20, 10), definition.GetIntrinsicSize());
        Assert.Equal(new Size(100, 40), definition.GetIntrinsicSize(new Size(200, 80)));
        using View root = new();
        using Svg svg = new() { Definition = definition };
        root.Children.Add(svg);
        LayoutEngine engine = new(SkiaTextShaper.Shared);
        LayoutBox child = engine.ComputeLayout(root, new Size(200, 200)).Children[0];
        Assert.True(double.IsFinite(child.ContentBox.Width) && double.IsFinite(child.ContentBox.Height));
        Assert.Equal(100d, child.ContentBox.Width);
        Assert.Equal(50d, child.ContentBox.Height);
    }

    public static void SvgTextKeepsDocumentOrder()
    {
        SvgNode definition = SvgParser.Parse(TestRunner.ReadFixture("Images/mixed-text.svg"));
        SvgNode text = definition.Children[0];
        Assert.Equal("before", text.Children[0].Text);
        Assert.Equal(SvgNodeKind.TextSpan, text.Children[1].Kind);
        Assert.Equal("middle", text.Children[1].Children[0].Text);
        Assert.Equal("after", text.Children[2].Text);
    }

    public static void CompiledFactoryIsRetainedAndInvalidatesOnce()
    {
        QueuedDispatcher dispatcher = new();
        int factories = 0;
        int opened = 0;
        int invalidations = 0;
        PathGeometry first = Geometry.Parse("M0 0 L24 0 L12 24 Z");
        using Image image = new()
        {
            Dispatcher = dispatcher,
            Source = new CompiledSvgSource(() =>
            {
                factories++;
                return new Svg { Definition = new SvgNode(SvgNodeKind.Svg)
                {
                    ViewBox = new Rect(0, 0, 24, 24),
                    Children = [new SvgNode(SvgNodeKind.Path) { Geometry = first, Fill = Brushes.Blue }]
                } };
            })
        };
        image.ImageOpened += (_, _) => opened++;
        image.Attach(SkiaImageDecoder.Shared);
        Pump(dispatcher, () => opened == 1);
        Svg instance = image.VectorImage ?? throw new InvalidOperationException("Compiled SVG did not open.");
        image.VisualInvalidated += (_, _) => invalidations++;
        SvgNode original = instance.Definition ?? throw new InvalidOperationException("SVG definition is missing.");
        instance.Definition = original with
        {
            Children = [original.Children[0] with { Geometry = Geometry.Parse("M0 0 H24 V24 H0 Z") }]
        };
        Assert.Equal(1, invalidations);
        image.Width = 48;
        image.Stretch = Stretch.Fill;
        image.Attach(SkiaImageDecoder.Shared);
        dispatcher.Drain();
        Assert.Same(instance, image.VectorImage);
        Assert.Equal(1, factories);
        Assert.Equal(1, opened);
        image.Detach();
        Assert.Null(image.VectorImage);
        image.Attach(SkiaImageDecoder.Shared);
        Pump(dispatcher, () => opened == 2);
        Assert.Equal(2, factories);
    }

    public static void SampleContainsWorkingImagePage()
    {
        using ImageSampleView page = new();
        Assert.True(page.Preview.Source is CompiledSvgSource,
            "The resource URI must be replaced by a compiled factory using its logical resource name.");
        QueuedDispatcher dispatcher = new();
        Image preview = page.Preview;
        preview.Dispatcher = dispatcher;
        int opened = 0;
        int failed = 0;
        preview.ImageOpened += (_, _) => opened++;
        preview.ImageFailed += (_, _) => failed++;
        preview.Attach(SkiaImageDecoder.Shared);
        Pump(dispatcher, () => opened == 1 || failed > 0);
        Assert.Equal(0, failed);
        preview.Source = "resource://NativeUI.Sample.Shared/Assets/logo.png";
        Pump(dispatcher, () => opened == 2 || failed > 0);
        Assert.Equal(new Size(96, 64), preview.GetIntrinsicSize());
        preview.Source = "resource://NativeUI.Sample.Shared/Assets/missing.png";
        Pump(dispatcher, () => failed == 1);
        Assert.Null(preview.DecodedImage);
        Assert.False(preview.IsLoading);
        Assert.Equal(2, opened);
        preview.Source = null;
        Assert.Equal(1, failed);
        SvgNode inline = page.InlineArtwork.Definition ?? throw new InvalidOperationException("Inline SVG definition is missing.");
        Assert.Equal(3, inline.Children.Length);
        Assert.Equal("Native", inline.Children[2].Children[0].Text);
    }

    public static void CompiledGradientMatchesRuntimeSvg()
    {
        using ImageSampleView page = new();
        CompiledSvgSource source = page.Preview.Source as CompiledSvgSource
            ?? throw new InvalidOperationException("The sample SVG must be compiled.");
        using Svg svg = source.Factory();
        SvgNode definition = svg.Definition ?? throw new InvalidOperationException("Compiled definition is missing.");
        SvgNode gradient = definition.Children[0].Children[0];
        Assert.Equal(Color.FromRgbBytes(0x25, 0x63, 0xeb), gradient.Children[0].StopColor);
        using SKBitmap compiled = new(96, 64);
        using SKBitmap runtime = new(96, 64);
        using SKCanvas compiledCanvas = new(compiled);
        using SKCanvas runtimeCanvas = new(runtime);
        using SkiaDrawingContext compiledContext = new(compiledCanvas);
        using SkiaDrawingContext runtimeContext = new(runtimeCanvas);
        compiledCanvas.Clear(SKColors.Transparent);
        runtimeCanvas.Clear(SKColors.Transparent);
        compiledContext.DrawSvg(definition, new Rect(0, 0, 96, 64));
        runtimeContext.DrawSvg(SvgParser.Parse(TestRunner.ReadFixture("Images/logo.svg")), new Rect(0, 0, 96, 64));
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 96; x++) Assert.Equal(runtime.GetPixel(x, y), compiled.GetPixel(x, y));
        }
    }

    public static void TypedDefinitionsRenderReferencesClipsMasksAndText()
    {
        SvgNode definition = SvgParser.Parse(TestRunner.ReadFixture("Images/typed-scene.svg"));
        Assert.Equal(SvgNodeKind.Group, definition.Children[1].Kind);
        Assert.Equal(2f, definition.Children[1].Transform?.M31);
        Assert.Equal("tile", definition.Children[1].Children[0].Href);
        using SKBitmap bitmap = new(64, 24);
        using SKCanvas canvas = new(bitmap);
        using SkiaDrawingContext context = new(canvas);
        canvas.Clear(SKColors.Transparent);
        context.DrawSvg(definition, new Rect(0, 0, 64, 24));
        Assert.Equal(SKColors.Lime, bitmap.GetPixel(4, 18));
        Assert.Equal((byte)0, bitmap.GetPixel(4, 4).Alpha);
        Assert.Equal((byte)0, bitmap.GetPixel(18, 18).Alpha);
        Assert.True(bitmap.GetPixel(27, 2).Red > bitmap.GetPixel(27, 2).Blue);
        Assert.True(bitmap.GetPixel(48, 2).Blue > bitmap.GetPixel(48, 2).Red);
        SvgNode text = definition.Children[3];
        Assert.Equal("A", text.Children[0].Text);
        Assert.Equal(SvgNodeKind.TextSpan, text.Children[1].Kind);
        Assert.Equal("C", text.Children[2].Text);
    }

    public static void GoogleSvgResourceIsCompiledAndEmbedded()
    {
        using ImageSampleView page = new();
        CompiledSvgSource source = page.GoogleLogo.Source as CompiledSvgSource
            ?? throw new InvalidOperationException("The Google resource must compile to an SVG factory.");
        using Svg svg = source.Factory();
        Assert.Equal(new Size(74, 24), svg.GetIntrinsicSize());
        using Stream resource = typeof(ImageSampleView).Assembly.GetManifestResourceStream("Assets/google.svg")
            ?? throw new InvalidOperationException("The Google SVG resource is missing from the sample assembly.");
        using StreamReader reader = new(resource);
        SvgNode embedded = SvgParser.Parse(reader.ReadToEnd());
        Assert.Equal(svg.GetIntrinsicSize(), embedded.GetIntrinsicSize());
        SvgNode definition = svg.Definition ?? throw new InvalidOperationException("Compiled definition is missing.");
        Assert.Equal(6, definition.Children.Length);
        Assert.Equal(new SolidColorBrush(Color.FromRgbBytes(0x42, 0x85, 0xF4)), definition.Children[0].Fill);
        using Svg second = source.Factory();
        Assert.False(ReferenceEquals(svg.Definition, second.Definition));
        SvgNode secondDefinition = second.Definition ?? throw new InvalidOperationException("Second definition is missing.");
        Assert.False(ReferenceEquals(svg, second));
        SvgNode original = definition;
        svg.Definition = definition with { Children = [definition.Children[0] with { Fill = Brushes.Black }, .. definition.Children.Skip(1)] };
        Assert.Same(secondDefinition, second.Definition);
        svg.Definition = original;
        using SKBitmap bitmap = new(74, 24);
        using SKCanvas canvas = new(bitmap);
        using SkiaDrawingContext context = new(canvas);
        canvas.Clear(SKColors.Transparent);
        context.DrawSvg(definition, new Rect(0, 0, 74, 24));
        int paintedPixels = 0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).Alpha > 0)
                {
                    paintedPixels++;
                }
            }
        }

        Assert.True(paintedPixels > 200, "The compiled Google SVG must draw its wordmark.");
    }

    public static void SvgAndArcsRenderWithActualSkia()
    {
        foreach (bool large in new[] { false, true })
        {
            foreach (SweepDirection direction in Enum.GetValues<SweepDirection>())
            {
                PathGeometry geometry = new([new PathFigure
                {
                    StartPoint = new Point(10, 20),
                    Segments = [new ArcSegment { Point = new Point(60, 30), Size = new Size(30, 20), IsLargeArc = large, SweepDirection = direction }]
                }]);
                using SKPath actual = SkiaDrawingContext.CreatePath(geometry);
                using SKPath expected = new();
                expected.MoveTo(10, 20);
                expected.ArcTo(30, 20, 0, large ? SKPathArcSize.Large : SKPathArcSize.Small,
                    direction == SweepDirection.Clockwise ? SKPathDirection.Clockwise : SKPathDirection.CounterClockwise, 60, 30);
                Assert.Equal(expected.TightBounds, actual.TightBounds);
            }
        }

        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(TestRunner.ReadFixture("Images/logo.svg"));
        using IDecodedImage decoded = SkiaImageDecoder.Shared.DecodeAsync(bytes, "image/svg+xml", default).GetAwaiter().GetResult();
        using SKBitmap bitmap = new(96, 64);
        using SKCanvas canvas = new(bitmap);
        using SkiaDrawingContext context = new(canvas);
        canvas.Clear(SKColors.Transparent);
        context.DrawImage(decoded, new Rect(0, 0, 96, 64));
        Assert.True(bitmap.GetPixel(10, 30).Alpha > 200);
        canvas.Clear(SKColors.Transparent);
        context.DrawSvg(SvgParser.Parse(TestRunner.ReadFixture("Images/logo.svg")), new Rect(0, 0, 96, 64));
        Assert.True(bitmap.GetPixel(10, 30).Alpha > 200);
    }

    private static void Pump(QueuedDispatcher dispatcher, Func<bool> complete)
    {
        DateTime limit = DateTime.UtcNow.AddSeconds(10);
        while (!complete() && DateTime.UtcNow < limit)
        {
            dispatcher.Drain();
            Thread.Sleep(1);
        }

        dispatcher.Drain();
        Assert.True(complete(), "Image operation did not complete.");
    }

    private sealed class QueuedDispatcher : IDispatcher
    {
        private readonly ConcurrentQueue<Action> _actions = new();
        public bool CheckAccess() => true;
        public void Invoke(Action action)
        {
            _actions.Enqueue(action);
        }
        public void Drain()
        {
            while (_actions.TryDequeue(out Action? action))
            {
                action();
            }
        }
    }
}

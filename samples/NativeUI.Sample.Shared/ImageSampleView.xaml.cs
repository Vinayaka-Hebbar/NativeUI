using NativeUI.Controls;
using NativeUI.Layout;
using NativeUI.Media;
using ShapePath = NativeUI.Controls.Shapes.Path;

namespace NativeUI.Sample;

/// <summary>Demonstrates image sources, loading events, stretch, and live vector updates.</summary>
public partial class ImageSampleView
{
    private readonly ImageSource? _compiledSource;
    private int _openedCount;
    private bool _alternate;
    private bool _alternateSvg;

    /// <summary>Creates the image and vector examples without requiring network access.</summary>
    public ImageSampleView()
    {
        InitializeComponent();
        _compiledSource = Preview.Source;
        ShowSvgButton.Click += (_, _) => Preview.Source = _compiledSource;
        ShowBitmapButton.Click += (_, _) => Preview.Source = "resource://NativeUI.Sample.Shared/Assets/logo.png";
        ShowFailureButton.Click += (_, _) => Preview.Source = "resource://NativeUI.Sample.Shared/Assets/missing.png";
        ClearImageButton.Click += (_, _) =>
        {
            Preview.Source = null;
            ImageStatus.Value = "Image cleared.";
        };
        StretchNoneButton.Click += (_, _) => SetStretch(Stretch.None);
        StretchFillButton.Click += (_, _) => SetStretch(Stretch.Fill);
        StretchUniformButton.Click += (_, _) => SetStretch(Stretch.Uniform);
        StretchCoverButton.Click += (_, _) => SetStretch(Stretch.UniformToFill);
        ChangePathButton.Click += (_, _) =>
        {
            _alternate = !_alternate;
            LivePath.Data = Geometry.Parse(_alternate ? "M0 0 H48 V48 H0 Z" : "M0 0 L48 0 L24 48 Z");
        };
        EditableImage.Source = new CompiledSvgSource(static () => new Svg
        {
            Definition = new SvgNode(SvgNodeKind.Svg)
            {
                ViewBox = new Rect(0, 0, 48, 48),
                Children = [new SvgNode(SvgNodeKind.Path) { Geometry = Geometry.Parse("M0 0 L48 0 L24 48 Z"), Fill = Brushes.Blue }]
            }
        });
        ChangeSvgButton.Click += (_, _) =>
        {
            if (EditableImage.VectorImage is Svg svg && svg.Definition is SvgNode definition)
            {
                _alternateSvg = !_alternateSvg;
                SvgNode path = definition.Children[0] with
                {
                    Geometry = Geometry.Parse(_alternateSvg ? "M0 0 H48 V48 H0 Z" : "M0 0 L48 0 L24 48 Z"),
                    Fill = _alternateSvg ? Brushes.Green : Brushes.Blue
                };
                svg.Definition = definition with { Children = [path] };
            }
        };
    }

    private void SetStretch(Stretch stretch)
    {
        Preview.Stretch = stretch;
        StretchStatus.Value = "Stretch: " + stretch;
    }

    private void OnImageOpened(object? sender, EventArgs args)
    {
        _openedCount++;
        ImageStatus.Value = $"Image ready. Opened events: {_openedCount}.";
    }

    private void OnImageFailed(object? sender, ImageFailedEventArgs args)
    {
        ImageStatus.Value = "Image failed: " + args.Exception.Message;
    }
}

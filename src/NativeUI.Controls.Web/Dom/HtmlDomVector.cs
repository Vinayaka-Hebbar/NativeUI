using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Layout;
using NativeUI.Media;

namespace NativeUI.Controls.Web;

/// <summary>Presents standalone shapes and SVG viewports using native SVG elements.</summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomVector : HtmlDomElement
{
    private SvgNode? _appliedDefinition;
    private bool _updating;

    internal HtmlDomVector(UIElement source, JSObject? element) : base(source, element, "svg")
    {
    }

    /// <inheritdoc />
    public override string ClassName
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            if (Element is null)
            {
                return base.ClassName;
            }

            return ImageInterop.GetSvgClassName(Element);
        }
    }

    /// <inheritdoc />
    public override void SetClassName(string className)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentNullException.ThrowIfNull(className);
        if (Element is null)
        {
            base.SetClassName(className);
            return;
        }

        ImageInterop.SetSvgClassName(Element, className);
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        SetStyleProperty("overflow", "hidden");
        Source.VisualInvalidated += OnInvalidated;
        UpdateVector();
    }

    private void OnInvalidated(object? sender, EventArgs args) => UpdateVector();

    private void UpdateVector()
    {
        if (Element is null || IsDisposed || _updating)
        {
            return;
        }

        _updating = true;
        try
        {
            Stretch stretch;
            SvgNode definition;
            Size natural;
            if (Source is Shapes.Shape shape)
            {
                natural = shape.GetIntrinsicSize();
                definition = SvgDomBuilder.CreateShapeDefinition(shape, shape.RenderSize);
                stretch = shape.Stretch;
            }
            else if (Source is Svg svg)
            {
                natural = svg.GetIntrinsicSize();
                definition = SvgDomBuilder.Definition(svg);
                stretch = svg.Stretch;
            }
            else
            {
                return;
            }

            SvgLength? width = Source.ComputedStyle.Width.IsAuto ? new SvgLength(natural.Width) : definition.Width;
            SvgLength? height = Source.ComputedStyle.Height is null ? new SvgLength(natural.Height) : definition.Height;
            SvgAspectRatio? aspect = stretch switch
            {
                Stretch.Fill => new SvgAspectRatio(SvgAlignment.None, Stretch.Fill),
                Stretch.UniformToFill => new SvgAspectRatio(SvgAlignment.XMidYMid, Stretch.UniformToFill),
                _ => null
            };

            // The applied definition includes control viewport values; child artwork stays unchanged.
            if (definition.Width != width || definition.Height != height || definition.AspectRatio != aspect)
            {
                definition = definition with { Width = width, Height = height, AspectRatio = aspect };
            }

            SvgDomBuilder.Update(Element, _appliedDefinition, definition, $"nui{NodeId}_");
            _appliedDefinition = definition;
        }
        finally
        {
            _updating = false;
        }
    }

    /// <inheritdoc />
    protected override void OnDisposing()
    {
        Source.VisualInvalidated -= OnInvalidated;
        _appliedDefinition = null;
        base.OnDisposing();
    }
}

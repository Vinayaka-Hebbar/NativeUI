using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;
using NativeUI.Media;

namespace NativeUI.Controls.Web;

/// <summary>Presents an Image as a browser image or inline SVG without changing its public control identity.</summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomImage : HtmlDomElement
{
    private readonly Image _image;
    private object? _content;
    private SvgNode? _definition;
    private bool _updating;

    internal HtmlDomImage(Image image, JSObject? element) : base(image, element, "img")
    {
        _image = image;
    }

    /// <inheritdoc />
    public override string ClassName
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            if (Element is null || TagName != "svg")
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
        if (Element is null || TagName != "svg")
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
        _image.VisualInvalidated += OnInvalidated;
        if (Element is not null)
        {
            _image.Attach(BrowserImageDecoder.Shared, new Uri(ImageInterop.BaseUri()));
        }

        UpdatePresentation();
    }

    private void OnInvalidated(object? sender, EventArgs args) => UpdatePresentation();

    private void UpdatePresentation()
    {
        if (Element is null || IsDisposed || _updating)
        {
            return;
        }

        _updating = true;
        try
        {
            object? content = (object?)_image.VectorImage ?? _image.DecodedImage;
            if (!ReferenceEquals(content, _content))
            {
                _content = content;
                _definition = null;
                if (_image.VectorImage is Svg)
                {
                    ReplaceElement(ImageInterop.CreateSvgElement("svg"), "svg");
                }
                else if (_image.DecodedImage is BrowserDecodedImage decoded)
                {
                    ReplaceElement(ImageInterop.CloneImage(decoded.Element), "img");
                }
                else
                {
                    ReplaceElement(DomInterop.CreateElement("img"), "img");
                }
            }

            SetStyleProperty("overflow", "hidden");
            SetStyleProperty("object-fit", _image.Stretch switch
            {
                Stretch.None => "none",
                Stretch.Fill => "fill",
                Stretch.UniformToFill => "cover",
                _ => "contain"
            });
            SetStyleProperty("object-position", "center");
            if (TagName == "svg" && _image.VectorImage is Svg svg)
            {
                SvgNode definition = SvgDomBuilder.Definition(svg);
                SvgDomBuilder.Update(Element, _definition, definition, $"nui{NodeId}_");
                _definition = definition;
                SetAttribute("preserveAspectRatio", SvgDomBuilder.Aspect(_image.Stretch));
                SetAttribute("role", _image.IsDecorative ? null : "img");
                SetAttribute("aria-label", _image.IsDecorative ? null : _image.AltText);
                SetAttribute("aria-hidden", _image.IsDecorative ? "true" : null);
                SetAttribute("alt", null);
                NativeUI.Layout.Size natural = svg.GetIntrinsicSize();
                if (_image.ComputedStyle.Width.IsAuto)
                {
                    SetStyleProperty("width", SvgDomBuilder.Number(natural.Width) + "px");
                }

                if (_image.ComputedStyle.Height.IsAuto)
                {
                    SetStyleProperty("height", "auto");
                }

                SetStyleProperty("aspect-ratio", $"{SvgDomBuilder.Number(natural.Width)} / {SvgDomBuilder.Number(natural.Height)}");
            }
            else
            {
                SetAttribute("alt", _image.IsDecorative ? "" : _image.AltText);
                SetAttribute("role", _image.IsDecorative ? "presentation" : null);
                SetAttribute("aria-label", null);
                SetAttribute("aria-hidden", _image.IsDecorative ? "true" : null);
                SetStyleProperty("aspect-ratio", null);
                if (_image.ComputedStyle.Width.IsAuto)
                {
                    SetStyleProperty("width", null);
                }

                if (_image.ComputedStyle.Height.IsAuto)
                {
                    SetStyleProperty("height", null);
                }
            }
        }
        finally
        {
            _updating = false;
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        UpdatePresentation();
    }

    /// <inheritdoc />
    protected override void OnDisposing()
    {
        _image.VisualInvalidated -= OnInvalidated;
        _image.Detach();
        _content = null;
        _definition = null;
        base.OnDisposing();
    }
}

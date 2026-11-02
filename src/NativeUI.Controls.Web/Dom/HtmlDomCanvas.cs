using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Layout;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes a <see cref="CanvasView"/> with an HTML <c>&lt;canvas&gt;</c> element, whose size comes from CSS. Draws once per
/// frame after an invalidation or a size change. Without a browser element, invalidations do nothing.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomCanvas : HtmlDomElement
{
    private readonly CanvasView _canvasView;
    private int? _renderRequestId;

    internal HtmlDomCanvas(CanvasView canvasView, JSObject? element)
        : base(canvasView, element, "canvas")
    {
        _canvasView = canvasView;
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        _canvasView.VisualInvalidated += OnVisualInvalidated;
        if (Element is JSObject element)
        {
            // The observer reports the first size once the element is connected and visible.
            CanvasInterop.ObserveResize(element, ScheduleRender);
        }
    }

    /// <inheritdoc />
    protected override void OnDisposing()
    {
        _canvasView.VisualInvalidated -= OnVisualInvalidated;
        if (_renderRequestId is int requestId)
        {
            DomInterop.CancelAnimationFrame(requestId);
            _renderRequestId = null;
        }

        if (Element is JSObject element)
        {
            CanvasInterop.UnobserveResize(element);
        }

        base.OnDisposing();
    }

    private void OnVisualInvalidated(object? sender, EventArgs e)
    {
        ScheduleRender();
    }

    private void ScheduleRender()
    {
        if (IsDisposed || Element is null || _renderRequestId is not null)
        {
            return;
        }

        _renderRequestId = DomInterop.RequestAnimationFrame(OnAnimationFrame);
    }

    private void OnAnimationFrame()
    {
        _renderRequestId = null;
        if (!IsDisposed)
        {
            Render();
        }
    }

    private void Render()
    {
        if (Element is not JSObject element)
        {
            return;
        }

        double width;
        double height;
        using (JSObject size = CanvasInterop.GetContentSize(element))
        {
            width = size.GetPropertyAsDouble("width");
            height = size.GetPropertyAsDouble("height");
        }

        // A hidden canvas has no size. The observer reports the size when it is shown.
        if (width <= 0 || height <= 0)
        {
            return;
        }

        WebCanvas2DDrawingContext context = new(element, width, height);
        try
        {
            context.Begin();
            _canvasView.RenderCanvas(context, new Rect(0, 0, width, height));
        }
        finally
        {
            context.End();
        }
    }
}

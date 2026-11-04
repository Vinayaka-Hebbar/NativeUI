using System.Text;
using NativeUI.Core;
using NativeUI.Graphics;
using NativeUI.Layout;
using NativeUI.Media;

namespace NativeUI.Controls;

/// <summary>Displays raster images and SVG content with a stable control identity.</summary>
public class Image : UIElement, IIntrinsicSize
{
    private CancellationTokenSource? _cancellation;
    private IImageDecoder? _decoder;
    private Uri? _baseUri;
    private ImageLoadOptions _loadOptions = new();
    private int _generation;
    private bool _disposed;
    private IDecodedImage? _decoded;
    private Svg? _vector;

    /// <summary>Identifies the Source bindable property.</summary>
    public static readonly BindableProperty<ImageSource?> SourceProperty =
        new BindableProperty<ImageSource?>(nameof(Source), typeof(Image), null,
            static (owner, _, _) => ((Image)owner).Restart())
        {
            ValueConverter = static value => value is string uri ? (ImageSource?)uri : throw new InvalidCastException("Image.Source requires an ImageSource or URI string.")
        };

    /// <summary>Gets or sets the Source value.</summary>
    public ImageSource? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>Identifies the Stretch bindable property.</summary>
    public static readonly BindableProperty<Stretch> StretchProperty =
        BindableProperty.Create<Image, Stretch>(nameof(Stretch), defaultValue: Stretch.Uniform,
            propertyChanged: static (owner, _, _) => owner.InvalidateVisual());

    /// <summary>Gets or sets the Stretch value.</summary>
    public Stretch Stretch
    {
        get => GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    /// <summary>Identifies the AltText bindable property.</summary>
    public static readonly BindableProperty<string> AltTextProperty =
        BindableProperty.Create<Image, string>(nameof(AltText), defaultValue: string.Empty,
            propertyChanged: static (owner, _, _) => owner.InvalidateVisual());

    /// <summary>Gets or sets the AltText value.</summary>
    public string AltText
    {
        get => GetValue(AltTextProperty);
        set => SetValue(AltTextProperty, value);
    }

    /// <summary>Identifies the IsDecorative bindable property.</summary>
    public static readonly BindableProperty<bool> IsDecorativeProperty =
        BindableProperty.Create<Image, bool>(nameof(IsDecorative), defaultValue: false,
            propertyChanged: static (owner, _, _) => owner.InvalidateVisual());

    /// <summary>Gets or sets the IsDecorative value.</summary>
    public bool IsDecorative
    {
        get => GetValue(IsDecorativeProperty);
        set => SetValue(IsDecorativeProperty, value);
    }

    private static readonly BindableProperty<bool> IsLoadingProperty = BindableProperty.Create<Image, bool>(nameof(IsLoading));

    /// <summary>Gets or sets encoded loading limits. Replacing options reloads the active source.</summary>
    public ImageLoadOptions LoadOptions
    {
        get => _loadOptions;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value.MaximumEncodedBytes);
            if (ReferenceEquals(_loadOptions, value)) return;
            _loadOptions = value;
            Restart();
        }
    }

    /// <summary>Gets whether the active source is being prepared.</summary>
    public bool IsLoading => GetValue(IsLoadingProperty);

    /// <summary>Occurs once when the active source is ready for presentation, including compiled SVGs.</summary>
    public event EventHandler? ImageOpened;

    /// <summary>Occurs when the active source cannot be prepared.</summary>
    public event EventHandler<ImageFailedEventArgs>? ImageFailed;

    /// <inheritdoc />
    public override string TagName => "image";

    /// <summary>Gets the owned decoded image, or null for vector/empty content.</summary>
    public IDecodedImage? DecodedImage => _decoded;

    /// <summary>Gets the owned compiled SVG instance; painting never creates it.</summary>
    public Svg? VectorImage => _vector;

    /// <inheritdoc />
    public Size GetIntrinsicSize(Size? containingSize = null) => _vector?.GetIntrinsicSize(containingSize) ?? _decoded?.Size ?? Size.Zero;

    /// <summary>Attaches a platform decoder and begins loading the active source once.</summary>
    public void Attach(IImageDecoder decoder, Uri? browserBaseUri = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (ReferenceEquals(_decoder, decoder) && Equals(_baseUri, browserBaseUri)) return;
        Detach();
        _decoder = decoder;
        _baseUri = browserBaseUri;
        Restart();
    }

    /// <summary>Cancels loading and releases the current presentation instance.</summary>
    public void Detach()
    {
        _generation++;
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = null;
        _decoder = null;
        ClearContent();
        if (!_disposed) SetValue(IsLoadingProperty, false);
    }

    private void Restart()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _generation++;
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = null;
        ClearContent();
        SetValue(IsLoadingProperty, false);
        InvalidateVisual();
        if (_decoder is null || Source is null) return;
        _cancellation = new CancellationTokenSource();
        SetValue(IsLoadingProperty, true);
        _ = LoadAsync(Source, _decoder, _generation, _cancellation.Token);
    }

    private async Task LoadAsync(ImageSource source, IImageDecoder decoder, int generation, CancellationToken token)
    {
        IDecodedImage? decoded = null;
        Svg? vector = null;
        Exception? failure = null;
        // Yield even for compiled sources so XAML has time to subscribe its event handlers.
        await Task.Yield();
        try
        {
            token.ThrowIfCancellationRequested();
            if (source is CompiledSvgSource compiled)
            {
                // Visual construction belongs to the UI dispatcher, not a decoder worker.
                Dispatcher.Invoke(() =>
                {
                    if (!token.IsCancellationRequested) vector = compiled.Factory() ?? throw new InvalidOperationException("The SVG factory returned null.");
                });
            }
            else if (source is BitmapImage uri)
            {
                byte[] bytes = await ImageResourceResolver.ReadAsync(uri.Uri, new ImageResourceContext { ApplicationBaseUri = _baseUri, Options = _loadOptions }, token);
                string contentType = ImageResourceResolver.GetContentType(bytes);
                if (contentType == "image/svg+xml")
                {
                    string document = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
                    SvgParser.Parse(document);
                }

                decoded = await decoder.DecodeAsync(bytes, contentType, token);
            }
            else throw new NotSupportedException("Unknown ImageSource type.");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception) { failure = exception; }
        Dispatcher.Invoke(() =>
        {
            if (_disposed || token.IsCancellationRequested || generation != _generation)
            {
                decoded?.Dispose();
                vector?.Dispose();
                return;
            }
            _decoded = decoded;
            _vector = vector;
            if (_vector is not null)
            {
                _vector.Parent = this;
            }
            SetValue(IsLoadingProperty, false);
            InvalidateVisual();
            if (generation != _generation || _disposed) return;
            if (failure is not null) ImageFailed?.Invoke(this, new ImageFailedEventArgs(source, failure));
            else ImageOpened?.Invoke(this, EventArgs.Empty);
        });
    }

    private void ClearContent()
    {
        _decoded?.Dispose();
        _decoded = null;
        if (_vector is not null)
        {
            _vector.Parent = null;
            _vector.Dispose();
        }
        _vector = null;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Detach();
            _disposed = true;
            ImageOpened = null;
            ImageFailed = null;
        }
        base.Dispose(disposing);
    }
}

using NativeUI.Core;
using NativeUI.Media;

namespace NativeUI.Controls;

/// <summary>
/// Represents a picture of a person or an entity, with a text fallback such as initials. The template has an
/// <c>image</c> part and a <c>fallback</c> part, both filling the avatar. The fallback shows until the image opens, and
/// again when the image fails or <see cref="Source"/> is <see langword="null"/>. The avatar clips its parts to its
/// <c>border-radius</c>. Size, colors, and the alignment of the fallback text come from stylesheets.
/// </summary>
public class Avatar : Control
{
    private const string ImagePartName = "image";
    private const string FallbackPartName = "fallback";

    static Avatar()
    {
        PositionProperty.OverrideMetadata<Avatar>(NativeUI.Layout.Position.Relative);
        OverflowXProperty.OverrideMetadata<Avatar>(Overflow.Hidden);
        OverflowYProperty.OverrideMetadata<Avatar>(Overflow.Hidden);
    }

    /// <summary>
    /// Identifies the <see cref="Source"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<ImageSource?> SourceProperty =
        BindableProperty.Create<Avatar, ImageSource?>(
            nameof(Source),
            defaultValue: null,
            propertyChanged: static (owner, _, source) => owner.OnSourceChanged(source));

    /// <summary>
    /// Identifies the <see cref="Fallback"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<string?> FallbackProperty =
        BindableProperty.Create<Avatar, string?>(
            nameof(Fallback),
            defaultValue: null,
            propertyChanged: static (owner, _, fallback) => owner._fallback.Value = fallback ?? string.Empty);

    /// <summary>
    /// Identifies the <see cref="AltText"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<string> AltTextProperty =
        BindableProperty.Create<Avatar, string>(
            nameof(AltText),
            defaultValue: string.Empty,
            propertyChanged: static (owner, _, altText) => owner._image.AltText = altText);

    private Image _image = null!;
    private Text _fallback = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="Avatar"/> class.
    /// </summary>
    public Avatar()
    {
        _image.ImageOpened += (_, _) => ShowImage(true);
        _image.ImageFailed += (_, args) =>
        {
            ShowImage(false);
            ImageFailed?.Invoke(this, args);
        };
    }

    /// <summary>
    /// Occurs when <see cref="Source"/> cannot be loaded. The fallback stays visible.
    /// </summary>
    public event EventHandler<ImageFailedEventArgs>? ImageFailed;

    /// <inheritdoc />
    public override string TagName => "avatar";

    /// <summary>
    /// Gets or sets the picture, or <see langword="null"/> to show only the fallback.
    /// </summary>
    public ImageSource? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>
    /// Gets or sets the text shown while the picture is not shown, such as initials.
    /// </summary>
    public string? Fallback
    {
        get => GetValue(FallbackProperty);
        set => SetValue(FallbackProperty, value);
    }

    /// <summary>
    /// Gets or sets the description of the picture for assistive technologies.
    /// </summary>
    public string AltText
    {
        get => GetValue(AltTextProperty);
        set => SetValue(AltTextProperty, value);
    }

    /// <inheritdoc />
    protected override void BuildTemplate()
    {
        _image = DefinePart(new Image { Stretch = Stretch.UniformToFill, IsVisible = false }, ImagePartName);
        _fallback = DefinePart(new Text { IsTextSelectionEnabled = false }, FallbackPartName);
        FillAvatar(_image);
        FillAvatar(_fallback);
        Children.Add(_fallback);
        Children.Add(_image);
    }

    private static void FillAvatar(UIElement part)
    {
        part.Position = NativeUI.Layout.Position.Absolute;
        part.Top = 0;
        part.Right = 0;
        part.Bottom = 0;
        part.Left = 0;
    }

    private void OnSourceChanged(ImageSource? source)
    {
        ShowImage(false);
        _image.Source = source;
    }

    private void ShowImage(bool isOpened)
    {
        _image.IsVisible = isOpened;
        _fallback.IsVisible = !isOpened;
    }
}

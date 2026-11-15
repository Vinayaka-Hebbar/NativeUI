using System.Text;
using NativeUI.Core;
using NativeUI.Graphics;
using NativeUI.Media;
using NativeUI.Xaml;
using NativeUI.Layout;

namespace NativeUI.Controls;

/// <summary>
/// Displays read-only or bound text content, rendered via HarfBuzz/SkiaSharp on desktop and as a native HTML
/// <c>&lt;span&gt;</c> element on Web. Text selection is enabled by default on both platforms.
/// </summary>
[ContentProperty(nameof(Inlines))]
public class Text : UIElement, ITextSource
{
    private InlineCollection? _inlines;
    private string? _textContent;
    private IReadOnlyList<TextRun>? _textRuns;
    private ComputedStyle? _inlineBaseStyle;
    private ITextLayout? _textLayout;
    private Rect _textBounds;
    private Hyperlink? _pressedHyperlink;
    private Hyperlink? _focusedHyperlink;
    private Hyperlink? _hoveredHyperlink;
    private Point _pressPosition;
    private bool _hasHyperlinks;
    private bool _wasFocusable;
    private int _selectionStart;
    private int _selectionLength;

    /// <summary>Initializes inline pointer and keyboard activation.</summary>
    public Text()
    {
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        PointerMoved += OnPointerMoved;
        PointerExited += OnPointerExited;
        KeyDown += OnKeyDown;
        GotFocus += OnGotFocus;
    }

    /// <inheritdoc />
    public override Cursor Cursor => _hoveredHyperlink?.ComputedStyle.Cursor ?? base.Cursor;

    /// <summary>
    /// Identifies the <see cref="Value"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<string> ValueProperty =
        BindableProperty.Create<Text, string>(
            nameof(Value),
            defaultValue: string.Empty,
            propertyChanged: static (owner, _, _) =>
            {
                owner._textLayout = null;
                owner.ClampSelection();
                owner.InvalidateVisual();
            });

    /// <summary>
    /// Identifies the <see cref="IsTextSelectionEnabled"/> bindable property.
    /// Defaults to <see langword="true"/> so <see cref="Text"/> controls are selectable on both Web and Desktop.
    /// </summary>
    public static readonly BindableProperty<bool> IsTextSelectionEnabledProperty =
        BindableProperty.Create<Text, bool>(
            nameof(IsTextSelectionEnabled),
            defaultValue: true,
            propertyChanged: static (owner, _, newValue) =>
            {
                if (!newValue)
                {
                    owner.ClearSelection();
                }

                owner.InvalidateVisual();
            });

    /// <summary>
    /// Identifies the <see cref="AriaLabel"/> accessibility bindable property.
    /// </summary>
    public static readonly BindableProperty<string?> AriaLabelProperty =
        BindableProperty.Create<Text, string?>(
            nameof(AriaLabel),
            defaultValue: null);

    /// <inheritdoc />
    public override string TagName => "text";

    /// <summary>
    /// Gets or sets the text string displayed by this control.
    /// </summary>
    public string Value
    {
        get => GetValue(ValueProperty) ?? string.Empty;
        set => SetValue(ValueProperty, value);
    }

    /// <summary>
    /// Gets formatted inline content. Nonempty content takes precedence over Value.
    /// </summary>
    public InlineCollection Inlines => _inlines ??= new InlineCollection(this);

    internal bool HasHyperlinks => _hasHyperlinks;

    internal bool HasInlines => _inlines is { Count: > 0 };

    internal event EventHandler? InlinesChanged;

    internal string TextContent
    {
        get
        {
            if (_inlines is null || _inlines.Count == 0)
            {
                return Value;
            }

            if (_textContent is null)
            {
                StringBuilder builder = new();
                AppendText(_inlines, builder);
                _textContent = builder.ToString();
            }

            return _textContent;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the user can select and copy text from this control.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    public bool IsTextSelectionEnabled
    {
        get => GetValue(IsTextSelectionEnabledProperty);
        set => SetValue(IsTextSelectionEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets the accessible label announced by assistive technologies.
    /// </summary>
    public string? AriaLabel
    {
        get => GetValue(AriaLabelProperty);
        set => SetValue(AriaLabelProperty, value);
    }

    /// <summary>
    /// Gets the starting UTF-16 character index of the current selection.
    /// </summary>
    public int SelectionStart => _selectionStart;

    /// <summary>
    /// Gets the UTF-16 character length of the current selection.
    /// </summary>
    public int SelectionLength => _selectionLength;

    /// <summary>
    /// Gets a value indicating whether this control currently has a non-empty text selection.
    /// </summary>
    public bool HasSelection => _selectionLength > 0;

    /// <summary>
    /// Gets the currently selected substring of <see cref="Value"/>, or <see cref="string.Empty"/> if nothing is selected.
    /// </summary>
    public string SelectedText
    {
        get
        {
            if (_selectionLength <= 0 || string.IsNullOrEmpty(TextContent))
            {
                return string.Empty;
            }

            int start = Math.Clamp(_selectionStart, 0, TextContent.Length);
            int len = Math.Clamp(_selectionLength, 0, TextContent.Length - start);
            return len > 0 ? TextContent.Substring(start, len) : string.Empty;
        }
    }

    /// <summary>
    /// Selects the UTF-16 character range <c>[start, start + length)</c>, normalizing endpoints to complete grapheme cluster boundaries.
    /// </summary>
    public void Select(int start, int length)
    {
        if (!IsTextSelectionEnabled || string.IsNullOrEmpty(TextContent) || length <= 0)
        {
            ClearSelection();
            return;
        }

        TextRange normalized = UnicodeTextSegmentation.NormalizeRangeToGraphemeBoundaries(TextContent, start, length);
        if (_selectionStart == normalized.Start && _selectionLength == normalized.Length)
        {
            return;
        }

        _selectionStart = normalized.Start;
        _selectionLength = normalized.Length;
        InvalidateVisual();
    }

    /// <summary>
    /// Selects the entire <see cref="TextContent"/> of this control.
    /// </summary>
    public void SelectAll()
    {
        if (!IsTextSelectionEnabled || string.IsNullOrEmpty(TextContent))
        {
            return;
        }

        Select(0, TextContent.Length);
    }

    /// <summary>
    /// Clears any active text selection on this control.
    /// </summary>
    public void ClearSelection()
    {
        if (_selectionStart == 0 && _selectionLength == 0)
        {
            return;
        }

        _selectionStart = 0;
        _selectionLength = 0;
        InvalidateVisual();
    }



    /// <inheritdoc />
    public override void InvalidateStyle()
    {
        _textRuns = null;
        base.InvalidateStyle();
        InlinesChanged?.Invoke(this, EventArgs.Empty);
    }

    string? ITextSource.Text => TextContent;

    /// <inheritdoc />
    IReadOnlyList<TextRun> ITextSource.TextRuns
    {
        get => GetTextRuns();
    }

    private IReadOnlyList<TextRun> GetTextRuns()
    {
        if (_inlines is null || _inlines.Count == 0)
        {
            return [];
        }

        ComputedStyle style = ComputedStyle;
        if (_textRuns is null || _inlineBaseStyle != style)
        {
            List<TextRun> runs = [];
            int offset = 0;
            AppendRuns(_inlines, runs, ref offset);
            _textRuns = runs.ToArray();
            _inlineBaseStyle = style;
        }

        return _textRuns;
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(DataContextChangedEventArgs args)
    {
        base.OnDataContextChanged(args);
        if (_inlines is not null)
        {
            foreach (Inline inline in _inlines.ToArray())
            {
                inline.SetInheritedDataContext(DataContext);
            }
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && _inlines is not null)
        {
            foreach (Inline inline in _inlines.ToArray())
            {
                inline.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    internal void OnInlinesChanged()
    {
        _textContent = null;
        _textRuns = null;
        _textLayout = null;
        Hyperlink[] links = GetHyperlinks(Inlines).ToArray();
        if (links.Length > 0 && !_hasHyperlinks)
        {
            _wasFocusable = Focusable;
            Focusable = true;
        }
        else if (links.Length == 0 && _hasHyperlinks)
        {
            Focusable = _wasFocusable;
        }

        _hasHyperlinks = links.Length > 0;
        if (_focusedHyperlink is null || !links.Contains(_focusedHyperlink))
        {
            _focusedHyperlink = links.FirstOrDefault();
        }

        _hoveredHyperlink = null;
        ClampSelection();
        InvalidateVisual();
        InlinesChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void UpdateTextLayout(ITextLayout layout, Rect bounds)
    {
        _textLayout = layout;
        _textBounds = bounds;
    }

    private void OnGotFocus(object? sender, RoutedEventArgs args)
    {
        _focusedHyperlink = GetHyperlinks(Inlines).FirstOrDefault();
    }

    private void OnKeyDown(object? sender, KeyEventArgs args)
    {
        // Native DOM anchors own their keyboard activation and tab order.
        if (NativeHandle is not null || !IsEffectivelyEnabled || !IsFocused)
        {
            return;
        }

        if (args.Key == Key.Enter && _focusedHyperlink is Hyperlink link)
        {
            link.PerformClick();
            args.Handled = true;
        }
        else if (args.Key == Key.Tab)
        {
            Hyperlink[] links = GetHyperlinks(Inlines).ToArray();
            int next = Array.IndexOf(links, _focusedHyperlink) + (args.Modifiers.HasFlag(InputModifiers.Shift) ? -1 : 1);
            if (next >= 0 && next < links.Length)
            {
                _focusedHyperlink = links[next];
                InvalidateVisual();
                args.Handled = true;
            }
        }
    }

    private void OnPointerPressed(object? sender, PointerEventArgs args)
    {
        if (NativeHandle is null && args.Buttons.HasFlag(PointerButtons.Primary))
        {
            _pressedHyperlink = GetHyperlinkAt(args.Position);
            _pressPosition = args.Position;
        }
    }

    private void OnPointerReleased(object? sender, PointerEventArgs args)
    {
        Hyperlink? link = _pressedHyperlink;
        _pressedHyperlink = null;
        if (link is null || HasSelection || !IsEffectivelyEnabled ||
            Math.Abs(args.Position.X - _pressPosition.X) > 4 || Math.Abs(args.Position.Y - _pressPosition.Y) > 4)
        {
            return;
        }

        if (ReferenceEquals(link, GetHyperlinkAt(args.Position)))
        {
            _focusedHyperlink = link;
            link.PerformClick();
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs args)
    {
        Hyperlink? hovered = GetHyperlinkAt(args.Position);
        if (!ReferenceEquals(_hoveredHyperlink, hovered))
        {
            _hoveredHyperlink = hovered;
            InvalidateVisual();
        }
    }

    private void OnPointerExited(object? sender, PointerEventArgs args)
    {
        _pressedHyperlink = null;
        if (_hoveredHyperlink is not null)
        {
            _hoveredHyperlink = null;
            InvalidateVisual();
        }
    }

    internal void DrawInlineDecorations(DrawingContext context, ITextLayout layout, Point origin, double opacity)
    {
        Hyperlink? link = _hoveredHyperlink ?? (IsFocused && FocusState == FocusState.Keyboard ? _focusedHyperlink : null);
        int offset = 0;
        if (link is null || !IsEffectivelyEnabled || !TryGetInlineRange(Inlines, link, ref offset, out TextRange range))
        {
            return;
        }

        ComputedStyle style = link.ComputedStyle;
        Paint paint = new(style.TextColor ?? Colors.Black, opacity);
        double thickness = Math.Max(1, style.FontSize / 16);
        foreach (TextLine line in layout.Lines)
        {
            int start = Math.Max(range.Start, line.StartTextIndex);
            int end = Math.Min(range.End, line.EndTextIndex);
            if (end <= start)
            {
                continue;
            }

            foreach (Rect rectangle in layout.HitTestTextRange(start, end - start))
            {
                context.DrawRectangle(new Rect(origin.X + rectangle.X,
                    origin.Y + line.BaselineY + style.FontSize * 0.08, rectangle.Width, thickness), paint);
            }
        }
    }

    private static bool TryGetInlineRange(InlineCollection inlines, Inline target, ref int offset, out TextRange range)
    {
        foreach (Inline inline in inlines)
        {
            int start = offset;
            if (inline is Span span)
            {
                if (TryGetInlineRange(span.Inlines, target, ref offset, out range))
                {
                    return true;
                }
            }
            else
            {
                offset += inline is Run run ? run.Value.Length : 1;
            }

            if (ReferenceEquals(inline, target))
            {
                range = new TextRange(start, offset - start);
                return true;
            }
        }

        range = TextRange.Empty;
        return false;
    }

    private Hyperlink? GetHyperlinkAt(Point position)
    {
        if (_textLayout is null)
        {
            return null;
        }

        TextHitTestResult hit = _textLayout.HitTestPoint(new Point(position.X - _textBounds.X, position.Y - _textBounds.Y));
        if (!hit.IsInside)
        {
            return null;
        }

        int offset = 0;
        return FindHyperlink(Inlines, hit.Range.Start, ref offset, null);
    }

    private static Hyperlink? FindHyperlink(InlineCollection inlines, int position, ref int offset, Hyperlink? parent)
    {
        foreach (Inline inline in inlines)
        {
            if (inline is Span span)
            {
                Hyperlink? link = FindHyperlink(span.Inlines, position, ref offset, span as Hyperlink ?? parent);
                if (link is not null)
                {
                    return link;
                }
            }
            else
            {
                int length = inline is Run run ? run.Value.Length : 1;
                if (position >= offset && position < offset + length)
                {
                    return parent;
                }

                offset += length;
            }
        }

        return null;
    }

    private static IEnumerable<Hyperlink> GetHyperlinks(InlineCollection inlines)
    {
        foreach (Inline inline in inlines)
        {
            if (inline is Hyperlink link)
            {
                yield return link;
            }
            else if (inline is Span span)
            {
                foreach (Hyperlink nested in GetHyperlinks(span.Inlines))
                {
                    yield return nested;
                }
            }
        }
    }

    private static void AppendText(InlineCollection inlines, StringBuilder builder)
    {
        foreach (Inline inline in inlines)
        {
            switch (inline)
            {
                case Run run:
                    builder.Append(run.Value);
                    break;
                case Span span:
                    AppendText(span.Inlines, builder);
                    break;
                case LineBreak:
                    builder.Append('\n');
                    break;
            }
        }
    }

    private static void AppendRuns(InlineCollection inlines, List<TextRun> runs, ref int offset)
    {
        foreach (Inline inline in inlines)
        {
            if (inline is Span span)
            {
                AppendRuns(span.Inlines, runs, ref offset);
                continue;
            }

            int length = inline is Run run ? run.Value.Length : inline is LineBreak ? 1 : 0;
            if (length > 0)
            {
                runs.Add(new TextRun(new TextRange(offset, length), inline.ComputedStyle));
                offset += length;
            }
        }
    }

    private void ClampSelection()
    {
        if (string.IsNullOrEmpty(TextContent))
        {
            _selectionStart = 0;
            _selectionLength = 0;
            return;
        }

        _selectionStart = Math.Clamp(_selectionStart, 0, TextContent.Length);
        _selectionLength = Math.Clamp(_selectionLength, 0, TextContent.Length - _selectionStart);
    }
}

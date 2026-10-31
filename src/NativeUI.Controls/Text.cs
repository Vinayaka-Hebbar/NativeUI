using NativeUI.Core;
using NativeUI.Layout;

namespace NativeUI.Controls;

/// <summary>
/// Displays read-only or bound text content, rendered via HarfBuzz/SkiaSharp on desktop and as a native HTML
/// <c>&lt;span&gt;</c> element on Web. Text selection is enabled by default on both platforms.
/// </summary>
public class Text : UIElement
{
    private int _selectionStart;
    private int _selectionLength;

    /// <summary>
    /// Identifies the <see cref="Value"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<string> ValueProperty =
        BindableProperty.Create<Text, string>(
            nameof(Value),
            defaultValue: string.Empty,
            propertyChanged: static (owner, _, _) =>
            {
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
    public override string StyleTypeName => "text";

    /// <summary>
    /// Gets or sets the text string displayed by this control.
    /// </summary>
    public string Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
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
            if (_selectionLength <= 0 || string.IsNullOrEmpty(Value))
            {
                return string.Empty;
            }

            int start = Math.Clamp(_selectionStart, 0, Value.Length);
            int len = Math.Clamp(_selectionLength, 0, Value.Length - start);
            return len > 0 ? Value.Substring(start, len) : string.Empty;
        }
    }

    /// <summary>
    /// Selects the UTF-16 character range <c>[start, start + length)</c>, normalizing endpoints to complete grapheme cluster boundaries.
    /// </summary>
    public void Select(int start, int length)
    {
        if (!IsTextSelectionEnabled || string.IsNullOrEmpty(Value) || length <= 0)
        {
            ClearSelection();
            return;
        }

        TextRange normalized = UnicodeTextSegmentation.NormalizeRangeToGraphemeBoundaries(Value, start, length);
        if (_selectionStart == normalized.Start && _selectionLength == normalized.Length)
        {
            return;
        }

        _selectionStart = normalized.Start;
        _selectionLength = normalized.Length;
        InvalidateVisual();
    }

    /// <summary>
    /// Selects the entire <see cref="Value"/> of this control.
    /// </summary>
    public void SelectAll()
    {
        if (!IsTextSelectionEnabled || string.IsNullOrEmpty(Value))
        {
            return;
        }

        Select(0, Value.Length);
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
    protected override string? GetLayoutTextContent()
    {
        return Value;
    }

    private void ClampSelection()
    {
        if (string.IsNullOrEmpty(Value))
        {
            _selectionStart = 0;
            _selectionLength = 0;
            return;
        }

        _selectionStart = Math.Clamp(_selectionStart, 0, Value.Length);
        _selectionLength = Math.Clamp(_selectionLength, 0, Value.Length - _selectionStart);
    }
}

using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes an <see cref="Input"/> with a native HTML <c>&lt;input type="text"&gt;</c> element.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomInput : HtmlDomElement, IInputPeer
{
    private readonly Input _input;

    internal HtmlDomInput(Input input, JSObject? element)
        : base(input, element, "input")
    {
        _input = input;
    }

    /// <inheritdoc />
    void IInputPeer.Focus()
    {
        if (Element is JSObject element)
        {
            DomInterop.Focus(element);
        }
    }

    /// <summary>
    /// Applies an edit reported by the browser.
    /// </summary>
    internal void OnBrowserInput(string value)
    {
        _input.Value = value;
    }

    /// <summary>
    /// Applies a focus change reported by the browser.
    /// </summary>
    internal void OnBrowserFocus(bool isFocused)
    {
        _input.SetFocused(isFocused);
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        SetAttribute("type", "text");
        UpdateValue();
        UpdatePlaceholder();
        UpdateReadOnly();
        UpdateEnabledState();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, Input.ValueProperty))
        {
            UpdateValue();
        }
        else if (ReferenceEquals(args.Property, Input.PlaceholderProperty))
        {
            UpdatePlaceholder();
        }
        else if (ReferenceEquals(args.Property, Input.IsReadOnlyProperty))
        {
            UpdateReadOnly();
        }
        else if (ReferenceEquals(args.Property, UIElement.IsEnabledProperty))
        {
            UpdateEnabledState();
        }
    }

    /// <inheritdoc />
    protected override void OnDisposing()
    {
        _input.SetFocused(false);
        base.OnDisposing();
    }

    /// <summary>
    /// Writes only a differing value, because assigning the DOM property moves the caret and cancels IME composition.
    /// </summary>
    private void UpdateValue()
    {
        string value = _input.Value ?? string.Empty;
        if (Element is JSObject element && DomInterop.GetValue(element) != value)
        {
            DomInterop.SetValue(element, value);
        }
    }

    private void UpdatePlaceholder()
    {
        SetAttribute("placeholder", _input.Placeholder);
    }

    private void UpdateReadOnly()
    {
        SetAttribute("readonly", _input.IsReadOnly ? "readonly" : null);
    }

    private void UpdateEnabledState()
    {
        SetAttribute("disabled", _input.IsEnabled ? null : "disabled");
    }
}

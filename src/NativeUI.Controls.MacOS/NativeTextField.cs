using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;

namespace NativeUI.Controls.MacOS;

/// <summary>
/// Presents an <see cref="Input"/> with an <c>NSTextField</c> placed over the window's rendered image, inside a view that
/// applies the clipping of the element's ancestors. Operations must run on the AppKit main thread.
/// </summary>
internal sealed class NativeTextField : INativeHandle, IFocusPeer
{
    private const string ClassName = "NativeUITextField";
    private const string DarkAppearanceName = "NSAppearanceNameDarkAqua";
    private const string LightAppearanceName = "NSAppearanceNameAqua";
    private const nint FocusRingTypeNone = 1;

    // NSTextFieldCell insets its text by this distance, and the text starts at the content box edge on every platform.
    private const double CellHorizontalInset = 2;

    private static readonly Dictionary<nint, NativeTextField> s_fields = [];
    private static nint s_class;
    private static nint s_superclass;

    private readonly Input _input;
    private readonly Window _window;
    private readonly nint _parentView;
    private readonly nint _clipView;
    private readonly nint _field;
    private ComputedStyle? _appliedStyle;
    private Theme? _appliedTheme;
    private bool _disposed;
    private Label? _label;

    /// <summary>
    /// Creates the native views as children of <paramref name="parentView"/> and becomes the handle of <paramref name="input"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException"><paramref name="input"/> already owns a native handle.</exception>
    public NativeTextField(Input input, nint parentView, Window window)
    {
        if (input.NativeHandle is not null)
        {
            throw new InvalidOperationException("The control already owns a native handle.");
        }

        _input = input;
        _window = window;
        _parentView = parentView;
        _clipView = AppKitInterop.SendMessageViewInit(
            AppKitInterop.SendMessage(AppKitInterop.GetClass("NSView"), AppKitInterop.GetSelector("alloc")),
            AppKitInterop.GetSelector("initWithFrame:"),
            default);
        AppKitInterop.SendMessageVoid(_clipView, AppKitInterop.GetSelector("setClipsToBounds:"), true);

        _field = AppKitInterop.SendMessageViewInit(
            AppKitInterop.SendMessage(GetFieldClass(), AppKitInterop.GetSelector("alloc")),
            AppKitInterop.GetSelector("initWithFrame:"),
            default);
        s_fields[_field] = this;
        ConfigureField();

        AppKitInterop.SendMessageVoid(_clipView, AppKitInterop.GetSelector("addSubview:"), _field);
        AppKitInterop.SendMessageVoid(parentView, AppKitInterop.GetSelector("addSubview:"), _clipView);

        UpdateValue();
        UpdatePlaceholder();
        UpdateLabel();
        SetString("setAccessibilityHelp:", _input.ValidationMessage);
        UpdateEditable();
        UpdateEnabled();

        _input.NativeHandle = this;
        _input.PropertyChanged += OnInputPropertyChanged;
    }

    /// <inheritdoc />
    void IFocusPeer.Focus()
    {
        nint window = AppKitInterop.SendMessage(_field, AppKitInterop.GetSelector("window"));
        if (window != nint.Zero)
        {
            _ = AppKitInterop.SendMessage(window, AppKitInterop.GetSelector("makeFirstResponder:"), _field);
        }
    }

    /// <summary>
    /// Ends editing by making the window's content view the first responder, which keeps keyboard events in the window.
    /// </summary>
    void IFocusPeer.Blur()
    {
        nint window = AppKitInterop.SendMessage(_field, AppKitInterop.GetSelector("window"));
        if (window != nint.Zero)
        {
            nint contentView = AppKitInterop.SendMessage(window, AppKitInterop.GetSelector("contentView"));
            _ = AppKitInterop.SendMessage(window, AppKitInterop.GetSelector("makeFirstResponder:"), contentView);
        }
    }

    /// <summary>
    /// Positions the field for the latest layout, in the top-down coordinates of the layout.
    /// </summary>
    /// <param name="clip">The visible part of the element, or an empty rectangle when none of it is visible.</param>
    /// <param name="contentBox">The element's content box.</param>
    /// <param name="style">The element's computed style.</param>
    /// <param name="opacity">The combined opacity of the element and its ancestors.</param>
    /// <param name="parentHeight">The height of the parent view, whose coordinates start at the bottom.</param>
    public void Place(Rect clip, Rect contentBox, ComputedStyle style, double opacity, double parentHeight)
    {
        ApplyStyle(style);

        bool isVisible = clip.Width > 0 && clip.Height > 0;
        AppKitInterop.SendMessageVoid(_clipView, AppKitInterop.GetSelector("setHidden:"), !isVisible);
        if (!isVisible)
        {
            return;
        }

        AppKitInterop.SendMessageVoid(_clipView, AppKitInterop.GetSelector("setAlphaValue:"), opacity);
        AppKitInterop.CGRect clipFrame = AppKitInterop.GetBackingAlignedRect(
            _parentView,
            new AppKitInterop.CGRect(clip.X, parentHeight - clip.Bottom, clip.Width, clip.Height));
        AppKitInterop.SendMessageSetFrame(_clipView, AppKitInterop.GetSelector("setFrame:"), clipFrame);

        // A field draws its text from the top, so a field of its natural height centers the text in a taller content box.
        // The cell and the field editor round a fractional origin differently, which moves the text when editing starts.
        double fieldHeight = AppKitInterop.SendMessageSize(_field, AppKitInterop.GetSelector("intrinsicContentSize")).Height;
        double fieldTop = contentBox.Y + ((contentBox.Height - fieldHeight) / 2.0);
        AppKitInterop.CGRect fieldFrame = AppKitInterop.GetBackingAlignedRect(
            _parentView,
            new AppKitInterop.CGRect(
                contentBox.X - CellHorizontalInset,
                parentHeight - (fieldTop + fieldHeight),
                contentBox.Width + (2 * CellHorizontalInset),
                fieldHeight));
        AppKitInterop.SendMessageSetFrame(
            _field,
            AppKitInterop.GetSelector("setFrame:"),
            new AppKitInterop.CGRect(
                fieldFrame.Origin.X - clipFrame.Origin.X,
                fieldFrame.Origin.Y - clipFrame.Origin.Y,
                fieldFrame.Size.Width,
                fieldFrame.Size.Height));
    }

    /// <summary>
    /// Removes the native views and clears the control's focus state and peer.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _input.PropertyChanged -= OnInputPropertyChanged;
        if (_label is not null)
        {
            _label.VisualInvalidated -= OnLabelInvalidated;
            _label = null;
        }
        InputManager.Current.PrimaryKeyboardDevice.HandleLostFocus(_input);
        if (ReferenceEquals(_input.NativeHandle, this))
        {
            _input.NativeHandle = null;
        }

        s_fields.Remove(_field);
        _ = AppKitInterop.SendMessage(_field, AppKitInterop.GetSelector("abortEditing"));
        AppKitInterop.SendMessageVoid(_field, AppKitInterop.GetSelector("setDelegate:"), nint.Zero);
        _ = AppKitInterop.SendMessage(_clipView, AppKitInterop.GetSelector("removeFromSuperview"));
        _ = AppKitInterop.SendMessage(_field, AppKitInterop.GetSelector("release"));
        _ = AppKitInterop.SendMessage(_clipView, AppKitInterop.GetSelector("release"));
    }

    private static unsafe nint GetFieldClass()
    {
        if (s_class != nint.Zero)
        {
            return s_class;
        }

        s_superclass = AppKitInterop.GetClass("NSTextField");
        nint fieldClass = AppKitInterop.AllocateClassPair(s_superclass, ClassName, 0);

        delegate* unmanaged[Cdecl]<nint, nint, byte> becomeFirstResponderPtr = &OnBecomeFirstResponder;
        delegate* unmanaged[Cdecl]<nint, nint, nint, void> textDidChangePtr = &OnTextDidChange;
        delegate* unmanaged[Cdecl]<nint, nint, nint, void> textDidEndEditingPtr = &OnTextDidEndEditing;
        delegate* unmanaged[Cdecl]<nint, nint, nint, nint, nint, byte> doCommandPtr = &OnDoCommandBySelector;

        _ = AppKitInterop.ClassAddMethod(fieldClass, AppKitInterop.GetSelector("becomeFirstResponder"), (nint)becomeFirstResponderPtr, "c@:");
        _ = AppKitInterop.ClassAddMethod(fieldClass, AppKitInterop.GetSelector("controlTextDidChange:"), (nint)textDidChangePtr, "v@:@");
        _ = AppKitInterop.ClassAddMethod(fieldClass, AppKitInterop.GetSelector("controlTextDidEndEditing:"), (nint)textDidEndEditingPtr, "v@:@");
        _ = AppKitInterop.ClassAddMethod(fieldClass, AppKitInterop.GetSelector("control:textView:doCommandBySelector:"), (nint)doCommandPtr, "c@:@@:");

        AppKitInterop.RegisterClassPair(fieldClass);
        s_class = fieldClass;
        return fieldClass;
    }

    /// <summary>
    /// Reports focus when AppKit makes the field first responder. The begin-editing notification is not used because
    /// it is posted only at the first keystroke.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static byte OnBecomeFirstResponder(nint self, nint cmd)
    {
        AppKitInterop.ObjCSuper super = new(self, s_superclass);
        byte accepted = AppKitInterop.SendSuperMessage(ref super, cmd);
        if (accepted != 0 && s_fields.TryGetValue(self, out NativeTextField? field))
        {
            InputManager.Current.PrimaryKeyboardDevice.HandleGotFocus(field._input, FocusState.Programmatic);

            // AppKit receives the native field interaction outside the window's pointer dispatcher.
            field._window.OverlayManager?.HandlePointerPressed(field._input);
        }

        return accepted;
    }

    /// <summary>
    /// Lets Escape in the field editor close a modal dialog, and Tab and Shift+Tab move focus through the
    /// <see cref="KeyboardDevice"/> instead of the AppKit key view loop. The field editor otherwise consumes these keys.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static byte OnDoCommandBySelector(nint self, nint cmd, nint control, nint textView, nint selector)
    {
        if (!s_fields.TryGetValue(self, out NativeTextField? field))
        {
            return 0;
        }

        bool handled;
        if (selector == AppKitInterop.GetSelector("cancelOperation:"))
        {
            handled = field._window.TryCloseModalDialog();
        }
        else if (selector == AppKitInterop.GetSelector("insertNewline:") && Form.GetForm(field._input) is Form form &&
            (AppKitInterop.SendMessage(textView, AppKitInterop.GetSelector("hasMarkedText")) & 0xFF) == 0)
        {
            form.RequestSubmit();
            handled = true;
        }
        else if (selector == AppKitInterop.GetSelector("insertTab:"))
        {
            handled = InputManager.Current.PrimaryKeyboardDevice.MoveFocus(field._input, FocusNavigationDirection.Next, FocusState.Keyboard);
        }
        else if (selector == AppKitInterop.GetSelector("insertBacktab:"))
        {
            handled = InputManager.Current.PrimaryKeyboardDevice.MoveFocus(field._input, FocusNavigationDirection.Previous, FocusState.Keyboard);
        }
        else
        {
            handled = false;
        }

        return handled ? (byte)1 : (byte)0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnTextDidChange(nint self, nint cmd, nint notification)
    {
        if (s_fields.TryGetValue(self, out NativeTextField? field))
        {
            ValidityState.MarkUserInteraction(field._input);
            field._input.Value = field.ReadFieldValue();
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnTextDidEndEditing(nint self, nint cmd, nint notification)
    {
        if (s_fields.TryGetValue(self, out NativeTextField? field))
        {
            InputManager.Current.PrimaryKeyboardDevice.HandleLostFocus(field._input);
        }
    }

    private void ConfigureField()
    {
        AppKitInterop.SendMessageVoid(_field, AppKitInterop.GetSelector("setBezeled:"), false);
        AppKitInterop.SendMessageVoid(_field, AppKitInterop.GetSelector("setBordered:"), false);
        AppKitInterop.SendMessageVoid(_field, AppKitInterop.GetSelector("setDrawsBackground:"), false);
        AppKitInterop.SendMessageVoid(_field, AppKitInterop.GetSelector("setFocusRingType:"), FocusRingTypeNone);
        AppKitInterop.SendMessageVoid(_field, AppKitInterop.GetSelector("setDelegate:"), _field);

        nint cell = AppKitInterop.SendMessage(_field, AppKitInterop.GetSelector("cell"));
        AppKitInterop.SendMessageVoid(cell, AppKitInterop.GetSelector("setWraps:"), false);
        AppKitInterop.SendMessageVoid(cell, AppKitInterop.GetSelector("setScrollable:"), true);
        AppKitInterop.SendMessageVoid(cell, AppKitInterop.GetSelector("setUsesSingleLineMode:"), true);
    }

    private void UpdateLabel()
    {
        Label? label = Label.GetLabeledBy(_input);
        if (!ReferenceEquals(_label, label))
        {
            if (_label is not null)
            {
                _label.VisualInvalidated -= OnLabelInvalidated;
            }

            _label = label;
            if (_label is not null)
            {
                _label.VisualInvalidated += OnLabelInvalidated;
            }
        }

        SetString("setAccessibilityLabel:", Label.GetAccessibleName(_input));
    }

    private void OnLabelInvalidated(object? sender, EventArgs args)
    {
        SetString("setAccessibilityLabel:", Label.GetAccessibleName(_input));
    }

    private void OnInputPropertyChanged(object? sender, BindablePropertyChangedEventArgs args)
    {
        if (ReferenceEquals(args.Property, Input.ValueProperty))
        {
            UpdateValue();
        }
        else if (ReferenceEquals(args.Property, Validation.ErrorsProperty) ||
            ReferenceEquals(args.Property, ValidityState.StateProperty) ||
            ReferenceEquals(args.Property, Input.RequiredErrorMessageProperty))
        {
            SetString("setAccessibilityHelp:", _input.ValidationMessage);
        }
        else if (ReferenceEquals(args.Property, Label.LabeledByProperty))
        {
            UpdateLabel();
        }
        else if (ReferenceEquals(args.Property, Input.PlaceholderProperty))
        {
            UpdatePlaceholder();
        }
        else if (ReferenceEquals(args.Property, Input.IsReadOnlyProperty))
        {
            UpdateEditable();
        }
        else if (ReferenceEquals(args.Property, UIElement.IsEffectivelyEnabledProperty))
        {
            UpdateEnabled();
        }
    }

    private string ReadFieldValue()
    {
        nint value = AppKitInterop.SendMessage(_field, AppKitInterop.GetSelector("stringValue"));
        return Marshal.PtrToStringUTF8(AppKitInterop.SendMessage(value, AppKitInterop.GetSelector("UTF8String"))) ?? string.Empty;
    }

    /// <summary>
    /// Writes only a differing value, because assigning the field's value moves the caret and cancels IME composition.
    /// </summary>
    private void UpdateValue()
    {
        string value = _input.Value ?? string.Empty;
        if (ReadFieldValue() != value)
        {
            SetString("setStringValue:", value);
        }
    }

    private void UpdatePlaceholder()
    {
        SetString("setPlaceholderString:", _input.Placeholder ?? string.Empty);
    }

    private void UpdateEditable()
    {
        AppKitInterop.SendMessageVoid(_field, AppKitInterop.GetSelector("setEditable:"), !_input.IsReadOnly);
        AppKitInterop.SendMessageVoid(_field, AppKitInterop.GetSelector("setSelectable:"), true);
    }

    private void UpdateEnabled()
    {
        AppKitInterop.SendMessageVoid(_field, AppKitInterop.GetSelector("setEnabled:"), _input.IsEffectivelyEnabled);
    }

    private void SetString(string selector, string value)
    {
        nint text = AppKitInterop.CreateNSString(value);
        AppKitInterop.SendMessageVoid(_field, AppKitInterop.GetSelector(selector), text);
        _ = AppKitInterop.SendMessage(text, AppKitInterop.GetSelector("release"));
    }

    private void ApplyStyle(ComputedStyle style)
    {
        Theme theme = Application.Current.CurrentTheme;
        if (_appliedStyle == style && _appliedTheme == theme)
        {
            return;
        }

        _appliedStyle = style;
        _appliedTheme = theme;

        (byte red, byte green, byte blue, byte alpha) = (style.TextColor ?? Colors.Black).ToSrgbBytes();
        nint textColor = AppKitInterop.SendMessageColor(
            AppKitInterop.GetClass("NSColor"),
            AppKitInterop.GetSelector("colorWithSRGBRed:green:blue:alpha:"),
            red / 255.0,
            green / 255.0,
            blue / 255.0,
            alpha / 255.0);
        AppKitInterop.SendMessageVoid(_field, AppKitInterop.GetSelector("setTextColor:"), textColor);
        AppKitInterop.SendMessageVoid(_field, AppKitInterop.GetSelector("setFont:"), CreateFont(style));

        // The named appearance follows the application theme for colors the style has no value for: placeholder and caret.
        nint appearanceName = AppKitInterop.CreateNSString(theme == Theme.Dark ? DarkAppearanceName : LightAppearanceName);
        nint appearance = AppKitInterop.SendMessage(
            AppKitInterop.GetClass("NSAppearance"),
            AppKitInterop.GetSelector("appearanceNamed:"),
            appearanceName);
        AppKitInterop.SendMessageVoid(_field, AppKitInterop.GetSelector("setAppearance:"), appearance);
        _ = AppKitInterop.SendMessage(appearanceName, AppKitInterop.GetSelector("release"));
    }

    /// <summary>
    /// Returns the font for <paramref name="style"/>, or the system font when the style names no installed family.
    /// </summary>
    private static nint CreateFont(ComputedStyle style)
    {
        if (!string.IsNullOrEmpty(style.FontFamily))
        {
            nint family = AppKitInterop.CreateNSString(style.FontFamily);
            nint manager = AppKitInterop.SendMessage(AppKitInterop.GetClass("NSFontManager"), AppKitInterop.GetSelector("sharedFontManager"));
            nint font = AppKitInterop.SendMessageFontWithFamily(
                manager,
                AppKitInterop.GetSelector("fontWithFamily:traits:weight:size:"),
                family,
                0,
                GetFontManagerWeight(style.FontWeight),
                style.FontSize);
            _ = AppKitInterop.SendMessage(family, AppKitInterop.GetSelector("release"));
            if (font != nint.Zero)
            {
                return font;
            }
        }

        return AppKitInterop.SendMessageSystemFont(
            AppKitInterop.GetClass("NSFont"),
            AppKitInterop.GetSelector("systemFontOfSize:weight:"),
            style.FontSize,
            GetSystemFontWeight(style.FontWeight));
    }

    /// <summary>
    /// Maps a CSS weight to the 0 to 15 scale of <c>NSFontManager</c>.
    /// </summary>
    private static long GetFontManagerWeight(FontWeight weight)
    {
        return weight switch
        {
            FontWeight.Thin => 2,
            FontWeight.Light => 3,
            FontWeight.Medium => 6,
            FontWeight.SemiBold => 8,
            FontWeight.Bold => 9,
            FontWeight.ExtraBold => 10,
            FontWeight.Black => 11,
            _ => 5
        };
    }

    /// <summary>
    /// Maps a CSS weight to the values of the <c>NSFontWeight</c> constants.
    /// </summary>
    private static double GetSystemFontWeight(FontWeight weight)
    {
        return weight switch
        {
            FontWeight.Thin => -0.6,
            FontWeight.Light => -0.4,
            FontWeight.Medium => 0.23,
            FontWeight.SemiBold => 0.3,
            FontWeight.Bold => 0.4,
            FontWeight.ExtraBold => 0.56,
            FontWeight.Black => 0.62,
            _ => 0
        };
    }
}

using System.Collections.Specialized;
using NativeUI.Core;
using NativeUI.Layout;

namespace NativeUI.Controls;

/// <summary>
/// A <see cref="Portal"/> that presents a title, a description, content, and a footer, always in that order. A desktop
/// platform presents it in a child window of its parent's window; Web mounts it as a native <c>&lt;dialog&gt;</c> element.
/// The parts carry the classes <c>dialog-title</c>, <c>dialog-description</c>, <c>dialog-content</c>, and <c>dialog-footer</c>.
/// Do not change <see cref="View.Children"/> directly; use <see cref="Content"/>.
/// </summary>
public class Dialog : Portal, IWindowHostedElement
{
    private const double FooterSpacing = 8;

    /// <summary>
    /// Identifies the <see cref="HasOwnWindow"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> HasOwnWindowProperty =
        BindableProperty.Create<Dialog, bool>(
            nameof(HasOwnWindow),
            defaultValue: true,
            propertyChanged: static (owner, _, _) => owner.InvalidateVisual());

    /// <summary>
    /// Identifies the <see cref="IsModal"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> IsModalProperty =
        BindableProperty.Create<Dialog, bool>(nameof(IsModal), defaultValue: true);

    /// <summary>
    /// Identifies the <see cref="Title"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<string?> TitleProperty =
        BindableProperty.Create<Dialog, string?>(
            nameof(Title),
            defaultValue: null,
            propertyChanged: static (owner, _, _) => owner.UpdateTitle());

    /// <summary>
    /// Identifies the <see cref="Description"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<string?> DescriptionProperty =
        BindableProperty.Create<Dialog, string?>(
            nameof(Description),
            defaultValue: null,
            propertyChanged: static (owner, _, _) => owner.UpdateDescription());

    /// <summary>
    /// Identifies the <see cref="TitleContent"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<UIElement?> TitleContentProperty =
        BindableProperty.Create<Dialog, UIElement?>(
            nameof(TitleContent),
            defaultValue: null,
            propertyChanged: static (owner, _, _) => owner.UpdateTitle());

    /// <summary>
    /// Identifies the <see cref="DescriptionContent"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<UIElement?> DescriptionContentProperty =
        BindableProperty.Create<Dialog, UIElement?>(
            nameof(DescriptionContent),
            defaultValue: null,
            propertyChanged: static (owner, _, _) => owner.UpdateDescription());

    private readonly View _titlePart = new() { Class = ["dialog-title"], IsVisible = false };
    private readonly View _descriptionPart = new() { Class = ["dialog-description"], IsVisible = false };
    private readonly View _contentPart = new() { Class = ["dialog-content"] };
    private readonly StackPanel _footerPart = new()
    {
        Class = ["dialog-footer"],
        IsVisible = false,
        Orientation = Orientation.Horizontal,
        Spacing = FooterSpacing,
        HorizontalAlignment = Alignment.End
    };

    private readonly Text _titleText = new();
    private readonly Text _descriptionText = new();
    private View? _owner;
    private bool _isAttachedToOwner;
    private TaskCompletionSource<object?>? _closeCompletion;
    private object? _closeResult;

    /// <summary>
    /// Gets the element representing the title part of the dialog.
    /// </summary>
    public View TitlePart => _titlePart;

    /// <summary>
    /// Gets the element representing the description part of the dialog.
    /// </summary>
    public View DescriptionPart => _descriptionPart;

    /// <summary>
    /// Initializes a new instance of the <see cref="Dialog"/> class.
    /// </summary>
    public Dialog()
    {
        Children.Add(_titlePart);
        Children.Add(_descriptionPart);
        Children.Add(_contentPart);
        Children.Add(_footerPart);

        ((INotifyCollectionChanged)_footerPart.Children).CollectionChanged += OnFooterChanged;
    }

    /// <inheritdoc />
    public override string TagName => "dialog";

    /// <summary>
    /// Gets or sets the view that hosts a dialog with no parent, such as one created in code. The dialog is a child of this
    /// view while open, which provides its parent window and inherited values. Defaults to <see cref="Application.MainView"/>.
    /// A dialog declared in a view stays in that view and ignores this.
    /// </summary>
    public View? Owner
    {
        get => _owner ?? Application.Current.MainView as View;
        set => _owner = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the parent ignores input while this dialog is open, and Escape closes it.
    /// A non-modal dialog instead closes on a press outside it. Defaults to <see langword="true"/>. A change applies the next time it opens.
    /// </summary>
    public bool IsModal
    {
        get => GetValue(IsModalProperty);
        set => SetValue(IsModalProperty, value);
    }

    /// <summary>
    /// Gets or sets the title text. <see cref="TitleContent"/> takes precedence.
    /// </summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>
    /// Gets or sets the description text. <see cref="DescriptionContent"/> takes precedence.
    /// </summary>
    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>
    /// Gets or sets custom title content, which <c>&lt;Dialog.Title&gt;</c> sets in XAML.
    /// </summary>
    public UIElement? TitleContent
    {
        get => GetValue(TitleContentProperty);
        set => SetValue(TitleContentProperty, value);
    }

    /// <summary>
    /// Gets or sets custom description content, which <c>&lt;Dialog.Description&gt;</c> sets in XAML.
    /// </summary>
    public UIElement? DescriptionContent
    {
        get => GetValue(DescriptionContentProperty);
        set => SetValue(DescriptionContentProperty, value);
    }

    /// <summary>
    /// Gets or sets the elements between the description and the footer. Assigning replaces the existing elements.
    /// </summary>
    public IList<UIElement> Content
    {
        get => _contentPart.Children;
        set => ReplaceElements(_contentPart.Children, value);
    }

    /// <summary>
    /// Gets or sets the elements in the footer row, which sit at its right end. Assigning replaces the existing elements.
    /// </summary>
    public IList<UIElement> Footer
    {
        get => _footerPart.Children;
        set => ReplaceElements(_footerPart.Children, value);
    }

    /// <summary>
    /// Opens this dialog like <see cref="Portal.Show"/>, and returns a task that completes when it closes, whatever closes
    /// it. The UI thread keeps running while the task is pending.
    /// </summary>
    /// <param name="anchor">The element to open next to, or <see langword="null"/> to center.</param>
    /// <exception cref="InvalidOperationException">This dialog has no parent and <see cref="Owner"/> is <see langword="null"/>.</exception>
    public Task ShowAsync(UIElement? anchor = null)
    {
        return OpenAndWaitForClose(anchor);
    }

    /// <summary>
    /// Opens this dialog like <see cref="ShowAsync(UIElement?)"/>, and returns the value passed to <see cref="Close(object?)"/>.
    /// Any other close, such as Escape or the title bar's close button, returns the default of <typeparamref name="TResult"/>,
    /// so a nullable type such as <c>bool?</c> tells them apart.
    /// </summary>
    /// <param name="anchor">The element to open next to, or <see langword="null"/> to center.</param>
    /// <exception cref="InvalidOperationException">This dialog has no parent and <see cref="Owner"/> is <see langword="null"/>.</exception>
    public async Task<TResult?> ShowAsync<TResult>(UIElement? anchor = null)
    {
        object? result = await OpenAndWaitForClose(anchor);
        return result is TResult typedResult ? typedResult : default;
    }

    /// <summary>
    /// Closes this dialog and completes a pending <see cref="ShowAsync{TResult}"/> with <paramref name="result"/>.
    /// Does nothing when it is already closed.
    /// </summary>
    public void Close(object? result)
    {
        if (IsOpen)
        {
            _closeResult = result;
            Close();
        }
    }

    /// <inheritdoc />
    public bool HasOwnWindow
    {
        get => GetValue(HasOwnWindowProperty);
        set => SetValue(HasOwnWindowProperty, value);
    }

    /// <inheritdoc />
    internal override bool ClosesOnOutsidePress => !IsModal;

    /// <inheritdoc />
    private protected override void OnOpening()
    {
        if (Parent is not null)
        {
            return;
        }

        View owner = Owner ?? throw new InvalidOperationException(
            $"Set {nameof(Owner)} or {nameof(Application)}.{nameof(Application.MainView)} before showing a dialog that has no parent.");
        _isAttachedToOwner = true;
        owner.Children.Add(this);
    }

    /// <inheritdoc />
    private protected override void OnClosed()
    {
        if (_isAttachedToOwner)
        {
            _isAttachedToOwner = false;
            (Parent as View)?.Children.Remove(this);
        }

        TaskCompletionSource<object?>? completion = _closeCompletion;
        object? result = _closeResult;
        _closeCompletion = null;
        _closeResult = null;
        completion?.SetResult(result);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Close();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Opens this dialog, or moves it when it is already open, and returns the task of the pending close. The task
    /// continues on the thread that closes the dialog, which is the UI thread.
    /// </summary>
    private Task<object?> OpenAndWaitForClose(UIElement? anchor)
    {
        TaskCompletionSource<object?> completion = _closeCompletion ??= new TaskCompletionSource<object?>();
        try
        {
            Show(anchor);
        }
        catch
        {
            _closeCompletion = null;
            throw;
        }

        return completion.Task;
    }

    private static void ReplaceElements(IList<UIElement> target, IList<UIElement> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);
        UIElement[] replacement = [.. elements];
        target.Clear();
        foreach (UIElement element in replacement)
        {
            target.Add(element);
        }
    }

    /// <summary>
    /// Shows <paramref name="content"/> in <paramref name="part"/>, or otherwise <paramref name="value"/> in <paramref name="text"/>,
    /// and hides the part when it has neither.
    /// </summary>
    private static void UpdatePart(View part, Text text, UIElement? content, string? value)
    {
        UIElement? shown = null;
        if (content is not null)
        {
            shown = content;
        }
        else if (!string.IsNullOrEmpty(value))
        {
            text.Value = value;
            shown = text;
        }

        if (part.Children.Count == 0 || !ReferenceEquals(part.Children[0], shown))
        {
            part.Children.Clear();
            if (shown is not null)
            {
                part.Children.Add(shown);
            }
        }

        part.IsVisible = shown is not null;
    }

    private void UpdateTitle()
    {
        UpdatePart(_titlePart, _titleText, TitleContent, Title);
    }

    private void UpdateDescription()
    {
        UpdatePart(_descriptionPart, _descriptionText, DescriptionContent, Description);
    }

    private void OnFooterChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _footerPart.IsVisible = _footerPart.Children.Count > 0;
    }
}

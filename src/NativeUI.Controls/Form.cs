using System.Collections.Specialized;
using System.Windows.Input;
using NativeUI.Core;

namespace NativeUI.Controls;

/// <summary>
/// Coordinates validation, submission, and control reset without owning model values or business rules.
/// </summary>
public class Form : Control
{
    /// <summary>
    /// Identifies the command executed after successful validation.
    /// </summary>
    public static readonly BindableProperty<ICommand?> SubmitCommandProperty =
        BindableProperty.Create<Form, ICommand?>(
            nameof(SubmitCommand));

    /// <summary>
    /// Identifies the submission command parameter.
    /// </summary>
    public static readonly BindableProperty<object?> SubmitCommandParameterProperty =
        BindableProperty.Create<Form, object?>(
            nameof(SubmitCommandParameter));

    /// <summary>
    /// Identifies the observable aggregate validation state.
    /// </summary>
    public static readonly ComputedBindableProperty<Form, bool> IsValidProperty =
        BindableProperty.CreateComputed<Form, bool>(
            nameof(IsValid), static form => form.IsValid);

    /// <summary>
    /// Identifies whether form-managed command execution is in progress.
    /// </summary>
    public static readonly ComputedBindableProperty<Form, bool> IsSubmittingProperty =
        BindableProperty.CreateComputed<Form, bool>(
            nameof(IsSubmitting), static form => form.IsSubmitting);

    private readonly HashSet<UIElement> _elements = [];
    private readonly HashSet<UIElement> _fields = [];
    private bool _isValid = true;
    private bool _disposed;
    private bool _isRequestingSubmit;
    private bool _isResetting;
    private Task _submissionTask = Task.CompletedTask;

    /// <summary>
    /// Creates a form with one keyboard submission path.
    /// </summary>
    public Form()
    {
        KeyDown += OnKeyDown;
        AddHandler(Button.ClickEvent, OnClick);
        Reconcile();
    }

    /// <inheritdoc />
    public override string TagName => "form";

    /// <summary>
    /// Gets or sets the command invoked after validation and the Submit notification.
    /// </summary>
    public ICommand? SubmitCommand
    {
        get => GetValue(SubmitCommandProperty);
        set => SetValue(SubmitCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets the parameter passed to the submission command.
    /// </summary>
    public object? SubmitCommandParameter
    {
        get => GetValue(SubmitCommandParameterProperty);
        set => SetValue(SubmitCommandParameterProperty, value);
    }

    /// <summary>
    /// Gets the current validity of participating, effectively enabled controls.
    /// </summary>
    public bool IsValid => _isValid;

    /// <summary>
    /// Gets whether the current form-managed submission has not completed.
    /// </summary>
    public bool IsSubmitting => !_submissionTask.IsCompleted;

    /// <summary>
    /// Gets completion and any exception from the most recent submission, including synchronous commands.
    /// </summary>
    public Task SubmissionTask => _submissionTask;

    /// <summary>
    /// Occurs after successful validation, before command execution; handlers can cancel submission.
    /// </summary>
    public event EventHandler<FormSubmitEventArgs>? Submit;

    /// <summary>
    /// Occurs after participating UI values have reset and validation has refreshed.
    /// </summary>
    public event EventHandler? Reset;

    /// <summary>
    /// Refreshes participating control errors and returns their aggregate validity.
    /// </summary>
    public bool Validate()
    {
        Reconcile();
        foreach (UIElement element in _fields.ToArray())
        {
            if (element.IsEffectivelyEnabled)
            {
                ((IFormControl)element).CheckValidity();
            }
        }

        RefreshValidity();
        return IsValid;
    }

    /// <summary>
    /// Requests validated submission; observe SubmissionTask for completion and failures.
    /// </summary>
    public void RequestSubmit() => RequestSubmit(null);

    internal void RequestSubmit(Button? submitter)
    {
        if (_disposed || _isRequestingSubmit || _isResetting || IsSubmitting ||
            !IsEffectivelyEnabled || submitter is { IsEffectivelyEnabled: false })
        {
            return;
        }

        _isRequestingSubmit = true;
        try
        {
            if (!Validate())
            {
                foreach (UIElement element in EnumerateTree(this))
                {
                    if (_fields.Contains(element) && element is IFormControl field && field.WillValidate &&
                        !ValidityState.IsEffectivelyValid(element, field))
                    {
                        field.ReportValidity();
                        break;
                    }
                }

                return;
            }

            if (SubmitCommand?.CanExecute(SubmitCommandParameter) == false)
            {
                return;
            }

            TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _submissionTask = completion.Task;
            _ = ExecuteSubmission(submitter, completion);
        }
        finally
        {
            _isRequestingSubmit = false;
        }
    }

    private async Task ExecuteSubmission(Button? submitter, TaskCompletionSource completion)
    {
        Exception? failure = null;
        try
        {
            NotifyPropertyChanged(IsSubmittingProperty, false, true);
            FormSubmitEventArgs args = new(submitter);
            Submit?.Invoke(this, args);
            if (!args.Cancel && !_disposed && IsEffectivelyEnabled &&
                SubmitCommand is ICommand command && command.CanExecute(SubmitCommandParameter))
            {
                if (command is IAsyncCommand asynchronous)
                {
                    await asynchronous.ExecuteAsync(SubmitCommandParameter).ConfigureAwait(false);
                }
                else
                {
                    command.Execute(SubmitCommandParameter);
                }
            }
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            Dispatcher.Invoke(() =>
            {
                if (failure is OperationCanceledException)
                {
                    completion.TrySetCanceled();
                }
                else if (failure is not null)
                {
                    completion.TrySetException(failure);
                }
                else
                {
                    completion.TrySetResult();
                }

                if (!_disposed)
                {
                    NotifyPropertyChanged(IsSubmittingProperty, true, false);
                }
            });
        }
    }

    /// <summary>
    /// Restores captured UI values through normal bindings; does not roll back the ViewModel.
    /// </summary>
    public void ResetForm()
    {
        if (_disposed || _isResetting || _isRequestingSubmit || IsSubmitting)
        {
            return;
        }

        _isResetting = true;
        try
        {
            Reconcile();
            foreach (UIElement element in _fields.ToArray())
            {
                ((IFormControl)element).Reset();
            }

            Validate();
            Reset?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _isResetting = false;
        }
    }

    /// <summary>
    /// Returns the containing form, stopping at its ownership boundary.
    /// </summary>
    internal static Form? GetForm(UIElement element)
    {
        for (UIElement? current = element.Parent; current is not null; current = current.Parent)
        {
            if (current is Form form)
            {
                return form;
            }
        }

        return null;
    }

    /// <inheritdoc />
    protected internal override void ValidateChild(UIElement child)
    {
        base.ValidateChild(child);
        if (EnumerateTree(child).Any(static element => element is Form))
        {
            throw new InvalidOperationException("Forms cannot be nested.");
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree()
    {
        base.OnAttachedToVisualTree();
        Reconcile();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _disposed = true;
            Unsubscribe();
        }

        base.Dispose(disposing);
    }

    private void OnClick(object? sender, RoutedEventArgs args)
    {
        if (args.Source is not Button { IsEffectivelyEnabled: true } button)
        {
            return;
        }

        switch (button.Type)
        {
            case ButtonType.Submit:
                RequestSubmit(button);
                break;
            case ButtonType.Reset:
                ResetForm();
                break;
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs args)
    {
        if (!args.Handled && args.Key == Key.Enter && args.Source is Input && !args.IsRepeat)
        {
            RequestSubmit();
            args.Handled = true;
        }
    }

    private void Reconcile()
    {
        if (_disposed)
        {
            return;
        }

        HashSet<UIElement> current = [.. EnumerateTree(this)];
        foreach (UIElement removed in _elements.Except(current).ToArray())
        {
            Unsubscribe(removed);
            _elements.Remove(removed);
            _fields.Remove(removed);
        }

        foreach (UIElement element in current)
        {
            if (!_elements.Add(element))
            {
                continue;
            }

            element.PropertyChanged += OnFieldChanged;
            if (element is View view)
            {
                ((INotifyCollectionChanged)view.Children).CollectionChanged += OnChildrenChanged;
            }

            if (element is IFormControl field)
            {
                _fields.Add(element);
                field.CaptureInitialValue();
            }
        }

        RefreshValidity();
    }

    private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs args) => Reconcile();

    private void OnFieldChanged(object? sender, BindablePropertyChangedEventArgs args)
    {
        if (ReferenceEquals(args.Property, Validation.HasErrorProperty) ||
            ReferenceEquals(args.Property, ValidityState.StateProperty) ||
            ReferenceEquals(args.Property, IsEffectivelyEnabledProperty))
        {
            RefreshValidity();
        }
    }

    private void RefreshValidity()
    {
        bool next = _fields.All(static field => ValidityState.IsEffectivelyValid(field, (IFormControl)field));
        if (_isValid != next)
        {
            bool previous = _isValid;
            _isValid = next;
            NotifyPropertyChanged(IsValidProperty, previous, next);
        }
    }

    private static IEnumerable<UIElement> EnumerateTree(UIElement element)
    {
        yield return element;
        if (element is View view)
        {
            foreach (UIElement child in view.Children.ToArray())
            {
                foreach (UIElement descendant in EnumerateTree(child))
                {
                    yield return descendant;
                }
            }
        }
    }

    private void Unsubscribe(UIElement element)
    {
        element.PropertyChanged -= OnFieldChanged;
        if (element is View view)
        {
            ((INotifyCollectionChanged)view.Children).CollectionChanged -= OnChildrenChanged;
        }
    }

    private void Unsubscribe()
    {
        foreach (UIElement element in _elements)
        {
            Unsubscribe(element);
        }

        _elements.Clear();
        _fields.Clear();
    }
}

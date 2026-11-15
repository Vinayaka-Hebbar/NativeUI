using System.Collections;
using System.ComponentModel;

namespace NativeUI.Core;

/// <summary>
/// Owns a binding's provider subscriptions and conversion failure.
/// </summary>
internal sealed class BindingValidation(BindableObject target, BindableProperty targetProperty, string propertyName) : IDisposable
{
    private object? _source;
    private ValidationError? _conversionError;

    internal bool HasConversionError => _conversionError is not null;

    internal IReadOnlyList<ValidationError> Errors { get; private set; } = [];

    internal void SetSource(object? source)
    {
        if (_source is INotifyDataErrorInfo previous)
        {
            previous.ErrorsChanged -= OnErrorsChanged;
        }

        _source = source;
        _conversionError = null;
        if (source is INotifyDataErrorInfo current)
        {
            current.ErrorsChanged += OnErrorsChanged;
        }

        Refresh();
    }

    internal void SetConversionError(Exception? exception)
    {
        _conversionError = exception is null ? null : new ValidationError(exception.Message, propertyName, ValidationErrorKind.Conversion, targetProperty);
        Refresh();
    }

    internal void Refresh()
    {
        List<ValidationError> errors = [];
        if (_conversionError is not null)
        {
            errors.Add(_conversionError);
        }

        if (_source is INotifyDataErrorInfo notify)
        {
            AddErrors(notify.GetErrors(propertyName), errors);
        }
        else if (_source is IDataErrorInfo legacy)
        {
            if (!string.IsNullOrEmpty(legacy[propertyName]))
            {
                errors.Add(new ValidationError(legacy[propertyName], propertyName, TargetProperty: targetProperty));
            }
        }

        ValidationError[] next = errors.Distinct().ToArray();
        if (!Errors.SequenceEqual(next))
        {
            Errors = Array.AsReadOnly(next);
            target.NotifyBindingValidationChanged();
        }
    }

    private void AddErrors(IEnumerable? source, List<ValidationError> errors)
    {
        if (source is null)
        {
            return;
        }

        foreach (object? error in source)
        {
            if (error is not null)
            {
                errors.Add(error is ValidationError existing
                    ? existing with { TargetProperty = targetProperty }
                    : new ValidationError(error, propertyName, TargetProperty: targetProperty));
            }
        }
    }

    private void OnErrorsChanged(object? sender, DataErrorsChangedEventArgs args)
    {
        if (string.IsNullOrEmpty(args.PropertyName) || args.PropertyName == propertyName)
        {
            object? source = _source;
            target.Dispatcher.Invoke(() =>
            {
                if (ReferenceEquals(source, _source))
                {
                    Refresh();
                }
            });
        }
    }

    public void Dispose() => SetSource(null);
}

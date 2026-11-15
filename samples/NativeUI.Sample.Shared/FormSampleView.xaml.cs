using NativeUI.Controls;
using NativeUI.Core;

namespace NativeUI.Sample;

/// <summary>Demonstrates native form primitives with shared ViewModel validation.</summary>
public partial class FormSampleView
{
    /// <summary>Creates the form and presents its attached validation messages.</summary>
    public FormSampleView()
    {
        DataContext = new FormSampleViewModel();
        InitializeComponent();
        NameInput.PropertyChanged += OnValidationChanged;
        EmailInput.PropertyChanged += OnValidationChanged;
        DeliveryGroup.PropertyChanged += OnValidationChanged;
        EditorForm.PropertyChanged += OnFormPropertyChanged;
        SaveButton.IsEnabled = EditorForm.IsValid && !EditorForm.IsSubmitting;
        UpdateMessages();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            EditorForm.PropertyChanged -= OnFormPropertyChanged;
            NameInput.PropertyChanged -= OnValidationChanged;
            EmailInput.PropertyChanged -= OnValidationChanged;
            DeliveryGroup.PropertyChanged -= OnValidationChanged;
        }

        base.Dispose(disposing);
    }

    private void OnFormPropertyChanged(object? sender, BindablePropertyChangedEventArgs args)
    {
        if (ReferenceEquals(args.Property, Form.IsValidProperty) ||
            ReferenceEquals(args.Property, Form.IsSubmittingProperty))
        {
            SaveButton.IsEnabled = EditorForm.IsValid && !EditorForm.IsSubmitting;
        }
    }

    private void OnValidationChanged(object? sender, BindablePropertyChangedEventArgs args)
    {
        if (ReferenceEquals(args.Property, Validation.ErrorsProperty) || ReferenceEquals(args.Property, Input.ValueProperty) ||
            ReferenceEquals(args.Property, RadioGroup.SelectedValueProperty))
        {
            UpdateMessages();
        }
    }

    private void UpdateMessages()
    {
        NameErrors.Value = NameInput.ValidationMessage;
        EmailErrors.Value = EmailInput.ValidationMessage;
        DeliveryErrors.Value = DeliveryGroup.ValidationMessage;
    }
}

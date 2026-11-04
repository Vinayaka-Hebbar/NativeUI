using NativeUI.Controls;
using NativeUI.Core;

namespace NativeUI.Sample;

/// <summary>Demonstrates Switch, CheckBox, Toggle, and ToggleGroup styled with state pseudo-classes and template parts.</summary>
public partial class ToggleSampleView
{
    private bool _isUpdatingSelectAll;

    /// <summary>Creates the examples and connects the status texts and the select-all check box to the controls.</summary>
    public ToggleSampleView()
    {
        InitializeComponent();

        AirplaneSwitch.PropertyChanged += (_, args) =>
        {
            if (ReferenceEquals(args.Property, Switch.IsCheckedProperty))
            {
                AirplaneStatus.Value = AirplaneSwitch.IsChecked ? "Airplane mode is on" : "Airplane mode is off";
            }
        };

        SelectAllCheckBox.PropertyChanged += (_, args) =>
        {
            if (ReferenceEquals(args.Property, CheckBox.IsCheckedProperty))
            {
                OnSelectAllChanged();
            }
        };

        EmailCheckBox.PropertyChanged += OnNotificationChanged;
        PushCheckBox.PropertyChanged += OnNotificationChanged;
        UpdateSelectAll();

        AlignmentGroup.PropertyChanged += (_, args) =>
        {
            if (ReferenceEquals(args.Property, ToggleGroup.SelectedValueProperty))
            {
                AlignmentStatus.Value = "Alignment: " + (AlignmentGroup.SelectedValue ?? "none");
            }
        };

        FormatGroup.PropertyChanged += (_, args) =>
        {
            if (ReferenceEquals(args.Property, ToggleGroup.SelectedValuesProperty))
            {
                FormatStatus.Value = FormatGroup.SelectedValues.Count == 0
                    ? "Formats: none"
                    : "Formats: " + string.Join(", ", FormatGroup.SelectedValues);
            }
        };
    }

    /// <summary>
    /// Checks or unchecks every notification check box when the user clicks the select-all check box.
    /// </summary>
    private void OnSelectAllChanged()
    {
        if (_isUpdatingSelectAll || SelectAllCheckBox.IsChecked is not bool isChecked)
        {
            return;
        }

        _isUpdatingSelectAll = true;
        EmailCheckBox.IsChecked = isChecked;
        PushCheckBox.IsChecked = isChecked;
        _isUpdatingSelectAll = false;
        UpdateSelectAll();
    }

    private void OnNotificationChanged(object? sender, BindablePropertyChangedEventArgs args)
    {
        if (ReferenceEquals(args.Property, CheckBox.IsCheckedProperty) && !_isUpdatingSelectAll)
        {
            UpdateSelectAll();
        }
    }

    /// <summary>
    /// Shows the select-all check box as checked when every notification is checked, unchecked when none is, and
    /// indeterminate otherwise.
    /// </summary>
    private void UpdateSelectAll()
    {
        bool email = EmailCheckBox.IsChecked == true;
        bool push = PushCheckBox.IsChecked == true;
        _isUpdatingSelectAll = true;
        SelectAllCheckBox.IsChecked = email == push ? email : null;
        _isUpdatingSelectAll = false;
    }
}

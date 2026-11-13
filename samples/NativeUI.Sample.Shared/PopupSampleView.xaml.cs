namespace NativeUI.Sample;

/// <summary>Demonstrates Popover with a referenced target and with a target declared inside the popover.</summary>
public partial class PopupSampleView
{
    /// <summary>Creates the examples and reports the open state of the first popover.</summary>
    public PopupSampleView()
    {
        InitializeComponent();

        DimensionsPopover.Opened += (_, _) => DimensionsStatus.Value = "Open";
        DimensionsPopover.Closed += (_, _) => DimensionsStatus.Value = "Closed";
    }
}

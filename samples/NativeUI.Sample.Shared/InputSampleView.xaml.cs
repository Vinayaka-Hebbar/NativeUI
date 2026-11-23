namespace NativeUI.Sample;

/// <summary>
/// Demonstrates input binding and browser or native focus.
/// </summary>
public partial class InputSampleView
{
    /// <summary>Initializes the sample and its interactive behavior.</summary>
    public InputSampleView()
    {
        InitializeComponent();

        DataContext = new HelloViewModel();
        FocusNameButton.Click += (_, _) => NameInput.Focus();
        NameInput.GotFocus += (_, _) => FocusStatus.Value = "The field has focus";
        NameInput.LostFocus += (_, _) => FocusStatus.Value = "The field does not have focus";
    }
}

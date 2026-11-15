using CommunityToolkit.Mvvm.ComponentModel;

namespace NativeUI.Sample;

/// <summary>Supplies editable text for the inline binding example.</summary>
public partial class TextSampleViewModel : ObservableObject
{
    [ObservableProperty]
    private string _firstName = "Ada";

    [ObservableProperty]
    private string _lastName = "Lovelace";
}

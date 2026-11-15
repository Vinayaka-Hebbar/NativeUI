namespace NativeUI.Sample;

/// <summary>Demonstrates inline formatting, bindings, links, and shared text selection.</summary>
public partial class TextSampleView
{
    /// <summary>Initializes the text examples and their bound values.</summary>
    public TextSampleView()
    {
        DataContext = new TextSampleViewModel();
        InitializeComponent();
    }
}

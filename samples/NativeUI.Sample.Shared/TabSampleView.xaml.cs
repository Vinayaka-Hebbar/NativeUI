using System.Collections.ObjectModel;

namespace NativeUI.Sample;

/// <summary>
/// Demonstrates tab header styles and data-bound document pages.
/// </summary>
public partial class TabSampleView
{
    /// <summary>
    /// Initializes the examples with two independently editable documents.
    /// </summary>
    public TabSampleView()
    {
        InitializeComponent();
        ObservableCollection<TabDocument> documents = [];
        documents.Add(new TabDocument("Settings.cs", "Settings document", documents));
        documents.Add(new TabDocument("App.xaml", "Application document", documents));
        EditorTabs.ItemsSource = documents;
    }
}
